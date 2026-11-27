import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { CheckCircle2, Gift, Heart, Layers, ShoppingCart } from 'lucide-react'
import { toast } from 'sonner'
import { CourseGrid } from '../../components/course/CourseCard'
import { BundleCard } from '../../components/growth/Sections'
import { PageHeader as DashHeader } from '../../components/dashboard'
import { EmptyState, ErrorState, PageHeader, Skeleton, Spinner } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { useAuth, useCart } from '../../hooks/useAuth'
import { useWishlist } from '../../hooks/useWishlist'
import { getErrorMessage } from '../../services/apiClient'
import { bundleService, giftService, wishlistService } from '../../services/growthApi'
import { formatDateTime, formatPrice } from '../../utils/format'
import NotFoundPage from '../NotFoundPage'

// =================== Danh sách combo ===================
export function BundlesPage() {
  useDocumentTitle('Combo khóa học')
  const { data, loading, error, reload } = useAsync(() => bundleService.list(), [])
  return (
    <>
      <PageHeader title="Combo khóa học" crumbs={[{ label: 'Combo' }]} eyebrow="Tiết kiệm hơn khi học theo lộ trình"
        description="Mỗi combo gồm nhiều khóa học bổ trợ nhau, giá thấp hơn mua lẻ từng khóa." />
      <div className="container-page py-10 md:py-12">
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">{Array.from({ length: 3 }, (_, i) => <Skeleton key={i} className="h-64" />)}</div>
        ) : data?.length === 0 ? (
          <EmptyState title="Chưa có combo" action={<Link to="/khoa-hoc" className="btn-primary">Xem khóa học</Link>}>Các combo sẽ sớm được mở bán.</EmptyState>
        ) : (
          <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">{data?.map((b) => <BundleCard key={b.id} bundle={b} />)}</div>
        )}
      </div>
    </>
  )
}

// =================== Chi tiết combo ===================
export function BundleDetailPage() {
  const { slug = '' } = useParams()
  const { data: b, loading, error, status, reload } = useAsync(() => bundleService.get(slug), [slug])
  const { isAuthenticated } = useAuth()
  const navigate = useNavigate()
  useDocumentTitle(b ? b.title : 'Combo')

  if (status === 404) return <NotFoundPage />
  if (error) return <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>
  if (loading || !b) return <div className="container-page py-16"><Skeleton className="h-96" /></div>

  const owned = b.ownedCourseIds.length
  const buy = (gift: boolean) => {
    if (!isAuthenticated) {
      toast.info('Vui lòng đăng nhập để tiếp tục')
      navigate(`/dang-nhap?next=${encodeURIComponent(`/combo/${b.slug}`)}`)
      return
    }
    navigate(`/thanh-toan?combo=${b.id}${gift ? '&tang=1' : ''}`)
  }

  return (
    <>
      <PageHeader title={b.title} crumbs={[{ label: 'Combo', to: '/combo' }, { label: b.title }]} eyebrow={`Combo ${b.courses.length} khóa học`}
        description={b.description ?? undefined}>
        <div className="flex flex-wrap items-center gap-4 rounded-(--radius-card) bg-white p-5 shadow-(--shadow-soft)" aria-label="Mua combo">
          <div className="mr-auto">
            <p className="flex flex-wrap items-baseline gap-3">
              <span className="text-3xl font-bold">{formatPrice(b.price)}</span>
              <span className="text-ink-muted line-through">{formatPrice(b.originalTotal)}</span>
              <span className="font-bold text-accent-700">Tiết kiệm {b.savingPercent}%</span>
            </p>
            <p className="mt-1 text-sm text-ink-soft">Giá gốc nếu mua lẻ {b.courses.length} khóa: {formatPrice(b.originalTotal)}</p>
          </div>
          {owned > 0 && owned < b.courses.length && (
            <p className="w-full rounded-(--radius-control) bg-warning-50 p-3 text-sm sm:order-last">
              Bạn đã sở hữu {owned}/{b.courses.length} khóa trong combo. Combo chỉ bán trọn bộ, bạn có thể mua lẻ các khóa còn lại hoặc mua combo để tặng.
            </p>
          )}
          {owned === b.courses.length ? (
            <p className="flex items-center gap-2 font-semibold text-success"><CheckCircle2 className="size-5" aria-hidden="true" />Bạn đã sở hữu toàn bộ combo</p>
          ) : (
            <>
              <button type="button" className="btn-primary min-h-12" disabled={owned > 0} onClick={() => buy(false)}>
                <Layers className="size-4" aria-hidden="true" />Mua combo
              </button>
            </>
          )}
          <button type="button" className="btn-outline min-h-12" onClick={() => buy(true)}><Gift className="size-4" aria-hidden="true" />Mua tặng</button>
        </div>
      </PageHeader>
      <div className="container-page py-10 md:py-12">
        <h2 className="text-2xl font-semibold">Các khóa học trong combo</h2>
        <div className="mt-6"><CourseGrid courses={b.courses} columns={3} eagerCount={3} /></div>
      </div>
    </>
  )
}

