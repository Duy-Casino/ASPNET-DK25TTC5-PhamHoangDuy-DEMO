import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import DOMPurify from 'dompurify'
import { Check, ChevronDown, FileText, PlayCircle, Star, X } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, ErrorState, Pagination, RatingStars, Skeleton, Spinner, TextArea } from '../components/ui'
import { useAsync, useDocumentTitle } from '../hooks/useAsync'
import { useAuth, useCart } from '../hooks/useAuth'
import { useReveal } from '../hooks/useReveal'
import { catalogService, reviewService } from '../services/api'
import { getErrorMessage } from '../services/apiClient'
import type { CourseDetail } from '../types/api'
import { courseLevelLabel } from '../types/common'
import { discountPercent, formatClock, formatDate, formatDuration, formatNumber, formatPrice } from '../utils/format'
import NotFoundPage from './NotFoundPage'

export default function CourseDetailPage() {
  const { slug = '' } = useParams()
  const { data: course, loading, error, status, reload } = useAsync(() => catalogService.detail(slug), [slug])
  const [preview, setPreview] = useState<{ title: string; url: string } | null>(null)
  const ref = useReveal<HTMLDivElement>()
  useDocumentTitle(course?.title)

  if (status === 404) return <NotFoundPage />
  if (error) return <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>
  if (loading || !course) return <DetailSkeleton />

  return (
    <div ref={ref} className="relative">
      {/* Thẻ mua hàng (desktop): lớp phủ cùng lưới, dính theo khi cuộn suốt trang */}
      <div className="pointer-events-none absolute inset-0 z-10 hidden lg:block">
        <div className="container-page grid h-full grid-cols-[1fr_380px] gap-16">
          <div />
          <div className="pt-12">
            <div className="pointer-events-auto sticky top-24">
              <PurchaseCard course={course} onPreview={setPreview} />
            </div>
          </div>
        </div>
      </div>

      <section className="bg-brand-700 text-white" aria-labelledby="course-title">
        <div className="container-page grid gap-10 py-12 md:py-16 lg:grid-cols-[1fr_380px] lg:gap-16">
          <div className="flex flex-col gap-5">
            <nav aria-label="Đường dẫn" className="text-sm text-brand-100">
              <Link to="/khoa-hoc" className="hover:text-white">Khóa học</Link>
              <span aria-hidden="true"> / </span>
              <Link to={`/khoa-hoc?category=${course.category.slug}`} className="hover:text-white">{course.category.name}</Link>
            </nav>
            <h1 id="course-title" className="text-[clamp(2rem,1.3rem+2.6vw,3.25rem)] leading-[1.05] font-extrabold tracking-[-0.025em]">{course.title}</h1>
            <p className="max-w-2xl text-lg text-brand-50">{course.shortDescription}</p>
            <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-brand-50">
              {course.reviewCount > 0 ? <RatingStars value={course.averageRating} count={course.reviewCount} light /> : <span>Chưa có đánh giá</span>}
              <span>{formatNumber(course.enrollmentCount)} học viên</span>
              <span>{courseLevelLabel[course.level]}</span>
              {course.updatedAt && <span>Cập nhật {formatDate(course.updatedAt)}</span>}
            </div>
            <Link to={`/giang-vien/${course.instructor.id}`} className="flex w-fit items-center gap-3 rounded-(--radius-control) pr-4 hover:bg-brand-800">
              <Avatar name={course.instructor.fullName} src={course.instructor.avatarUrl} size={44} />
              <span>
                <span className="block text-sm text-brand-100">Giảng viên</span>
                <span className="font-bold">{course.instructor.fullName}</span>
              </span>
            </Link>
          </div>
          <div className="lg:hidden">
            <PurchaseCard course={course} onPreview={setPreview} />
          </div>
        </div>
      </section>

      <div className="container-page grid gap-16 py-16 lg:grid-cols-[1fr_380px]">
        <div className="flex min-w-0 flex-col gap-16">
          {course.outcomes.length > 0 && (
            <section aria-labelledby="outcomes" className="bg-brand-50 p-6 sm:p-10" data-reveal>
              <h2 id="outcomes" className="text-2xl font-extrabold">Bạn sẽ học được</h2>
              <ul className="mt-6 grid gap-4 sm:grid-cols-2">
                {course.outcomes.map((o) => (
                  <li key={o} className="flex gap-3"><Check className="mt-1 size-5 shrink-0 text-brand-600" aria-hidden="true" />{o}</li>
                ))}
              </ul>
            </section>
          )}

          <Curriculum course={course} onPreview={setPreview} />

          {course.requirements.length > 0 && (
            <section aria-labelledby="req" data-reveal>
              <h2 id="req" className="text-2xl font-extrabold">Yêu cầu</h2>
              <ul className="mt-4 list-disc space-y-2 pl-6">
                {course.requirements.map((r) => <li key={r}>{r}</li>)}
              </ul>
            </section>
          )}

          {course.description && (
            <section aria-labelledby="desc" data-reveal>
              <h2 id="desc" className="text-2xl font-extrabold">Mô tả khóa học</h2>
              <div
                className="mt-4 max-w-[70ch] space-y-4 text-lg leading-relaxed [&_a]:text-brand-700 [&_a]:underline [&_li]:ml-6 [&_ul]:list-disc"
                dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(course.description) }}
              />
            </section>
          )}

          <section aria-labelledby="ins" className="bg-surface-2 p-6 sm:p-10" data-reveal>
            <h2 id="ins" className="text-2xl font-extrabold">Giảng viên</h2>
            <Link to={`/giang-vien/${course.instructor.id}`} className="mt-6 flex items-center gap-4 hover:text-brand-700">
              <Avatar name={course.instructor.fullName} src={course.instructor.avatarUrl} size={72} />
              <span>
                <span className="block text-xl font-extrabold">{course.instructor.fullName}</span>
                <span className="text-ink-soft">{course.instructor.headline}</span>
              </span>
            </Link>
            <p className="mt-4 text-sm text-ink-soft">
              {course.instructor.totalCourses} khóa học · {formatNumber(course.instructor.totalStudents)} học viên · {course.instructor.totalReviews} đánh giá
            </p>
            {course.instructor.bio && <p className="mt-4 max-w-[70ch]">{course.instructor.bio}</p>}
          </section>

          <Reviews course={course} onChanged={reload} />
        </div>
        <div className="hidden lg:block" />
      </div>

      {preview && <PreviewDialog title={preview.title} url={preview.url} onClose={() => setPreview(null)} />}
    </div>
  )
}

