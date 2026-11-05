import { useCallback, useEffect, useRef, useState, type DependencyList } from 'react'
import { getErrorMessage } from '../services/apiClient'

export interface AsyncState<T> {
  data: T | null
  loading: boolean
  error: string | null
  /** HTTP status của lỗi (404 → trang không tồn tại) */
  status: number | null
  reload: () => void
  setData: (data: T | null) => void
}

/** Gọi API khi deps đổi; bỏ qua kết quả cũ nếu request mới đã bắt đầu. */
export function useAsync<T>(fn: () => Promise<T>, deps: DependencyList): AsyncState<T> {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [status, setStatus] = useState<number | null>(null)
  const [nonce, setNonce] = useState(0)
  const seq = useRef(0)

  useEffect(() => {
    const id = ++seq.current
    setLoading(true)
    setError(null)
    setStatus(null)
    fn()
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
  }, [...deps, nonce])

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

export function useDocumentTitle(title: string | undefined) {
  useEffect(() => {
    if (title) document.title = `${title} · EduViet`
  }, [title])
}
