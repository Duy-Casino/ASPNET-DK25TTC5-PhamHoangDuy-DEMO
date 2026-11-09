import { Link, useSearchParams } from 'react-router-dom'
import { Spinner } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { authService } from '../../services/api'

export default function ConfirmEmailPage() {
  useDocumentTitle('Xác nhận email')
  const [params] = useSearchParams()
  const userId = params.get('userId') ?? ''
  const token = params.get('token') ?? ''
  const { loading, error } = useAsync(
    () => (userId && token ? authService.confirmEmail(userId, token) : Promise.reject(new Error('missing'))),
    [userId, token],
  )

  return (
    <section className="container-page flex min-h-[60vh] flex-col justify-center py-24">
      {loading ? (
        <p className="flex items-center gap-3 text-lg" role="status"><Spinner className="size-6 text-brand-600" /> Đang xác nhận email…</p>
      ) : error ? (
        <>
          <h1 className="text-title font-semibold">Không xác nhận được email</h1>
          <p className="mt-4 text-lg text-ink-soft">Liên kết không hợp lệ hoặc đã hết hạn.</p>
          <Link to="/" className="btn-primary mt-8 w-fit">Về trang chủ</Link>
        </>
      ) : (
        <>
          <h1 className="text-title font-semibold">Email đã được xác nhận</h1>
          <p className="mt-4 text-lg text-ink-soft">Cảm ơn bạn! Tài khoản của bạn đã sẵn sàng.</p>
          <Link to="/khoa-hoc" className="btn-primary mt-8 w-fit">Khám phá khóa học</Link>
        </>
      )}
    </section>
  )
}