function PurchaseCard({ course, onPreview }: { course: CourseDetail; onPreview: (p: { title: string; url: string }) => void }) {
  const { isAuthenticated } = useAuth()
  const { add, has, pendingId } = useCart()
  const navigate = useNavigate()
  const inCart = course.isInCart || has(course.id)
  const off = discountPercent(course.price, course.discountPrice)
  const busy = pendingId === course.id

  const requireLogin = () => {
    toast.info('Vui lòng đăng nhập để tiếp tục')
    navigate(`/dang-nhap?next=${encodeURIComponent(`/khoa-hoc/${course.slug}`)}`)
  }

  return (
    <aside className="bg-white text-ink lg:shadow-[0_12px_40px_rgb(11_27_51/0.14)]" aria-label="Mua khóa học">
      <button
        type="button"
        disabled={!course.previewVideoUrl}
        onClick={() => course.previewVideoUrl && onPreview({ title: course.title, url: course.previewVideoUrl })}
        className="group relative block aspect-video w-full overflow-hidden bg-surface-3"
      >
        {course.thumbnailUrl && <img src={course.thumbnailUrl} alt="" width={800} height={450} className="size-full object-cover" />}
        {course.previewVideoUrl && (
          <span className="absolute inset-0 flex items-center justify-center bg-ink/30 transition-colors group-hover:bg-ink/45">
            <span className="flex items-center gap-2 rounded-(--radius-control) bg-white px-4 py-2 font-semibold transition-transform group-active:scale-95">
              <PlayCircle className="size-5 text-brand-600" aria-hidden="true" /> Xem giới thiệu
            </span>
          </span>
        )}
      </button>
      <div className="flex flex-col gap-4 p-6">
        <div className="flex flex-wrap items-baseline gap-3">
          <span className="text-3xl font-extrabold">{formatPrice(course.effectivePrice)}</span>
          {off !== null && (
            <>
              <span className="text-ink-muted line-through">{formatPrice(course.price)}</span>
              <span className="font-bold text-accent-700">Giảm {off}%</span>
            </>
          )}
        </div>

        {course.isOwner ? (
          <div className="flex flex-col gap-2">
            <p className="bg-brand-50 p-4 text-sm">Đây là khóa học của bạn.</p>
            <Link to={`/giang-day/khoa-hoc/${course.id}`} className="btn-primary min-h-12 w-full text-base">Soạn nội dung</Link>
          </div>
        ) : course.isEnrolled ? (
          <div className="flex flex-col gap-2">
            <p className="bg-success-50 p-4 font-semibold text-success">Bạn đã sở hữu khóa học này</p>
            <Link to={`/hoc/${course.slug}`} className="btn-primary min-h-12 w-full text-base">Vào học</Link>
          </div>
        ) : (
          <>
            {inCart ? (
              <Link to="/gio-hang" className="btn-soft min-h-12 w-full text-base">Đã có trong giỏ — xem giỏ hàng</Link>
            ) : (
              <button
                type="button"
                className="btn-accent min-h-12 w-full text-base"
                disabled={busy}
                onClick={() => (isAuthenticated ? add(course.id, course.title) : requireLogin())}
              >
                {busy && <Spinner className="size-4" />} Thêm vào giỏ hàng
              </button>
            )}
            <button
              type="button"
              className="btn-primary min-h-12 w-full text-base"
              onClick={() => (isAuthenticated ? navigate(`/thanh-toan?course=${course.id}`) : requireLogin())}
            >
              {course.effectivePrice === 0 ? 'Đăng ký học miễn phí' : 'Mua ngay'}
            </button>
          </>
        )}

        <ul className="flex flex-col gap-2 text-sm text-ink-soft">
          <li>{course.totalLessons} bài học · {formatDuration(course.totalDurationSeconds)} video</li>
          {course.quizCount > 0 && <li>{course.quizCount} bài kiểm tra</li>}
          <li>Học trên mọi thiết bị, không giới hạn thời gian</li>
          <li>Chứng chỉ có mã xác minh khi hoàn thành</li>
        </ul>
      </div>
    </aside>
  )
}

