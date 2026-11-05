import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { toast } from 'sonner'
import { CountChart, DataTable, Modal, PageHeader, Panel, ProgressBar, RevenueChart, SelectField, StatCard, StatusBadge } from '../../components/dashboard'
import { Avatar, EmptyState, ErrorState, Field, Pagination, RatingStars, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { catalogService } from '../../services/api'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import { instructorService } from '../../services/dashboardApi'
import type { CourseStatus } from '../../types/common'
import { courseStatusLabel } from '../../types/common'
import { formatDate, formatDateTime, formatNumber, formatPrice } from '../../utils/format'

export function InstructorOverviewPage() {
  useDocumentTitle('Tổng quan giảng dạy')
  const { data, loading, error, reload } = useAsync(() => instructorService.overview(), [])

  if (error) return <ErrorState message={error} onRetry={reload} />
  if (loading || !data) return <div className="grid gap-4 lg:grid-cols-4">{Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-28" />)}</div>

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Tổng quan giảng dạy" description="Doanh thu tính trên đơn đã thanh toán, sau khi trừ mã giảm giá."
        actions={<Link to="/giang-day/khoa-hoc" className="btn-primary"><Plus className="size-4" aria-hidden="true" />Khóa học mới</Link>} />
      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <StatCard label="Doanh thu" value={formatPrice(data.totalRevenue, '0đ')} sub={`30 ngày: ${formatPrice(data.revenueLast30Days, '0đ')}`} tone="brand" />
        <StatCard label="Học viên" value={formatNumber(data.totalStudents)} sub={`+${data.enrollmentsLast30Days} lượt đăng ký trong 30 ngày`} />
        <StatCard label="Khóa học" value={String(data.totalCourses)} sub={`${data.publishedCourses} đã xuất bản · ${data.pendingCourses} chờ duyệt`} />
        <StatCard label="Đánh giá trung bình" value={data.totalReviews ? data.averageRating.toFixed(1) : '—'} sub={`${data.totalReviews} đánh giá`} tone="accent" />
      </div>
      <div className="grid gap-6 xl:grid-cols-[1.4fr_1fr]">
        <Panel title="Doanh thu 12 tháng"><RevenueChart data={data.monthly} /></Panel>
        <Panel title="Lượt đăng ký theo tháng"><CountChart data={data.monthly} dataKey="enrollments" label="Lượt đăng ký" /></Panel>
      </div>
      <Panel title="Theo khóa học">
        {data.courses.length === 0 ? <p className="text-ink-soft">Chưa có khóa học.</p> : (
          <DataTable head={<><th>Khóa học</th><th>Trạng thái</th><th className="text-right">Học viên</th><th className="text-right">Doanh thu</th><th>Đánh giá</th><th className="w-48">Tiến độ TB</th></>}>
            {data.courses.map((c) => (
              <tr key={c.courseId}>
                <td className="font-semibold"><Link to={`/giang-day/khoa-hoc/${c.courseId}`} className="hover:text-brand-700">{c.title}</Link></td>
                <td><StatusBadge status={c.status} /></td>
                <td className="text-right tabular-nums">{c.enrollments}</td>
                <td className="text-right font-semibold tabular-nums">{formatPrice(c.revenue, '0đ')}</td>
                <td>{c.reviewCount ? <RatingStars value={c.averageRating} size={12} count={c.reviewCount} /> : <span className="text-ink-muted">—</span>}</td>
                <td><ProgressBar value={c.averageProgress} label={`Tiến độ trung bình ${c.title}`} /></td>
              </tr>
            ))}
          </DataTable>
        )}
      </Panel>
    </div>
  )
}

export function InstructorCoursesPage() {
  useDocumentTitle('Khóa học của tôi')
  const [status, setStatus] = useState<CourseStatus | ''>('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)
  const { data, loading, error, reload } = useAsync(() => instructorService.courses(status || undefined, page), [status, page])

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Khóa học" description="Soạn nội dung, gửi duyệt và theo dõi trạng thái."
        actions={<button type="button" className="btn-primary" onClick={() => setCreating(true)}><Plus className="size-4" aria-hidden="true" />Tạo khóa học</button>} />
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Lọc trạng thái">
        {(['', 'Draft', 'Pending', 'Published', 'Rejected'] as const).map((s) => (
          <button key={s} type="button" role="tab" aria-selected={status === s} className={status === s ? 'btn-primary' : 'btn-soft'}
            onClick={() => { setStatus(s); setPage(1) }}>{s ? courseStatusLabel[s] : 'Tất cả'}</button>
        ))}
      </div>
      {error ? <ErrorState message={error} onRetry={reload} />
        : loading ? <Skeleton className="h-64" />
        : data?.items.length === 0 ? (
          <EmptyState title="Chưa có khóa học" action={<button type="button" className="btn-primary" onClick={() => setCreating(true)}>Tạo khóa học đầu tiên</button>}>
            Bắt đầu bằng tên khóa học và danh mục, phần còn lại bạn có thể bổ sung sau.
          </EmptyState>
        ) : (
          <ul className="flex flex-col gap-3">
            {data?.items.map((c) => (
              <li key={c.id}>
                <Link to={`/giang-day/khoa-hoc/${c.id}`} className="grid gap-4 rounded-(--radius-card) bg-white p-4 transition-colors hover:bg-brand-50 sm:grid-cols-[160px_1fr_auto] sm:items-center">
                  {c.thumbnailUrl
                    ? <img src={c.thumbnailUrl} alt="" width={320} height={180} className="aspect-video w-full rounded-lg object-cover" />
                    : <div className="flex aspect-video w-full items-center justify-center rounded-lg bg-surface-3 text-sm text-ink-muted">Chưa có ảnh</div>}
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2"><StatusBadge status={c.status} /></div>
                    <p className="mt-2 text-lg font-bold">{c.title}</p>
                    <p className="mt-1 text-sm text-ink-soft">
                      {c.totalLessons} bài · {c.enrollmentCount} học viên · {formatPrice(c.discountPrice ?? c.price)} · cập nhật {formatDate(c.updatedAt ?? c.createdAt)}
                    </p>
                    {c.status === 'Rejected' && c.rejectionReason && <p className="mt-2 bg-danger-50 p-2 text-sm text-danger">Lý do từ chối: {c.rejectionReason}</p>}
                  </div>
                  <span className="btn-soft">Soạn nội dung</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />
      {creating && <CreateCourseModal onClose={() => setCreating(false)} />}
    </div>
  )
}

function CreateCourseModal({ onClose }: { onClose: () => void }) {
  const navigate = useNavigate()
  const categories = useAsync(() => catalogService.categories(), [])
  const [title, setTitle] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [price, setPrice] = useState('0')
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      const course = await instructorService.createCourse({
        title, categoryId, price: Number(price) || 0, discountPrice: null, level: 'Beginner', language: 'vi',
        shortDescription: null, description: null, requirements: [], outcomes: [], thumbnailUrl: null, previewVideoUrl: null,
      })
      toast.success('Đã tạo khóa học. Tiếp tục soạn nội dung.')
      navigate(`/giang-day/khoa-hoc/${course.id}`)
    } catch (err) {
      setErrors(getFieldErrors(err))
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title="Tạo khóa học" onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <Field label="Tên khóa học" value={title} onChange={(e) => setTitle(e.target.value)} error={errors.title} required maxLength={200} autoFocus />
        <SelectField label="Danh mục" value={categoryId} onChange={setCategoryId}>
          <option value="">— Chọn danh mục —</option>
          {categories.data?.flatMap((c) => [c, ...c.children]).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </SelectField>
        {errors.categoryId && <p className="-mt-3 text-sm text-danger">{errors.categoryId}</p>}
        <Field label="Giá bán (đồng)" type="number" min={0} step={1000} value={price} onChange={(e) => setPrice(e.target.value)} error={errors.price} hint="Nhập 0 nếu là khóa học miễn phí." />
        <button type="submit" className="btn-primary min-h-12" disabled={saving || !title.trim() || !categoryId}>{saving && <Spinner className="size-4" />}Tạo và soạn nội dung</button>
      </form>
    </Modal>
  )
}

export function InstructorStudentsPage() {
  useDocumentTitle('Học viên')
  const [keyword, setKeyword] = useState('')
  const [courseId, setCourseId] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const courses = useAsync(() => instructorService.courses(undefined, 1), [])
  const { data, loading, error, reload } = useAsync(() => instructorService.students({ keyword: q || undefined, courseId: courseId || undefined, page }), [q, courseId, page])

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Học viên" description="Danh sách lượt ghi danh vào các khóa học của bạn." />
      <Panel>
        <div className="mb-6 grid gap-4 sm:grid-cols-2">
          <Field label="Tìm học viên" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Tên hoặc email" />
          <SelectField label="Khóa học" value={courseId} onChange={(v) => { setCourseId(v); setPage(1) }}>
            <option value="">Tất cả khóa học</option>
            {courses.data?.items.map((c) => <option key={c.id} value={c.id}>{c.title}</option>)}
          </SelectField>
        </div>
        {error ? <ErrorState message={error} onRetry={reload} />
          : loading ? <Skeleton className="h-48" />
          : data?.items.length === 0 ? <EmptyState title="Chưa có học viên">Học viên sẽ xuất hiện khi có người mua khóa học của bạn.</EmptyState>
          : (
            <>
              <DataTable head={<><th>Học viên</th><th>Khóa học</th><th className="w-48">Tiến độ</th><th>Trạng thái</th><th>Ghi danh</th><th>Học gần nhất</th></>}>
                {data?.items.map((s) => (
                  <tr key={`${s.userId}-${s.courseId}`}>
                    <td>
                      <div className="flex items-center gap-3">
                        <Avatar name={s.fullName} src={s.avatarUrl} size={36} />
                        <div><p className="font-semibold">{s.fullName}</p><p className="text-ink-muted">{s.email}</p></div>
                      </div>
                    </td>
                    <td>{s.courseTitle}</td>
                    <td><ProgressBar value={s.progressPercent} label={`Tiến độ ${s.fullName}`} /></td>
                    <td><StatusBadge status={s.status} /></td>
                    <td className="text-ink-soft">{formatDate(s.enrolledAt)}</td>
                    <td className="text-ink-soft">{s.lastAccessedAt ? formatDateTime(s.lastAccessedAt) : '—'}</td>
                  </tr>
                ))}
              </DataTable>
              <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
            </>
          )}
      </Panel>
    </div>
  )
}

export function InstructorReviewsPage() {
  useDocumentTitle('Đánh giá')
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => instructorService.reviews(page), [page])
  const [replyingId, setReplyingId] = useState<string | null>(null)
  const [reply, setReply] = useState('')
  const [saving, setSaving] = useState(false)

  const send = async (id: string) => {
    if (!reply.trim()) return
    setSaving(true)
    try {
      await instructorService.reply(id, reply.trim())
      toast.success('Đã gửi phản hồi')
      setReplyingId(null)
      setReply('')
      reload()
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Đánh giá khóa học" description="Phản hồi đánh giá giúp học viên mới tin tưởng hơn." />
      {error ? <ErrorState message={error} onRetry={reload} />
        : loading ? <Skeleton className="h-64" />
        : data?.items.length === 0 ? <EmptyState title="Chưa có đánh giá">Đánh giá của học viên sẽ hiển thị ở đây.</EmptyState>
        : (
          <ul className="flex flex-col gap-4">
            {data?.items.map((r) => (
              <li key={r.id} className="rounded-(--radius-card) bg-white p-5">
                <div className="flex flex-wrap items-center gap-3">
                  <Avatar name={r.userFullName} src={r.userAvatarUrl} size={40} />
                  <div className="min-w-0 flex-1">
                    <p className="font-semibold">{r.userFullName}</p>
                    <p className="text-sm text-ink-soft">{r.courseTitle} · {formatDate(r.createdAt)}</p>
                  </div>
                  <RatingStars value={r.rating} showValue={false} />
                </div>
                {r.comment && <p className="mt-3">{r.comment}</p>}
                {r.instructorReply ? (
                  <div className="mt-4 bg-surface-2 p-4"><p className="text-sm font-bold">Phản hồi của bạn</p><p className="mt-1">{r.instructorReply}</p></div>
                ) : replyingId === r.id ? (
                  <div className="mt-4 flex flex-col gap-3">
                    <TextArea label="Phản hồi" value={reply} onChange={(e) => setReply(e.target.value)} maxLength={2000} autoFocus />
                    <div className="flex gap-2">
                      <button type="button" className="btn-primary" disabled={saving || !reply.trim()} onClick={() => send(r.id)}>{saving && <Spinner className="size-4" />}Gửi</button>
                      <button type="button" className="btn-ghost" onClick={() => setReplyingId(null)}>Hủy</button>
                    </div>
                  </div>
                ) : (
                  <button type="button" className="btn-soft mt-4" onClick={() => { setReplyingId(r.id); setReply('') }}>Phản hồi</button>
                )}
              </li>
            ))}
          </ul>
        )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />
    </div>
  )
}
