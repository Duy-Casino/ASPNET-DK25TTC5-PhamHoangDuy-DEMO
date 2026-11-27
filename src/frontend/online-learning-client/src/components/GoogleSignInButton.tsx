import { useEffect, useRef, useState } from 'react'
import { publicSettingsService } from '../services/adminToolsApi'

interface GoogleId {
  initialize: (o: { client_id: string; callback: (r: { credential: string }) => void; ux_mode?: 'popup'; cancel_on_tap_outside?: boolean }) => void
  renderButton: (el: HTMLElement, o: Record<string, unknown>) => void
}
declare global {
  interface Window { google?: { accounts: { id: GoogleId } } }
}

let scriptPromise: Promise<void> | null = null
function loadGoogleScript() {
  scriptPromise ??= new Promise<void>((resolve, reject) => {
    const s = document.createElement('script')
    s.src = 'https://accounts.google.com/gsi/client'
    s.async = true
    s.defer = true
    s.onload = () => resolve()
    s.onerror = () => { scriptPromise = null; reject(new Error('Không tải được Google')) }
    document.head.appendChild(s)
  })
  return scriptPromise
}

/**
 * Nút "Đăng nhập bằng Google" (Google Identity Services). Chỉ hiện khi máy chủ đã cấu hình
 * Authentication:Google:ClientId; ID token nhận được gửi lên /api/auth/google để máy chủ xác minh.
 */
export function GoogleSignInButton({ text = 'continue_with', onCredential, disabled = false }: {
  text?: 'continue_with' | 'signup_with' | 'signin_with'
  onCredential: (credential: string) => void
  disabled?: boolean
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [clientId, setClientId] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  const callback = useRef(onCredential)
  useEffect(() => { callback.current = onCredential }, [onCredential])

  useEffect(() => {
    let alive = true
    publicSettingsService.get().then((s) => alive && setClientId(s.googleClientId ?? null)).catch(() => undefined)
    return () => { alive = false }
  }, [])

  useEffect(() => {
    if (!clientId) return
    let alive = true
    loadGoogleScript().then(() => {
      const el = ref.current
      if (!alive || !el || !window.google) return
      window.google.accounts.id.initialize({ client_id: clientId, callback: (r) => callback.current(r.credential), ux_mode: 'popup', cancel_on_tap_outside: true })
      window.google.accounts.id.renderButton(el, {
        type: 'standard', theme: 'outline', size: 'large', shape: 'pill', text, locale: 'vi', logo_alignment: 'center',
        width: Math.min(400, Math.max(240, Math.floor(el.parentElement?.clientWidth ?? 320))),
      })
    }).catch(() => alive && setFailed(true))
    return () => { alive = false }
  }, [clientId, text])

  if (!clientId) return null
  return (
    <div className="mt-6">
      <div className="flex items-center gap-3 text-sm text-ink-muted" aria-hidden="true">
        <span className="h-px flex-1 bg-line" />hoặc<span className="h-px flex-1 bg-line" />
      </div>
      <div className={`mt-5 flex justify-center ${disabled ? 'pointer-events-none opacity-60' : ''}`}>
        <div ref={ref} className="min-h-11" />
      </div>
      {failed && <p className="mt-2 text-center text-sm text-ink-soft">Không tải được nút Google. Kiểm tra kết nối mạng hoặc dùng email và mật khẩu.</p>}
    </div>
  )
}