function Curriculum({ course, onPreview }: { course: CourseDetail; onPreview: (p: { title: string; url: string }) => void }) {
  const [openAll, setOpenAll] = useState(false)
  return (
    <section aria-labelledby="curriculum" data-reveal>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h2 id="curriculum" className="text-2xl font-extrabold">Nội dung khóa học</h2>
          <p className="mt-1 text-ink-soft">
            {course.sections.length} chương · {course.totalLessons} bài · {formatDuration(course.totalDurationSeconds)}
          </p>
        </div>
        <button type="button" className="btn-ghost px-3 text-brand-700" onClick={() => setOpenAll((o) => !o)}>
          {openAll ? 'Thu gọn tất cả' : 'Mở tất cả'}
        </button>
      </div>
      <div className="mt-6 flex flex-col gap-2">
        {course.sections.map((s, i) => (
          <details key={`${s.id}-${openAll}`} open={openAll || i === 0} className="group bg-surface-2">
            <summary className="flex cursor-pointer list-none items-center gap-3 p-4 font-bold hover:bg-surface-3 [&::-webkit-details-marker]:hidden">
              <ChevronDown className="size-5 shrink-0 transition-transform duration-200 group-open:rotate-180" aria-hidden="true" />
              <span className="flex-1">{s.title}</span>
              <span className="text-sm font-normal text-ink-soft">{s.lessonCount} bài · {formatDuration(s.durationSeconds)}</span>
            </summary>
            <ul className="bg-white">
              {s.lessons.map((l) => (
                <li key={l.id} className="flex items-center gap-3 px-4 py-3 pl-12">
                  {l.type === 'Video' ? <PlayCircle className="size-4 shrink-0 text-ink-muted" aria-hidden="true" /> : <FileText className="size-4 shrink-0 text-ink-muted" aria-hidden="true" />}
                  {l.isPreview && l.previewVideoUrl ? (
                    <button type="button" className="flex-1 text-left font-medium text-brand-700 underline-offset-4 hover:underline"
                      onClick={() => onPreview({ title: l.title, url: l.previewVideoUrl! })}>
                      {l.title} <span className="ml-2 rounded-(--radius-control) bg-brand-50 px-2 py-0.5 text-xs font-semibold no-underline">Học thử</span>
                    </button>
                  ) : (
                    <span className="flex-1">{l.title}</span>
                  )}
                  <span className="text-sm text-ink-muted tabular-nums">{formatClock(l.durationSeconds)}</span>
                </li>
              ))}
            </ul>
          </details>
        ))}
      </div>
    </section>
  )
}