// =================== Nhận quà tặng ===================
export function GiftRedeemPage() {
  const { code = '' } = useParams()
  const { isAuthenticated } = useAuth()
  const { data: g, loading, error, status, reload } = useAsync(() => giftService.get(code), [code, isAuthenticated])
  const [redeeming, setRedeeming] = useState(false)
  const navigate = useNavigate()
  useDocumentTitle('Nhận quà tặng khóa học')

  if (status === 404) return <NotFoundPage />
  if (error) return <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>
  if (loading || !g) return <div className="container-page py-16"><Skeleton className="h-80 max-w-2xl" /></div>

  const next = encodeURIComponent(`/qua-tang/${code}`)
  const redeem = async () => {
    setRedeeming(true)
    try {
      const r = await giftService.redeem(code)
      toast.success(r.enrolledCount > 0 ? `Đã thêm ${r.enrolledCount} khóa học vào tài khoản của bạn.` : 'Bạn đã sở hữu các khóa học này từ trước.')
      navigate('/hoc-vien')
    } catch (err) {
      toast.error(getErrorMessage(err))
      reload()
    } finally {
      setRedeeming(false)
    }
  }

  return (
    <div className="container-page grid gap-10 py-12 md:py-16 lg:grid-cols-[1fr_1fr]">
      <div className="flex flex-col gap-5">
        <Gift className="size-14 text-accent-600" aria-hidden="true" />
        <h1 className="text-title font-semibold">{g.senderName} tặng bạn {g.courses.length > 1 ? `${g.courses.length} khóa học` : 'một khóa học'}</h1>
        <p className="text-lg text-ink-soft">Gửi tới {g.recipientName} ({g.recipientEmailMasked})</p>
        {g.message && <blockquote className="border-l-4 border-accent-500 bg-surface-2 px-5 py-4 text-lg italic">“{g.message}”</blockquote>}
        {g.status === 'Redeemed' ? (
          <p className="flex items-center gap-2 font-semibold text-success"><CheckCircle2 className="size-5" aria-hidden="true" />Quà đã được nhận{g.redeemedAt && ` lúc ${formatDateTime(g.redeemedAt)}`}.</p>
        ) : g.canRedeem ? (
          <button type="button" className="btn-primary min-h-12 self-start text-base" onClick={redeem} disabled={redeeming}>
            {redeeming && <Spinner className="size-4" />}Nhận quà và vào học
          </button>
        ) : (
          <div className="flex flex-col gap-3">
            <p className="rounded-(--radius-control) bg-warning-50 p-4">{g.blockedReason}</p>
            {!isAuthenticated && g.status === 'Sent' && (
              <div className="flex flex-wrap gap-3">
                <Link to={`/dang-nhap?next=${next}`} className="btn-primary">Đăng nhập</Link>
                <Link to={`/dang-ky?next=${next}`} className="btn-outline">Đăng ký tài khoản</Link>
              </div>
            )}
          </div>
        )}
      </div>
      <section aria-label="Khóa học được tặng" className="flex flex-col gap-3">
        {g.courses.map((c) => (
          <Link key={c.id} to={`/khoa-hoc/${c.slug}`} className="card card-hover flex items-center gap-4 p-3">
            {c.thumbnailUrl ? <img src={c.thumbnailUrl} alt="" className="aspect-video w-32 rounded-lg object-cover" /> : <span className="aspect-video w-32 rounded-lg bg-surface-3" />}
            <span className="font-semibold">{c.title}</span>
          </Link>
        ))}
      </section>
    </div>
  )
}

// =================== Khóa học yêu thích (khu vực học viên) ===================
export function WishlistPage() {
  useDocumentTitle('Khóa học yêu thích')
  const { data, loading, error, reload, setData } = useAsync(() => wishlistService.list(), [])
  const { toggle } = useWishlist()
  const { add, has, pendingId } = useCart()

  const remove = async (id: string) => {
    await toggle(id)
    setData((data ?? []).filter((x) => x.course.id !== id))
  }

  return (
    <div className="flex flex-col gap-8">
      <DashHeader title="Khóa học yêu thích" description="Lưu lại để mua sau. Bạn sẽ thấy ngay khi khóa học có flash sale." />
      {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.length === 0 ? (
        <EmptyState title="Chưa có khóa học yêu thích" action={<Link to="/khoa-hoc" className="btn-primary">Khám phá khóa học</Link>}>
          Bấm biểu tượng <Heart className="inline size-4" aria-label="trái tim" /> trên thẻ khóa học để lưu vào đây.
        </EmptyState>
      ) : (
        <ul className="flex flex-col gap-3">
          {data?.map(({ course }) => (
            <li key={course.id} className="card flex flex-wrap items-center gap-4 p-4">
              <Link to={`/khoa-hoc/${course.slug}`} className="min-w-0 flex-1">
                <p className="font-semibold hover:text-brand-700">{course.title}</p>
                <p className="text-sm text-ink-soft">{course.instructor.fullName}</p>
              </Link>
              <p className="text-right">
                <span className="block text-lg font-bold">{formatPrice(course.effectivePrice)}</span>
                {course.effectivePrice < course.price && <span className="text-sm text-ink-muted line-through">{formatPrice(course.price)}</span>}
              </p>
              <div className="flex gap-2">
                {has(course.id) ? (
                  <Link to="/gio-hang" className="btn-soft">Trong giỏ hàng</Link>
                ) : (
                  <button type="button" className="btn-primary" disabled={pendingId === course.id} onClick={() => add(course.id, course.title)}>
                    {pendingId === course.id ? <Spinner className="size-4" /> : <ShoppingCart className="size-4" aria-hidden="true" />}Thêm vào giỏ
                  </button>
                )}
                <button type="button" className="btn-ghost text-danger" onClick={() => remove(course.id)} aria-label={`Bỏ ${course.title} khỏi yêu thích`}>Bỏ</button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
