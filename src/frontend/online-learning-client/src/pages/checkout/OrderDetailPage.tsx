import { useEffect, useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { CheckCircle2, Clock, FileDown, RotateCcw } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorState, OrderStatusBadge, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { orderService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { paymentService, providerLabel, redirectToGateway, type PaymentProvider } from '../../services/paymentApi'
import { PaymentMethodPicker, TestModeNotice } from '../../components/payment/PaymentMethods'
import { refundService, refundStatusLabel, refundStatusStyle } from '../../services/revenueApi'
import { formatDateTime, formatNumber, formatPrice } from '../../utils/format'
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
  const [downloading, setDownloading] = useState(false)
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
  const hasReceipt = order.status === 'Paid' || order.status === 'Refunded'

  const downloadInvoice = async () => {
    setDownloading(true)
    try {
      await refundService.downloadInvoice(order.id, order.orderCode)
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setDownloading(false)
    }
  }

  return (
    <div className="container-page grid gap-12 py-12 md:py-16 lg:grid-cols-[1fr_440px]">
      <div className="flex flex-col gap-6">
        {paid ? (
          <CheckCircle2 className="size-16 text-success" aria-hidden="true" />
        ) : (
          <Clock className="size-16 text-warning" aria-hidden="true" />
        )}
        <h1 className="text-title font-semibold">
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
              <div className="mt-4 card p-4">
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

        {hasReceipt && order.totalAmount > 0 && <RefundSection orderId={order.id} />}

        <div className="flex flex-wrap gap-3">
          {hasReceipt && (
            <button type="button" className="btn-outline" onClick={downloadInvoice} disabled={downloading}>
              {downloading ? <Spinner className="size-4" /> : <FileDown className="size-4" aria-hidden="true" />} Tải biên nhận (PDF)
            </button>
          )}
          <Link to="/tai-khoan/don-hang" className="btn-soft">Lịch sử mua hàng</Link>
          <Link to="/khoa-hoc" className="btn-ghost">Xem thêm khóa học</Link>
        </div>
      </div>

      <aside className="paper-receipt self-start bg-surface-2 px-6 pt-6 pb-10" aria-label="Hóa đơn">
        <p className="text-center font-bold">EduViet · Hóa đơn</p>
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
          {order.pointsDiscountAmount > 0 && (
            <div className="flex text-success"><dt>Điểm thưởng ({formatNumber(order.pointsUsed)} điểm)</dt><span className="dot-leader" /><dd>-{formatPrice(order.pointsDiscountAmount)}</dd></div>
          )}
          <div className="mt-2 flex bg-ink px-3 py-3 text-xl font-bold text-white">
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

/** Yêu cầu hoàn tiền theo Chính sách hoàn tiền: hiện trạng thái các yêu cầu và form gửi nếu còn đủ điều kiện. */
function RefundSection({ orderId }: { orderId: string }) {
  const { data, error, reload } = useAsync(() => refundService.status(orderId), [orderId])
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [fieldError, setFieldError] = useState<string | null>(null)
  const [sending, setSending] = useState(false)

  // Admin xem đơn của người khác: API trả 404, không hiện phần này
  if (error || !data) return null
  const latest = data.requests[0]
  if (!data.canRequest && !latest) return null

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (reason.trim().length < 10) { setFieldError('Vui lòng nêu lý do (ít nhất 10 ký tự).'); return }
    setSending(true)
    setFieldError(null)
    try {
      await refundService.request(orderId, reason.trim())
      toast.success('Đã gửi yêu cầu hoàn tiền. EduViet sẽ phản hồi trong vòng 7 ngày làm việc.')
      setOpen(false)
      reload()
    } catch (err) {
      setFieldError(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  return (
    <section className="card max-w-xl p-5" aria-label="Hoàn tiền">
      <h2 className="flex items-center gap-2 font-bold"><RotateCcw className="size-5 text-ink-soft" aria-hidden="true" />Hoàn tiền</h2>
      {latest && (
        <div className="mt-3 text-sm">
          <p>
            Yêu cầu gửi lúc {formatDateTime(latest.createdAt)}:{' '}
            <span className={`inline-flex rounded-(--radius-control) px-2 py-0.5 font-semibold ${refundStatusStyle[latest.status]}`}>{refundStatusLabel[latest.status]}</span>
          </p>
          <p className="mt-1 text-ink-soft">Lý do: {latest.reason}</p>
          {latest.adminNote && <p className="mt-1 text-ink-soft">Phản hồi: {latest.adminNote}</p>}
        </div>
      )}
      {data.canRequest && !open && (
        <div className="mt-3 text-sm text-ink-soft">
          <p>
            Bạn có thể yêu cầu hoàn tiền {data.deadline && <>đến <b className="text-ink">{formatDateTime(data.deadline)}</b></>} nếu học không quá{' '}
            {formatNumber(data.maxProgressPercent)}% nội dung (hiện {formatNumber(data.currentProgressPercent)}%).{' '}
            <Link to="/chinh-sach/hoan-tien" className="font-semibold text-brand-700 hover:underline">Xem chính sách</Link>
          </p>
          <button type="button" className="btn-soft mt-3" onClick={() => setOpen(true)}>Yêu cầu hoàn tiền</button>
        </div>
      )}
      {data.canRequest && open && (
        <form className="mt-3 flex flex-col gap-3" onSubmit={submit} noValidate>
          <TextArea label="Lý do yêu cầu hoàn tiền" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} error={fieldError ?? undefined} />
          <p className="text-sm text-ink-soft">Khi được chấp nhận, quyền học các khóa trong đơn bị thu hồi và tiền được hoàn về phương thức thanh toán ban đầu.</p>
          <div className="flex gap-2">
            <button type="submit" className="btn-primary" disabled={sending}>{sending && <Spinner className="size-4" />}Gửi yêu cầu</button>
            <button type="button" className="btn-ghost" onClick={() => setOpen(false)}>Hủy</button>
          </div>
        </form>
      )}
      {!data.canRequest && latest?.status !== 'Pending' && data.blockedReason && latest?.status !== 'Approved' && (
        <p className="mt-2 text-sm text-ink-muted">{data.blockedReason}</p>
      )}
    </section>
  )
}
