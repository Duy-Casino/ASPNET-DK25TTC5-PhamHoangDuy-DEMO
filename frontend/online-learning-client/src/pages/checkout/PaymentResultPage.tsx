import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { CheckCircle2, Clock, XCircle } from 'lucide-react'
import { TestModeNotice } from '../../components/payment/PaymentMethods'
import { ErrorState, Spinner } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { useCart } from '../../hooks/useAuth'
import { orderService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { paymentService, providerLabel, type PaymentProvider, type PaymentResult } from '../../services/paymentApi'
import { formatPrice } from '../../utils/format'

/** Nhận biết cổng qua tên tham số mà cổng nối vào ReturnUrl. */
function detectProvider(params: URLSearchParams): PaymentProvider | null {
  if (params.has('vnp_TxnRef')) return 'VNPay'
  if (params.has('partnerCode') && params.has('orderId')) return 'MoMo'
  if (params.has('ref') && params.has('transId')) return 'Test'
  return null
}

/**
 * Trang người dùng quay về sau khi thanh toán. Trang này KHÔNG cấp quyền học:
 * nó gửi tham số cho Backend kiểm tra chữ ký rồi đọc trạng thái đơn — quyền học đã được cấp (hoặc chưa) qua IPN.
 * Nếu cổng báo thành công nhưng IPN chưa tới, trang hỏi lại trạng thái vài lần.
 */
export default function PaymentResultPage() {
  useDocumentTitle('Kết quả thanh toán')
  const [params] = useSearchParams()
  const provider = detectProvider(params)
  const { reload: reloadCart } = useCart()
  const [result, setResult] = useState<PaymentResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [waiting, setWaiting] = useState(false)

  useEffect(() => {
    if (!provider) {
      setError('Không nhận được thông tin giao dịch từ cổng thanh toán.')
      return
    }
    let cancelled = false
    ;(async () => {
      try {
        const r = await paymentService.returnResult(provider, params.toString())
        if (cancelled) return
        setResult(r)
        // Chờ IPN tối đa ~15 giây nếu cổng báo thành công nhưng đơn chưa Paid
        if (r.gatewayReportedSuccess && r.orderStatus !== 'Paid' && r.orderId) {
          setWaiting(true)
          for (let i = 0; i < 8 && !cancelled; i++) {
            await new Promise((res) => setTimeout(res, 2000))
            const order = await orderService.get(r.orderId)
            if (order.status === 'Paid') {
              if (!cancelled) setResult({ ...r, orderStatus: 'Paid', message: 'Thanh toán thành công.' })
              break
            }
          }
          if (!cancelled) setWaiting(false)
        }
        void reloadCart()
      } catch (e) {
        if (!cancelled) setError(getErrorMessage(e))
      }
    })()
    return () => {
      cancelled = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  if (error) return <div className="container-page py-24"><ErrorState message={error} /></div>
  if (!result) {
    return (
      <div className="container-page flex min-h-[50vh] items-center gap-3 py-24 text-lg" role="status">
        <Spinner className="size-6 text-brand-600" /> Đang xác nhận giao dịch…
      </div>
    )
  }

  const paid = result.orderStatus === 'Paid'
  const pending = !paid && result.gatewayReportedSuccess

  return (
    <div className="container-page max-w-3xl py-16">
      {paid ? <CheckCircle2 className="size-16 text-success" aria-hidden="true" />
        : pending ? <Clock className="size-16 text-warning" aria-hidden="true" />
        : <XCircle className="size-16 text-danger" aria-hidden="true" />}
      <h1 className="mt-6 text-title font-semibold">
        {paid ? 'Thanh toán thành công' : pending ? 'Đang chờ xác nhận thanh toán' : 'Thanh toán chưa thành công'}
      </h1>
      <p className="mt-4 text-lg text-ink-soft" aria-live="polite">
        {waiting ? 'Cổng thanh toán đã báo thành công, hệ thống đang chờ xác nhận chính thức…' : result.message}
      </p>

      <dl className="mt-8 grid gap-4 rounded-(--radius-card) bg-surface-2 p-6 sm:grid-cols-3">
        <div><dt className="text-sm text-ink-soft">Mã đơn</dt><dd className="font-bold">{result.orderCode}</dd></div>
        <div><dt className="text-sm text-ink-soft">Phương thức</dt><dd className="font-bold">{result.provider ? providerLabel[result.provider] : '—'}</dd></div>
        <div><dt className="text-sm text-ink-soft">Số tiền</dt><dd className="font-bold tabular-nums">{formatPrice(result.amount, '0đ')}</dd></div>
      </dl>

      {result.isTestMode && <TestModeNotice className="mt-6" />}

      <div className="mt-8 flex flex-wrap gap-3">
        {paid && <Link to="/hoc-vien" className="btn-primary min-h-12 px-6 text-base">Vào học ngay</Link>}
        {result.orderId && <Link to={`/don-hang/${result.orderId}`} className={paid ? 'btn-soft min-h-12' : 'btn-primary min-h-12'}>{paid ? 'Xem hóa đơn' : 'Thử thanh toán lại'}</Link>}
        <Link to="/khoa-hoc" className="btn-ghost min-h-12">Xem thêm khóa học</Link>
      </div>
    </div>
  )
}
