import { useCallback, useEffect, useRef, useState, type DependencyList } from 'react'
import { getErrorMessage } from '../services/apiClient'
import { useSeo } from './useSeo'

export interface AsyncState<T> {
  data: T | null
  loading: boolean
  error: string | null
  /** HTTP status của lỗi (404 → trang không tồn tại) */
  status: number | null
  reload: () => void
  setData: (data: T | null) => void
}

/** Lỗi đáng thử lại: mất kết nối, hết thời gian chờ hoặc máy chủ lỗi 5xx (không thử lại lỗi 4xx). */
function isTransient(e: unknown): boolean {
  const s = (e as { response?: { status?: number } }).response?.status
  return s === undefined || s >= 500
}

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))

/**
 * Gọi API khi deps đổi; bỏ qua kết quả cũ nếu request mới đã bắt đầu.
 * `retries`: số lần tự thử lại khi lỗi tạm thời (chờ 1s, 2s, …) trước khi báo lỗi.
 */
export function useAsync<T>(fn: () => Promise<T>, deps: DependencyList, options: { retries?: number } = {}): AsyncState<T> {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<number | null>(null)
  const [nonce, setNonce] = useState(0)
  const seq = useRef(0)
  const retries = options.retries ?? 0

  useEffect(() => {
    const id = ++seq.current
    setLoading(true)
    setError(null)
    setStatus(null)
    const attempt = async (n: number): Promise<T> => {
      try {
        return await fn()
      } catch (e) {
        if (n >= retries || !isTransient(e) || id !== seq.current) throw e
        await sleep(1000 * (n + 1))
        return attempt(n + 1)
      }
    }
    attempt(0)
      .then((d) => {
        if (id === seq.current) setData(d)
      })
      .catch((e: unknown) => {
        if (id !== seq.current) return
        setError(getErrorMessage(e))
        const s = (e as { response?: { status?: number } }).response?.status
        setStatus(s ?? null)
      })
      .finally(() => {
        if (id === seq.current) setLoading(false)
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, nonce, retries])

  const reload = useCallback(() => setNonce((n) => n + 1), [])
  return { data, loading, error, status, reload, setData }
}

export function useDebounce<T>(value: T, delay = 350): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = window.setTimeout(() => setDebounced(value), delay)
    return () => window.clearTimeout(id)
  }, [value, delay])
  return debounced
}

/** Đặt tiêu đề trang (kèm description/canonical/OG mặc định). Trang cần SEO riêng dùng useSeo. */
export function useDocumentTitle(title: string | undefined) {
  useSeo({ title })
}
