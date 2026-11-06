import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { Field, Spinner } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { authService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { PASSWORD_HINT, validatePassword } from './RegisterPage'

export default function ResetPasswordPage() {
  useDocumentTitle('Đặt lại mật khẩu')
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  if (!email || !token) {
    return (
      <>
        <h1 className="text-title font-semibold">Liên kết không hợp lệ</h1>
        <p className="mt-4 text-lg text-ink-soft">Liên kết đặt lại mật khẩu bị thiếu thông tin. Hãy yêu cầu liên kết mới.</p>
        <Link to="/quen-mat-khau" className="btn-primary mt-8">Gửi lại liên kết</Link>
      </>
    )
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const local: Record<string, string> = {}
    const pw = validatePassword(password)
    if (pw) local.password = pw
    if (confirm !== password) local.confirm = 'Mật khẩu nhập lại không khớp.'
    setErrors(local)
    if (Object.keys(local).length) return

    setLoading(true)
    setFormError(null)
    try {
      await authService.resetPassword(email, token, password)
      toast.success('Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới.')
      navigate('/dang-nhap', { replace: true })
    } catch (err) {
      setFormError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      <h1 className="text-title font-semibold">Đặt lại mật khẩu</h1>
      <p className="mt-3 text-lg text-ink-soft">Cho tài khoản <b className="text-ink">{email}</b></p>
      <form onSubmit={submit} className="mt-10 flex flex-col gap-5" noValidate>
        {formError && <p role="alert" className="bg-danger-50 p-4 font-medium text-danger">{formError}</p>}
        <Field label="Mật khẩu mới" type="password" autoComplete="new-password" value={password}
          onChange={(e) => setPassword(e.target.value)} error={errors.password} hint={PASSWORD_HINT} />
        <Field label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" value={confirm}
          onChange={(e) => setConfirm(e.target.value)} error={errors.confirm} />
        <button type="submit" className="btn-primary min-h-12 text-base" disabled={loading}>
          {loading && <Spinner className="size-4" />} Lưu mật khẩu mới
        </button>
      </form>
    </>
  )
}
