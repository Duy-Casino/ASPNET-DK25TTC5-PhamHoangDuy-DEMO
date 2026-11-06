import { Link, useNavigate } from 'react-router-dom'
import { Trash2 } from 'lucide-react'
import { ResponsiveImg, PageHeader, EmptyState, Skeleton, Spinner } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { useCart } from '../../hooks/useAuth'
import { formatDuration, formatPrice } from '../../utils/format'

export default function CartPage() {
  useDocumentTitle('Giỏ hàng')
  const { cart, loading, remove, pendingId } = useCart()
  const navigate = useNavigate()
  const saving = cart.originalTotal - cart.subTotal

  return (
    <>
      <PageHeader title="Giỏ hàng" crumbs={[{ label: 'Giỏ hàng' }]} description={`${cart.count} khóa học`} />
    <div className="container-page py-10 md:py-12">

      {loading && cart.count === 0 ? (
        <div className="mt-10 flex flex-col gap-4"><Skeleton className="h-28" /><Skeleton className="h-28" /></div>
      ) : cart.count === 0 ? (
        <div className="mt-10">
          <EmptyState title="Giỏ hàng đang trống" action={<Link to="/khoa-hoc" className="btn-primary">Khám phá khóa học</Link>}>
            Thêm khóa học bạn quan tâm vào giỏ để thanh toán một lần.
          </EmptyState>
        </div>
      ) : (
        <div className="mt-10 grid gap-12 lg:grid-cols-[1fr_380px]">
          <ul className="flex flex-col gap-4">
            {cart.items.map(({ course }) => (
              <li key={course.id} className="grid grid-cols-[120px_1fr] gap-4 rounded-(--radius-card) bg-surface-2 p-3 sm:grid-cols-[200px_1fr_auto] sm:gap-6 sm:p-4">
                <Link to={`/khoa-hoc/${course.slug}`}>
                  <ResponsiveImg url={course.thumbnailUrl} sizes="(min-width: 640px) 200px, 120px" maxWidth={480} width={400} height={225} className="aspect-video w-full rounded-lg object-cover" />
                </Link>
                <div className="min-w-0">
                  <Link to={`/khoa-hoc/${course.slug}`} className="line-clamp-2 text-lg font-bold hover:text-brand-700">{course.title}</Link>
                  <p className="mt-1 text-sm text-ink-soft">{course.instructor.fullName}</p>
                  <p className="mt-1 text-sm text-ink-muted">{course.totalLessons} bài · {formatDuration(course.totalDurationSeconds)}</p>
                </div>
                <div className="col-span-2 flex items-center justify-between gap-4 sm:col-span-1 sm:flex-col sm:items-end sm:justify-start">
                  <div className="text-right">
                    <p className="text-xl font-bold">{formatPrice(course.effectivePrice)}</p>
                    {course.effectivePrice < course.price && <p className="text-sm text-ink-muted line-through">{formatPrice(course.price)}</p>}
                  </div>
                  <button type="button" className="btn-ghost px-3 text-danger" disabled={pendingId === course.id}
                    onClick={() => remove(course.id)} aria-label={`Xóa ${course.title} khỏi giỏ`}>
                    {pendingId === course.id ? <Spinner className="size-4" /> : <Trash2 className="size-4" aria-hidden="true" />} Xóa
                  </button>
                </div>
              </li>
            ))}
          </ul>

          <aside className="paper-receipt self-start bg-brand-50 px-6 pt-6 pb-10 lg:sticky lg:top-24" aria-label="Tổng tiền">
            <h2 className="text-xl font-semibold">Tóm tắt</h2>
            <dl className="mt-6 flex flex-col gap-3 tabular-nums">
              <div className="flex"><dt>Giá gốc</dt><span className="dot-leader" /><dd>{formatPrice(cart.originalTotal, '0đ')}</dd></div>
              {saving > 0 && <div className="flex text-success"><dt>Khuyến mãi</dt><span className="dot-leader" /><dd>-{formatPrice(saving)}</dd></div>}
              <div className="mt-2 flex bg-ink px-3 py-2 text-lg font-bold text-white"><dt>Tạm tính</dt><dd className="ml-auto">{formatPrice(cart.subTotal, '0đ')}</dd></div>
            </dl>
            <p className="mt-4 text-sm text-ink-soft">Mã giảm giá được áp dụng ở bước thanh toán.</p>
            <button type="button" className="btn-primary mt-6 min-h-12 w-full text-base" onClick={() => navigate('/thanh-toan')}>Thanh toán</button>
          </aside>
        </div>
      )}
    </div>
    </>
  )
}
