import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Field, Spinner } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'

/** Chỉ cho phép quay lại đường dẫn nội bộ (chống open redirect). */
export function safeNext(next: string | null): string {
  return next && next.startsWith('/') && !next.startsWith('//') ? next : '/'
}

export default function LoginPage() {
  useDocumentTitle('Đăng nhập')
  const { login, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const next = safeNext(params.get('next'))

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (isAuthenticated && !loading) return <Navigate to={next} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setErrors({})
    setFormError(null)
    setLoading(true)
    try {
      const user = await login(email.trim(), password)
      toast.success(`Chào mừng trở lại, ${user.fullName}!`)
      navigate(next, { replace: true })
    } catch (err) {
      setErrors(getFieldErrors(err))
      setFormError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      <h1 className="text-title font-extrabold">Đăng nhập</h1>
      <p className="mt-3 text-lg text-ink-soft">Tiếp tục khóa học bạn đang học dở.</p>
      <form onSubmit={submit} className="mt-10 flex flex-col gap-5" noValidate>
        {formError && <p role="alert" className="bg-danger-50 p-4 font-medium text-danger">{formError}</p>}
        <Field label="Email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} error={errors.email} />
        <Field label="Mật khẩu" type="password" autoComplete="current-password" required value={password}
          onChange={(e) => setPassword(e.target.value)} error={errors.password} />
        <Link to="/quen-mat-khau" className="-mt-2 w-fit text-sm font-semibold text-brand-700 hover:underline">Quên mật khẩu?</Link>
        <button type="submit" className="btn-primary min-h-12 text-base" disabled={loading}>
          {loading && <Spinner className="size-4" />} Đăng nhập
        </button>
      </form>
      <p className="mt-8 text-ink-soft">
        Chưa có tài khoản?{' '}
        <Link to={`/dang-ky${params.get('next') ? `?next=${encodeURIComponent(next)}` : ''}`} className="font-semibold text-brand-700 hover:underline">Đăng ký miễn phí</Link>
      </p>
    </>
  )
}