function Reviews({ course, onChanged }: { course: CourseDetail; onChanged: () => void }) {
  const [page, setPage] = useState(1)
  const reviews = useAsync(() => catalogService.reviews(course.id, page), [course.id, page])
  const total = Object.values(course.ratingDistribution).reduce((a, b) => a + b, 0)

  return (
    <section aria-labelledby="reviews" data-reveal>
      <h2 id="reviews" className="text-2xl font-extrabold">Đánh giá của học viên</h2>
      <div className="mt-6 grid gap-8 sm:grid-cols-[auto_1fr] sm:items-center">
        <div className="text-center">
          <p className="text-6xl font-extrabold tabular-nums">{course.averageRating.toFixed(1)}</p>
          <RatingStars value={course.averageRating} showValue={false} />
          <p className="mt-1 text-sm text-ink-soft">{course.reviewCount} đánh giá</p>
        </div>
        <ul className="flex flex-col gap-2">
          {[5, 4, 3, 2, 1].map((star) => {
            const count = course.ratingDistribution[String(star)] ?? 0
            const pct = total ? Math.round((count / total) * 100) : 0
            return (
              <li key={star} className="grid grid-cols-[3rem_1fr_3rem] items-center gap-3 text-sm">
                <span>{star} sao</span>
                <span className="h-2.5 overflow-hidden rounded-full bg-surface-3" aria-hidden="true">
                  <span className="block h-full rounded-full bg-accent-500" style={{ width: `${pct}%` }} />
                </span>
                <span className="text-right text-ink-soft tabular-nums">{pct}%</span>
              </li>
            )
          })}
        </ul>
      </div>

      {course.isEnrolled && <ReviewForm courseId={course.id} onSaved={() => { onChanged(); reviews.reload() }} />}

      <ul className="mt-10 flex flex-col gap-8">
        {reviews.loading && <Skeleton className="h-24" />}
        {reviews.data?.items.length === 0 && <li className="text-ink-soft">Chưa có đánh giá nào.</li>}
        {reviews.data?.items.map((r) => (
          <li key={r.id} className="flex gap-4">
            <Avatar name={r.userFullName} src={r.userAvatarUrl} size={44} />
            <div className="min-w-0 flex-1">
              <p className="font-bold">{r.userFullName}</p>
              <div className="flex flex-wrap items-center gap-3 text-sm text-ink-soft">
                <RatingStars value={r.rating} size={14} showValue={false} />
                <span>{formatDate(r.createdAt)}</span>
              </div>
              {r.comment && <p className="mt-2">{r.comment}</p>}
              {r.instructorReply && (
                <div className="mt-3 bg-surface-2 p-4">
                  <p className="text-sm font-bold">Phản hồi của giảng viên</p>
                  <p className="mt-1">{r.instructorReply}</p>
                </div>
              )}
            </div>
          </li>
        ))}
      </ul>
      <div className="mt-8">
        <Pagination page={page} totalPages={reviews.data?.totalPages ?? 1} onChange={setPage} />
      </div>
    </section>
  )
}

