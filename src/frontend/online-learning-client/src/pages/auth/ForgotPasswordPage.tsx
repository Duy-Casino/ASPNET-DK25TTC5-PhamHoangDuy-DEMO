import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Field, Spinner } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { authService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'

export default function ForgotPasswordPage() {
  useDocumentTitle('Quên mật khẩu')
  const [email, setEmail] = useState('')
  const [sent, setSent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!/^\S+@\S+\.\S+$/.test(email.trim())) {
      setError('Email không hợp lệ.')
      return
    }
    setLoading(true)
    setError(null)
    try {
      await authService.forgotPassword(email.trim())
      setSent(true)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setLoading(false)
    }
  }

  if (sent) {
    return (
      <>
        <h1 className="text-title font-semibold">Kiểm tra email</h1>
        <p className="mt-4 text-lg text-ink-soft">
          Nếu <b className="text-ink">{email}</b> đã đăng ký, bạn sẽ nhận được liên kết đặt lại mật khẩu trong vài phút. Liên kết có hiệu lực 1 giờ.
        </p>
        <Link to="/dang-nhap" className="btn-primary mt-8">Quay lại đăng nhập</Link>
      </>
    )
  }

  return (
    <>
      <h1 className="text-title font-semibold">Quên mật khẩu</h1>
      <p className="mt-3 text-lg text-ink-soft">Nhập email đã đăng ký, chúng tôi sẽ gửi liên kết đặt lại mật khẩu.</p>
      <form onSubmit={submit} className="mt-10 flex flex-col gap-5" noValidate>
        <Field label="Email" type="email" autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} error={error ?? undefined} />
        <button type="submit" className="btn-primary min-h-12 text-base" disabled={loading}>
          {loading && <Spinner className="size-4" />} Gửi liên kết
        </button>
      </form>
      <Link to="/dang-nhap" className="mt-8 inline-block font-semibold text-brand-700 hover:underline">Quay lại đăng nhập</Link>
    </>
  )
}
