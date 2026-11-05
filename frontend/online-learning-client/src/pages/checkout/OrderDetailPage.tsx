import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { CheckCircle2, Clock } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorState, OrderStatusBadge, Skeleton, Spinner } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { orderService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { paymentService, providerLabel, redirectToGateway, type PaymentProvider } from '../../services/paymentApi'
import { PaymentMethodPicker, TestModeNotice } from '../../components/payment/PaymentMethods'
import { formatDateTime, formatPrice } from '../../utils/format'
import NotFoundPage from '../NotFoundPage'

function useCountdown(until: string | undefined, active: boolean) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    if (!active) return
    const id = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(id)
  }, [active])
  if (!until) return null
  const ms = Math.max(0, new Date(until).getTime() - now)
  const m = Math.floor(ms / 60000)
  const s = Math.floor((ms % 60000) / 1000)
  return { ms, label: `${m}:${s.toString().padStart(2, '0')}` }
}

export default function OrderDetailPage() {
  const { id = '' } = useParams()
  const { data: order, loading, error, status, reload } = useAsync(() => orderService.get(id), [id])
  const [cancelling, setCancelling] = useState(false)
  const methods = useAsync(() => paymentService.methods(), [])
  const [method, setMethod] = useState<PaymentProvider | null>(null)
  const [paying, setPaying] = useState(false)
  useDocumentTitle(order ? `Đơn ${order.orderCode}` : 'Đơn hàng')
  const countdown = useCountdown(order?.expiresAt, order?.status === 'Pending')

  if (status === 404) return <NotFoundPage />
  if (error) return <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>
  if (loading || !order) return <div className="container-page py-16"><Skeleton className="h-96 max-w-2xl" /></div>

  const cancel = async () => {
    if (!window.confirm('Hủy đơn hàng này?')) return
    setCancelling(true)
    try {
      await orderService.cancel(order.id)
      toast.success('Đã hủy đơn hàng')
      reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setCancelling(false)
    }
  }

  const pay = async () => {
    const provider = method ?? methods.data?.[0]?.provider
    if (!provider) return
    setPaying(true)
    try {
      const session = await paymentService.checkout(order.id, provider)
      redirectToGateway(session.redirectUrl)
    } catch (err) {
      toast.error(getErrorMessage(err))
      setPaying(false)
    }
  }

  const paid = order.status === 'Paid'

  return (
    <div className="container-page grid gap-12 py-12 md:py-16 lg:grid-cols-[1fr_440px]">
      <div className="flex flex-col gap-6">
        {paid ? (
          <CheckCircle2 className="size-16 text-success" aria-hidden="true" />
        ) : (
          <Clock className="size-16 text-warning" aria-hidden="true" />
        )}
        <h1 className="text-title font-extrabold">
          {paid ? 'Đăng ký thành công' : order.status === 'Pending' ? 'Đơn hàng chờ thanh toán' : 'Chi tiết đơn hàng'}
        </h1>
        <div className="flex flex-wrap items-center gap-3">
          <OrderStatusBadge status={order.status} />
          <span className="text-ink-soft">Mã đơn <b className="text-ink">{order.orderCode}</b> · {formatDateTime(order.createdAt)}</span>
        </div>

        {paid && (
          <p className="max-w-xl text-lg text-ink-soft">
            Khóa học đã được thêm vào tài khoản của bạn. Quyền học chỉ được cấp sau khi hệ thống xác nhận thanh toán.
          </p>
        )}

        {order.status === 'Pending' && countdown && (
          <div className="max-w-xl bg-warning-50 p-5">
            <p className="font-bold">
              {countdown.ms > 0 ? <>Đơn còn hiệu lực trong <span className="tabular-nums">{countdown.label}</span></> : 'Đơn đã quá hạn thanh toán'}
            </p>
            <p className="mt-1 text-sm text-ink-soft">Đơn chưa thanh toán sẽ tự hết hạn sau 30 phút kể từ lúc đặt.</p>
            {countdown.ms > 0 && (
              <div className="mt-4 bg-white p-4">
                <PaymentMethodPicker methods={methods.data} loading={methods.loading} value={method ?? methods.data?.[0]?.provider ?? null} onChange={setMethod} />
              </div>
            )}
            <div className="mt-4 flex flex-wrap gap-3">
              {countdown.ms > 0 && (
                <button type="button" className="btn-primary" onClick={pay} disabled={paying || !(method ?? methods.data?.[0]?.provider)}>
                  {paying && <Spinner className="size-4" />} Thanh toán {formatPrice(order.totalAmount)}
                </button>
              )}
              <button type="button" className="btn-ghost text-danger" onClick={cancel} disabled={cancelling}>
                {cancelling && <Spinner className="size-4" />} Hủy đơn
              </button>
            </div>
          </div>
        )}

        {order.payments.some((p) => p.provider === 'Test') && <TestModeNotice className="max-w-xl" />}

        <div className="flex flex-wrap gap-3">
          <Link to="/tai-khoan/don-hang" className="btn-soft">Lịch sử mua hàng</Link>
          <Link to="/khoa-hoc" className="btn-ghost">Xem thêm khóa học</Link>
        </div>
      </div>

      <aside className="paper-receipt self-start bg-surface-2 px-6 pt-6 pb-10" aria-label="Hóa đơn">
        <p className="text-center font-extrabold">EduViet · Hóa đơn</p>
        <p className="text-center text-sm text-ink-muted">{order.orderCode}</p>
        <dl className="mt-6 flex flex-col gap-3 tabular-nums">
          {order.items.map((i) => (
            <div key={i.courseId} className="flex text-sm">
              <dt className="max-w-[60%]">
                {i.courseSlug ? <Link to={`/khoa-hoc/${i.courseSlug}`} className="hover:text-brand-700">{i.courseTitle}</Link> : i.courseTitle}
              </dt>
              <span className="dot-leader" />
              <dd>{formatPrice(i.price, '0đ')}</dd>
            </div>
          ))}
          <div className="mt-2 flex"><dt>Tạm tính</dt><span className="dot-leader" /><dd>{formatPrice(order.subTotal, '0đ')}</dd></div>
          {order.discountAmount > 0 && (
            <div className="flex text-success"><dt>Giảm ({order.couponCode})</dt><span className="dot-leader" /><dd>-{formatPrice(order.discountAmount)}</dd></div>
          )}
          <div className="mt-2 flex bg-ink px-3 py-3 text-xl font-extrabold text-white">
            <dt>Tổng cộng</dt><dd className="ml-auto">{formatPrice(order.totalAmount, '0đ')}</dd>
          </div>
        </dl>
        {order.payments.length > 0 && (
          <div className="mt-6 text-sm text-ink-soft">
            {order.payments.map((p) => (
              <p key={p.id}>
                {providerLabel[p.provider]} · {p.status === 'Success' ? 'Thành công' : p.status === 'Failed' ? 'Thất bại' : 'Đang xử lý'}
                {p.paidAt && ` · ${formatDateTime(p.paidAt)}`}
              </p>
            ))}
          </div>
        )}
      </aside>
    </div>
  )
}
