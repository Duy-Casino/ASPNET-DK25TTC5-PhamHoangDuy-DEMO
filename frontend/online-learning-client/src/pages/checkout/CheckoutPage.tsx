import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { PageHeader, ErrorState, Skeleton, Spinner } from '../../components/ui'
import { PaymentMethodPicker } from '../../components/payment/PaymentMethods'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { useCart } from '../../hooks/useAuth'
import { orderService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { paymentService, redirectToGateway, type PaymentProvider } from '../../services/paymentApi'
import type { PriceQuote } from '../../types/api'
import { formatPrice } from '../../utils/format'

/**
 * Thanh toán: ?course=<id> (mua ngay) hoặc lấy từ giỏ hàng.
 * Giá luôn do Backend tính (/api/orders/quote); client chỉ gửi danh sách khóa học + mã giảm giá.
 */
export default function CheckoutPage() {
  useDocumentTitle('Thanh toán')
  const [params] = useSearchParams()
  const singleCourse = params.get('course')
  const { cart, loading: cartLoading, reload: reloadCart } = useCart()
  const navigate = useNavigate()

  const courseIds = singleCourse ? [singleCourse] : cart.items.map((i) => i.courseId)
  const key = courseIds.join(',')

  const [couponInput, setCouponInput] = useState('')
  const [coupon, setCoupon] = useState('')
  const [quote, setQuote] = useState<PriceQuote | null>(null)
  const [quoteError, setQuoteError] = useState<string | null>(null)
  const [couponError, setCouponError] = useState<string | null>(null)
  const [quoting, setQuoting] = useState(false)
  const [placing, setPlacing] = useState(false)
  const methods = useAsync(() => paymentService.methods(), [])
  const [method, setMethod] = useState<PaymentProvider | null>(null)

  // Mặc định chọn phương thức đầu tiên đang bật
  useEffect(() => {
    if (!method && methods.data?.length) setMethod(methods.data[0].provider)
  }, [methods.data, method])

  useEffect(() => {
    if (!key) return
    let cancelled = false
    setQuoting(true)
    orderService.quote(key.split(','), coupon || undefined)
      .then((q) => {
        if (cancelled) return
        setQuote(q)
        setQuoteError(null)
        if (coupon) setCouponError(null)
      })
      .catch((err) => {
        if (cancelled) return
        if (coupon) {
          // Mã không hợp lệ: báo lỗi ở ô mã, tính lại giá không mã
          setCouponError(getErrorMessage(err))
          setCoupon('')
        } else {
          setQuoteError(getErrorMessage(err))
        }
      })
      .finally(() => !cancelled && setQuoting(false))
    return () => {
      cancelled = true
    }
  }, [key, coupon])

  if (!singleCourse && !cartLoading && !placing && cart.count === 0) return <Navigate to="/gio-hang" replace />

  const applyCoupon = (e: FormEvent) => {
    e.preventDefault()
    setCouponError(null)
    setCoupon(couponInput.trim().toUpperCase())
  }

  const placeOrder = async () => {
    setPlacing(true)
    try {
      const order = await orderService.create(
        singleCourse ? { courseIds: [singleCourse], couponCode: coupon } : { fromCart: true, couponCode: coupon },
      )
      if (order.status === 'Paid') {
        toast.success('Đăng ký thành công! Khóa học đã được thêm vào tài khoản của bạn.')
        // Chuyển trang trước rồi mới làm mới giỏ, để trang này không tự chuyển về giỏ hàng trống
        navigate(`/don-hang/${order.id}`, { replace: true })
        void reloadCart()
        return
      }
      if (!method) {
        navigate(`/don-hang/${order.id}`, { replace: true })
        return
      }
      try {
        const session = await paymentService.checkout(order.id, method)
        redirectToGateway(session.redirectUrl)
      } catch (err) {
        // Đơn đã tạo nhưng chưa tạo được giao dịch: chuyển tới trang đơn để thanh toán lại
        toast.error(getErrorMessage(err))
        navigate(`/don-hang/${order.id}`, { replace: true })
      }
    } catch (err) {
      toast.error(getErrorMessage(err))
      setPlacing(false)
    }
  }

  return (
    <>
      <PageHeader title="Thanh toán" crumbs={singleCourse ? [{ label: 'Khóa học', to: '/khoa-hoc' }, { label: 'Thanh toán' }] : [{ label: 'Giỏ hàng', to: '/gio-hang' }, { label: 'Thanh toán' }]} />
    <div className="container-page py-10 md:py-12">
      <Link to={singleCourse ? '/khoa-hoc' : '/gio-hang'} className="mt-2 inline-block font-semibold text-brand-700 hover:underline">
        ← {singleCourse ? 'Tiếp tục xem khóa học' : 'Quay lại giỏ hàng'}
      </Link>

      {quoteError ? (
        <div className="mt-10"><ErrorState message={quoteError} /></div>
      ) : (
        <div className="mt-10 grid gap-12 lg:grid-cols-[1fr_420px]">
          <div className="flex flex-col gap-10">
            <section aria-labelledby="items-h">
              <h2 id="items-h" className="text-2xl font-semibold">Khóa học</h2>
              <ul className="mt-6 flex flex-col gap-3">
                {!quote && <Skeleton className="h-20" />}
                {quote?.lines.map((l) => (
                  <li key={l.courseId} className="flex flex-wrap items-baseline justify-between gap-3 rounded-(--radius-card) bg-surface-2 p-4">
                    <span className="font-bold">{l.title}</span>
                    <span className="tabular-nums">
                      {l.originalPrice > l.price && <span className="mr-2 text-sm text-ink-muted line-through">{formatPrice(l.originalPrice)}</span>}
                      <b>{formatPrice(l.price)}</b>
                    </span>
                  </li>
                ))}
              </ul>
            </section>

            <section aria-labelledby="coupon-h">
              <h2 id="coupon-h" className="text-2xl font-semibold">Mã giảm giá</h2>
              <form onSubmit={applyCoupon} className="mt-4 flex max-w-md gap-2">
                <label htmlFor="coupon" className="sr-only">Mã giảm giá</label>
                <input id="coupon" value={couponInput} onChange={(e) => setCouponInput(e.target.value)} placeholder="Nhập mã, ví dụ CHAOBANMOI"
                  className="input uppercase" aria-invalid={!!couponError} aria-describedby={couponError ? 'coupon-err' : undefined} maxLength={50} />
                <button type="submit" className="btn-soft shrink-0" disabled={!couponInput.trim() || quoting}>Áp dụng</button>
              </form>
              {couponError && <p id="coupon-err" className="mt-2 text-sm font-medium text-danger">{couponError}</p>}
              {quote?.couponMessage && (
                <p className="mt-2 flex items-center gap-3 text-sm font-medium text-success">
                  {quote.couponMessage}
                  <button type="button" className="text-ink-soft underline" onClick={() => { setCoupon(''); setCouponInput('') }}>Bỏ mã</button>
                </p>
              )}
            </section>

            <section aria-labelledby="method-h">
              <h2 id="method-h" className="text-2xl font-semibold">Phương thức thanh toán</h2>
              <div className="mt-4">
                {quote?.totalAmount === 0 ? (
                  <p className="bg-success-50 p-4 text-sm">Đơn hàng 0đ không cần thanh toán — khóa học được mở ngay khi bạn hoàn tất.</p>
                ) : (
                  <PaymentMethodPicker methods={methods.data} loading={methods.loading} value={method} onChange={setMethod} />
                )}
              </div>
            </section>
          </div>

          <aside className="paper-receipt self-start bg-white px-6 pt-6 pb-10 ring-0 lg:sticky lg:top-24" aria-label="Hóa đơn">
            <div className="bg-surface-2 -mx-6 -mt-6 mb-6 px-6 py-4">
              <p className="text-center font-bold">EduViet · Hóa đơn tạm tính</p>
            </div>
            {quote ? (
              <dl className="flex flex-col gap-3 tabular-nums">
                {quote.lines.map((l) => (
                  <div key={l.courseId} className="flex text-sm">
                    <dt className="line-clamp-1 max-w-[60%]">{l.title}</dt><span className="dot-leader" /><dd>{formatPrice(l.price, '0đ')}</dd>
                  </div>
                ))}
                <div className="mt-2 flex"><dt>Tạm tính</dt><span className="dot-leader" /><dd>{formatPrice(quote.subTotal, '0đ')}</dd></div>
                {quote.discountAmount > 0 && (
                  <div className="flex text-success"><dt>Giảm ({quote.couponCode})</dt><span className="dot-leader" /><dd>-{formatPrice(quote.discountAmount)}</dd></div>
                )}
                <div className="mt-2 flex bg-ink px-3 py-3 text-xl font-bold text-white">
                  <dt>Tổng cộng</dt><dd className="ml-auto">{formatPrice(quote.totalAmount, '0đ')}</dd>
                </div>
              </dl>
            ) : (
              <Skeleton className="h-40" />
            )}
            <button type="button" className="btn-primary mt-6 min-h-14 w-full text-base" disabled={!quote || quoting || placing || (quote.totalAmount > 0 && !method)} onClick={placeOrder}>
              {(placing || quoting) && <Spinner className="size-4" />}
              {quote?.totalAmount === 0 ? 'Hoàn tất đăng ký' : 'Đặt hàng và thanh toán'}
            </button>
            <p className="mt-3 text-center text-xs text-ink-muted">Giá được hệ thống tính lại khi đặt hàng.</p>
          </aside>
        </div>
      )}
    </div>
    </>
  )
}