function ReviewForm({ courseId, onSaved }: { courseId: string; onSaved: () => void }) {
  const [rating, setRating] = useState(5)
  const [comment, setComment] = useState('')
  const [existing, setExisting] = useState(false)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    reviewService.mine(courseId)
      .then((r) => {
        setExisting(true)
        setRating(r.rating)
        setComment(r.comment ?? '')
      })
      .catch(() => setExisting(false))
  }, [courseId])

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      if (existing) await reviewService.update(courseId, rating, comment)
      else await reviewService.create(courseId, rating, comment)
      setExisting(true)
      toast.success('Đã lưu đánh giá của bạn')
      onSaved()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form onSubmit={submit} className="mt-8 flex flex-col gap-4 bg-brand-50 p-6">
      <fieldset>
        <legend className="font-bold">{existing ? 'Sửa đánh giá của bạn' : 'Đánh giá khóa học'}</legend>
        <div className="mt-2 flex gap-1" role="radiogroup" aria-label="Số sao">
          {[1, 2, 3, 4, 5].map((s) => (
            <button key={s} type="button" role="radio" aria-checked={rating === s} aria-label={`${s} sao`}
              onClick={() => setRating(s)}
              className={`size-11 rounded-full text-2xl transition-transform active:scale-90 ${s <= rating ? 'text-accent-500' : 'text-surface-3'}`}>
              <Star className="mx-auto size-7" fill="currentColor" aria-hidden="true" />
            </button>
          ))}
        </div>
      </fieldset>
      <TextArea label="Nhận xét" value={comment} onChange={(e) => setComment(e.target.value)} maxLength={2000} placeholder="Điều bạn thích nhất ở khóa học…" />
      <button type="submit" className="btn-primary w-fit" disabled={saving}>{saving && <Spinner className="size-4" />}Gửi đánh giá</button>
    </form>
  )
}

function PreviewDialog({ title, url, onClose }: { title: string; url: string; onClose: () => void }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="dialog" aria-modal="true" aria-label={`Xem thử: ${title}`}>
      <button type="button" className="absolute inset-0 bg-ink/80" aria-label="Đóng" onClick={onClose} />
      <div className="rise-in relative w-full max-w-4xl bg-ink">
        <div className="flex items-center justify-between gap-4 p-4 text-white">
          <p className="font-bold">{title}</p>
          <button type="button" onClick={onClose} className="rounded-full p-2 hover:bg-white/10" aria-label="Đóng xem thử">
            <X className="size-5" />
          </button>
        </div>
        <video src={url} controls autoPlay className="aspect-video w-full bg-black" />
      </div>
    </div>
  )
}

function DetailSkeleton() {
  return (
    <div>
      <div className="bg-brand-700">
        <div className="container-page grid gap-10 py-16 lg:grid-cols-[1fr_380px]">
          <div className="flex flex-col gap-4">
            <Skeleton className="h-12 w-3/4 bg-brand-600" />
            <Skeleton className="h-6 w-1/2 bg-brand-600" />
            <Skeleton className="h-6 w-1/3 bg-brand-600" />
          </div>
          <Skeleton className="h-96 bg-brand-600" />
        </div>
      </div>
    </div>
  )
}
