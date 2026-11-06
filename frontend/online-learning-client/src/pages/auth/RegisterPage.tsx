import { useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Field, Spinner } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { safeNext } from './LoginPage'

export const PASSWORD_HINT = 'Tối thiểu 8 ký tự, có chữ hoa, chữ thường và chữ số.'

export function validatePassword(pw: string): string | undefined {
  if (pw.length < 8 || !/[A-Z]/.test(pw) || !/[a-z]/.test(pw) || !/[0-9]/.test(pw)) return PASSWORD_HINT
}

export default function RegisterPage() {
  useDocumentTitle('Đăng ký')
  const { register, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const next = safeNext(params.get('next'))

  const [form, setForm] = useState({ fullName: '', email: '', password: '', confirm: '' })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (isAuthenticated && !loading) return <Navigate to={next} replace />

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => setForm((f) => ({ ...f, [k]: e.target.value }))

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    if (!form.fullName.trim()) local.fullName = 'Vui lòng nhập họ tên.'
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) local.email = 'Email không hợp lệ.'
    const pw = validatePassword(form.password)
    if (pw) local.password = pw
    if (form.confirm !== form.password) local.confirm = 'Mật khẩu nhập lại không khớp.'
    setErrors(local)
    setFormError(null)
    if (Object.keys(local).length) return

    setLoading(true)
    try {
      await register(form.fullName.trim(), form.email.trim(), form.password)
      toast.success('Tạo tài khoản thành công!')
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
      <h1 className="text-title font-semibold">Tạo tài khoản</h1>
      <p className="mt-3 text-lg text-ink-soft">Miễn phí, chỉ mất một phút.</p>
      <form onSubmit={submit} className="mt-10 flex flex-col gap-5" noValidate>
        {formError && <p role="alert" className="bg-danger-50 p-4 font-medium text-danger">{formError}</p>}
        <Field label="Họ và tên" autoComplete="name" value={form.fullName} onChange={set('fullName')} error={errors.fullName} />
        <Field label="Email" type="email" autoComplete="email" value={form.email} onChange={set('email')} error={errors.email} />
        <Field label="Mật khẩu" type="password" autoComplete="new-password" value={form.password} onChange={set('password')}
          error={errors.password} hint={PASSWORD_HINT} />
        <Field label="Nhập lại mật khẩu" type="password" autoComplete="new-password" value={form.confirm} onChange={set('confirm')} error={errors.confirm} />
        <button type="submit" className="btn-primary min-h-12 text-base" disabled={loading}>
          {loading && <Spinner className="size-4" />} Đăng ký
        </button>
      </form>
      <p className="mt-8 text-ink-soft">
        Đã có tài khoản? <Link to="/dang-nhap" className="font-semibold text-brand-700 hover:underline">Đăng nhập</Link>
      </p>
    </>
  )
}
