import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { toast } from 'sonner'
import { PageHeader, ErrorState, Field, Skeleton, Spinner, TextArea } from '../../components/ui'
import { PaymentMethodPicker } from '../../components/payment/PaymentMethods'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { useCart } from '../../hooks/useAuth'
import { orderService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import { paymentService, redirectToGateway, type PaymentProvider } from '../../services/paymentApi'
import type { PriceQuote } from '../../types/api'
import { formatNumber, formatPrice } from '../../utils/format'

/**
 * Thanh toán: ?course=<id> (mua ngay) hoặc lấy từ giỏ hàng.
 * Giá luôn do Backend tính (/api/orders/quote); client chỉ gửi danh sách khóa học, mã giảm giá
 * và lựa chọn dùng điểm thưởng (số điểm). Backend tự kiểm tra số dư và mức tối đa.
 */
export default function CheckoutPage() {
  useDocumentTitle('Thanh toán')
  const [params] = useSearchParams()
  const singleCourse = params.get('course')
  const bundleId = params.get('combo')
  const { cart, loading: cartLoading, reload: reloadCart } = useCart()
  const navigate = useNavigate()

  const courseIds = bundleId ? [] : singleCourse ? [singleCourse] : cart.items.map((i) => i.courseId)
  const key = bundleId ? `combo:${bundleId}` : courseIds.join(',')

  // Mua tặng: người nhận nhận email có liên kết nhận quà
  const [isGift, setIsGift] = useState(params.get('tang') === '1')
  const [gift, setGift] = useState({ recipientEmail: '', recipientName: '', message: '' })
  const [giftErrors, setGiftErrors] = useState<Record<string, string>>({})

  const [couponInput, setCouponInput] = useState('')
  const [coupon, setCoupon] = useState('')
  const [quote, setQuote] = useState<PriceQuote | null>(null)
  const [quoteError, setQuoteError] = useState<string | null>(null)
  const [couponError, setCouponError] = useState<string | null>(null)
  const [quoting, setQuoting] = useState(false)
  // Điểm thưởng: null = không dùng; 'max' = dùng tối đa; số = dùng đúng số điểm
  const [points, setPoints] = useState<number | 'max' | null>(null)
  const [pointsInput, setPointsInput] = useState('')
  const [pointsError, setPointsError] = useState<string | null>(null)
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
    orderService.quote(courseIds, coupon || undefined, points, { bundleId: bundleId ?? undefined, isGift })
      .then((q) => {
        if (cancelled) return
        setQuote(q)
        setQuoteError(null)
        if (coupon) setCouponError(null)
        if (points !== null) {
          setPointsError(null)
          setPointsInput(String(q.pointsUsed))
        }
      })
      .catch((err) => {
        if (cancelled) return
        if (typeof points === 'number') {
          // Số điểm vượt mức cho phép: báo lỗi ở ô điểm, tính lại với mức tối đa
          setPointsError(getErrorMessage(err))
          setPoints('max')
        } else if (points !== null && !coupon) {
          setPointsError(getErrorMessage(err))
          setPoints(null)
        } else if (coupon) {
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
  // eslint-disable-next-line react-hooks/exhaustive-deps -- courseIds được mã hóa trong key
  }, [key, coupon, points, isGift])

  if (!singleCourse && !bundleId && !cartLoading && !placing && cart.count === 0) return <Navigate to="/gio-hang" replace />

  const applyCoupon = (e: FormEvent) => {
    e.preventDefault()
    setCouponError(null)
    setCoupon(couponInput.trim().toUpperCase())
    // Giá sau mã thay đổi thì mức điểm tối đa cũng đổi: tính lại theo mức tối đa mới
    setPoints((p) => (p === null ? null : 'max'))
  }

  const togglePoints = (on: boolean) => {
    setPointsError(null)
    setPoints(on ? 'max' : null)
    if (!on) setPointsInput('')
  }

  const applyPoints = (e: FormEvent) => {
    e.preventDefault()
    const n = Number(pointsInput)
    if (!Number.isInteger(n) || n < 1) {
      setPointsError('Nhập số điểm là số nguyên lớn hơn 0.')
      return
    }
    setPointsError(null)
    setPoints(n)
  }

  const validateGift = () => {
    const e: Record<string, string> = {}
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(gift.recipientEmail.trim())) e.recipientEmail = 'Nhập email người nhận hợp lệ.'
    if (!gift.recipientName.trim()) e.recipientName = 'Nhập tên người nhận.'
    setGiftErrors(e)
    return Object.keys(e).length === 0
  }

  const placeOrder = async () => {
    if (isGift && !validateGift()) {
      document.getElementById('gift-h')?.scrollIntoView({ behavior: 'smooth', block: 'center' })
      return
    }
    setPlacing(true)
    const giftInput = isGift ? { recipientEmail: gift.recipientEmail.trim(), recipientName: gift.recipientName.trim(), message: gift.message.trim() || null } : null
    try {
      const order = await orderService.create(
        bundleId
          ? { bundleId, couponCode: coupon, points: quote?.pointsUsed || null, gift: giftInput }
          : singleCourse
            ? { courseIds: [singleCourse], couponCode: coupon, points: quote?.pointsUsed || null, gift: giftInput }
            : { fromCart: true, couponCode: coupon, points: quote?.pointsUsed || null, gift: giftInput },
      )
      if (order.status === 'Paid') {
        toast.success(order.gift ? `Đã gửi quà tặng tới ${order.gift.recipientEmail}.` : 'Đăng ký thành công! Khóa học đã được thêm vào tài khoản của bạn.')
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
      <PageHeader title={isGift ? 'Thanh toán quà tặng' : 'Thanh toán'} crumbs={bundleId ? [{ label: 'Combo', to: '/combo' }, { label: 'Thanh toán' }] : singleCourse ? [{ label: 'Khóa học', to: '/khoa-hoc' }, { label: 'Thanh toán' }] : [{ label: 'Giỏ hàng', to: '/gio-hang' }, { label: 'Thanh toán' }]} />
    <div className="container-page py-10 md:py-12">
      <Link to={bundleId ? '/combo' : singleCourse ? '/khoa-hoc' : '/gio-hang'} className="mt-2 inline-block font-semibold text-brand-700 hover:underline">
        ← {bundleId ? 'Xem các combo khác' : singleCourse ? 'Tiếp tục xem khóa học' : 'Quay lại giỏ hàng'}
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

            <section aria-labelledby="gift-h" className="max-w-xl">
              <h2 id="gift-h" className="text-2xl font-semibold">Mua tặng</h2>
              <label className="mt-4 flex cursor-pointer items-start gap-3">
                <input type="checkbox" className="mt-1 size-5 accent-brand-600" checked={isGift} onChange={(e) => { setIsGift(e.target.checked); setGiftErrors({}) }} />
                <span>
                  <span className="block font-semibold">Mua tặng người khác</span>
                  <span className="block text-sm text-ink-soft">Người nhận nhận email có liên kết nhận quà, đăng nhập bằng đúng email đó để vào học. Bạn không được ghi danh vào các khóa này.</span>
                </span>
              </label>
              {isGift && (
                <div className="mt-4 grid gap-4 rounded-(--radius-card) bg-surface-2 p-4 sm:grid-cols-2">
                  <Field label="Email người nhận" type="email" value={gift.recipientEmail} onChange={(e) => setGift({ ...gift, recipientEmail: e.target.value })} error={giftErrors.recipientEmail} maxLength={256} />
                  <Field label="Tên người nhận" value={gift.recipientName} onChange={(e) => setGift({ ...gift, recipientName: e.target.value })} error={giftErrors.recipientName} maxLength={100} />
                  <div className="sm:col-span-2">
                    <TextArea label="Lời nhắn (không bắt buộc)" rows={2} maxLength={500} value={gift.message} onChange={(e) => setGift({ ...gift, message: e.target.value })} />
                  </div>
                </div>
              )}
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
                  <button type="button" className="text-ink-soft underline" onClick={() => { setCoupon(''); setCouponInput(''); setPoints((p) => (p === null ? null : 'max')) }}>Bỏ mã</button>
                </p>
              )}
            </section>

            <section aria-labelledby="points-h">
              <h2 id="points-h" className="text-2xl font-semibold">Điểm thưởng</h2>
              {!quote ? (
                <Skeleton className="mt-4 h-16 max-w-md" />
              ) : !quote.points.enabled ? (
                <p className="mt-4 text-sm text-ink-soft">Tính năng dùng điểm khi thanh toán đang tạm dừng.</p>
              ) : (
                <div className="mt-4 max-w-md rounded-(--radius-card) bg-surface-2 p-4">
                  <label className="flex cursor-pointer items-start gap-3">
                    <input type="checkbox" className="peer sr-only" checked={points !== null}
                      disabled={quote.points.maxUsable === 0 && points === null} onChange={(e) => togglePoints(e.target.checked)} />
                    <span className="relative mt-0.5 h-6 w-11 shrink-0 rounded-full bg-surface-3 transition-colors peer-checked:bg-brand-600 peer-focus-visible:ring-4 peer-focus-visible:ring-brand-100 peer-disabled:opacity-50 after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:transition-transform peer-checked:after:translate-x-5" aria-hidden="true" />
                    <span>
                      <span className="block font-semibold">Dùng điểm thưởng để giảm giá</span>
                      <span className="block text-sm text-ink-soft">
                        Bạn có <b className="tabular-nums">{formatNumber(quote.points.balance)}</b> điểm · {formatNumber(quote.points.pointsPerUnit)} điểm = {formatPrice(quote.points.amountPerUnit)} · tối đa {quote.points.maxPercent}% giá trị đơn
                      </span>
                    </span>
                  </label>
                  {quote.points.maxUsable === 0 && points === null && (
                    <p className="mt-3 text-sm text-ink-soft">
                      {quote.points.balance === 0 ? 'Bạn chưa có điểm thưởng. ' : 'Đơn hàng này chưa dùng được điểm. '}
                      <Link to="/hoc-vien/diem-thuong" className="font-semibold text-brand-700 hover:underline">Cách nhận điểm</Link>
                    </p>
                  )}
                  {points !== null && (
                    <form onSubmit={applyPoints} className="mt-4 flex flex-col gap-2" noValidate>
                      <label htmlFor="points" className="text-sm font-semibold">Số điểm sử dụng (tối đa {formatNumber(quote.points.maxUsable)})</label>
                      <div className="flex gap-2">
                        <input id="points" type="number" inputMode="numeric" min={1} max={quote.points.maxUsable} step={1} value={pointsInput}
                          onChange={(e) => setPointsInput(e.target.value)} className="input tabular-nums"
                          aria-invalid={!!pointsError} aria-describedby={pointsError ? 'points-err' : undefined} />
                        <button type="submit" className="btn-soft shrink-0" disabled={quoting || !pointsInput}>Áp dụng</button>
                      </div>
                      <button type="button" className="self-start text-sm font-semibold text-brand-700 hover:underline disabled:opacity-50"
                        disabled={quoting || quote.pointsUsed === quote.points.maxUsable} onClick={() => { setPointsError(null); setPoints('max') }}>
                        Dùng tối đa {formatNumber(quote.points.maxUsable)} điểm
                      </button>
                    </form>
                  )}
                  {pointsError && <p id="points-err" className="mt-2 text-sm font-medium text-danger">{pointsError}</p>}
                  {quote.pointsUsed > 0 && (
                    <p className="mt-2 text-sm font-medium text-success">
                      Dùng {formatNumber(quote.pointsUsed)} điểm, giảm {formatPrice(quote.pointsDiscountAmount)}. Điểm được hoàn lại nếu đơn bị hủy hoặc hết hạn.
                    </p>
                  )}
                </div>
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
                {quote.pointsDiscountAmount > 0 && (
                  <div className="flex text-success"><dt>Điểm thưởng ({formatNumber(quote.pointsUsed)} điểm)</dt><span className="dot-leader" /><dd>-{formatPrice(quote.pointsDiscountAmount)}</dd></div>
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
            <p className="mt-2 text-center text-xs text-ink-muted">
              Khi đặt hàng, bạn đồng ý với <Link to="/chinh-sach/dieu-khoan" className="font-semibold text-brand-700 hover:underline">Điều khoản sử dụng</Link> và{' '}
              <Link to="/chinh-sach/hoan-tien" className="font-semibold text-brand-700 hover:underline">Chính sách hoàn tiền</Link>.
            </p>
          </aside>
        </div>
      )}
    </div>
    </>
  )
}
