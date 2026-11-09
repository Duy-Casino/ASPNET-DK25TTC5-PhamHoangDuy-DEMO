import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import type { AuthResponse, ProblemDetails } from '../types/api'

/**
 * Axios dùng chung.
 * - Access token chỉ giữ trong bộ nhớ (không localStorage) để giảm rủi ro XSS đánh cắp token.
 * - Refresh token nằm trong cookie HttpOnly do API đặt; gọi /api/auth/refresh để lấy access token mới.
 * - Khi gặp 401, tự refresh một lần (gộp các request song song) rồi gửi lại.
 */
export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  withCredentials: true,
  timeout: 20000,
  headers: { 'Content-Type': 'application/json' },
})

let accessToken: string | null = null
let refreshPromise: Promise<AuthResponse | null> | null = null
const listeners = new Set<(auth: AuthResponse | null) => void>()

export const tokenStore = {
  get: () => accessToken,
  set: (token: string | null) => {
    accessToken = token
  },
  /** AuthContext đăng ký để biết khi phiên được làm mới hoặc hết hạn. */
  subscribe: (fn: (auth: AuthResponse | null) => void) => {
    listeners.add(fn)
    return () => listeners.delete(fn)
  },
}

export function refreshSession(): Promise<AuthResponse | null> {
  refreshPromise ??= axios
    .post<AuthResponse>(`${apiClient.defaults.baseURL}/api/auth/refresh`, null, { withCredentials: true })
    .then((r) => {
      accessToken = r.data.accessToken
      listeners.forEach((l) => l(r.data))
      return r.data
    })
    .catch(() => {
      accessToken = null
      listeners.forEach((l) => l(null))
      return null
    })
    .finally(() => {
      refreshPromise = null
    })
  return refreshPromise
}

apiClient.interceptors.request.use((config) => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

apiClient.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    const original = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined
    const isAuthCall = original?.url?.startsWith('/api/auth/')
    if (error.response?.status === 401 && original && !original._retried && !isAuthCall) {
      original._retried = true
      const session = await refreshSession()
      if (session) {
        original.headers.Authorization = `Bearer ${session.accessToken}`
        return apiClient(original)
      }
    }
    return Promise.reject(error)
  },
)

/** Lấy thông báo lỗi tiếng Việt từ ProblemDetails của API. */
export function getErrorMessage(error: unknown, fallback = 'Đã xảy ra lỗi. Vui lòng thử lại.'): string {
  if (axios.isAxiosError(error)) {
    if (!error.response) return 'Không kết nối được máy chủ. Kiểm tra mạng và thử lại.'
    const data = error.response.data as ProblemDetails | undefined
    if (data?.errors) {
      const first = Object.values(data.errors).flat()[0]
      if (first) return first
    }
    if (data?.title) return data.title
    if (error.response.status === 429) return 'Bạn thao tác quá nhanh. Vui lòng đợi một phút.'
  }
  return fallback
}

/** Lỗi validation theo từng trường (khóa camelCase) để hiển thị dưới ô nhập. */
export function getFieldErrors(error: unknown): Record<string, string> {
  if (!axios.isAxiosError(error)) return {}
  const data = error.response?.data as ProblemDetails | undefined
  const result: Record<string, string> = {}
  for (const [key, messages] of Object.entries(data?.errors ?? {})) {
    const field = key.charAt(0).toLowerCase() + key.slice(1)
    if (messages[0]) result[field] = messages[0]
  }
  return result
}
