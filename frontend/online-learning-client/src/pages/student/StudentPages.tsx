import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Award, Download, Link2, PlayCircle } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, PageHeader, Panel, ProgressBar, StatCard } from '../../components/dashboard'
import { ResponsiveImg, EmptyState, ErrorState, Pagination, Skeleton, Spinner } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { getErrorMessage } from '../../services/apiClient'
import { certificateService } from '../../services/certificateApi'
import { learningService } from '../../services/dashboardApi'
import type { CertificateItem } from '../../types/dashboard'
import { formatDate, formatDateTime, formatDuration, formatPrice } from '../../utils/format'

const filters = [
  { value: 'all', label: 'Tất cả' },
  { value: 'in-progress', label: 'Đang học' },
  { value: 'not-started', label: 'Chưa bắt đầu' },
  { value: 'completed', label: 'Đã hoàn thành' },
]

/** Dashboard học viên: số liệu + khóa học đã mua, đang học, tiến độ, tiếp tục học. */
export function MyCoursesPage() {
  useDocumentTitle('Khóa học của tôi')
  const { user } = useAuth()
  const [filter, setFilter] = useState('all')
  const [page, setPage] = useState(1)
  const summary = useAsync(() => learningService.summary(), [])
  const courses = useAsync(() => learningService.myCourses(filter, page), [filter, page])
  const s = summary.data

  const continueCourse = courses.data?.items.find((c) => c.status === 'Active' && c.lastLessonId)

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title={`Chào ${user?.fullName.split(' ').slice(-1)[0] ?? 'bạn'}, học tiếp nhé!`} description="Tiến độ của bạn được lưu trên mọi thiết bị." />

      <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        {s ? (
          <>
            <StatCard label="Khóa học đã mua" value={String(s.totalCourses)} sub={`${s.inProgress} đang học · ${s.notStarted} chưa bắt đầu`} tone="brand" />
            <StatCard label="Đã hoàn thành" value={String(s.completed)} sub={`${s.certificates} chứng chỉ`} />
            <StatCard label="Bài học đã xong" value={String(s.completedLessons)} sub={`${formatDuration(s.learningSecondsCompleted)} nội dung`} />
            <StatCard label="Bài kiểm tra đạt" value={String(s.quizzesPassed)} sub={`Đã chi ${formatPrice(s.totalSpent, '0đ')}`} tone="accent" />
          </>
        ) : Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-28" />)}
      </div>

      {continueCourse && (
        <Link to={`/hoc/${continueCourse.slug}`} className="group grid gap-5 rounded-(--radius-card) bg-ink p-5 text-white sm:grid-cols-[200px_1fr_auto] sm:items-center">
          <ResponsiveImg url={continueCourse.thumbnailUrl} sizes="(min-width: 640px) 240px, 92vw" maxWidth={640} width={400} height={225} eager className="aspect-video w-full rounded-lg object-cover" />
          <div className="min-w-0">
            <p className="text-sm text-brand-100">Tiếp tục học</p>
            <p className="mt-1 text-xl font-bold">{continueCourse.title}</p>
            <p className="mt-1 text-sm text-brand-100">Bài gần nhất: {continueCourse.lastLessonTitle}</p>
          </div>
          <span className="btn bg-sun-300 text-ink group-hover:bg-sun-500"><PlayCircle className="size-5" aria-hidden="true" />Học tiếp</span>
        </Link>
      )}

      <Panel>
        <div className="mb-6 flex flex-wrap gap-2" role="tablist" aria-label="Lọc khóa học">
          {filters.map((f) => (
            <button key={f.value} type="button" role="tab" aria-selected={filter === f.value}
              className={filter === f.value ? 'btn-primary' : 'btn-soft'} onClick={() => { setFilter(f.value); setPage(1) }}>
              {f.label}
            </button>
          ))}
        </div>
        {courses.error ? <ErrorState message={courses.error} onRetry={courses.reload} />
          : courses.loading ? <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-3">{Array.from({ length: 3 }, (_, i) => <Skeleton key={i} className="h-64" />)}</div>
          : courses.data?.items.length === 0 ? (
            <EmptyState title={filter === 'all' ? 'Bạn chưa có khóa học nào' : 'Không có khóa học ở mục này'}
              action={<Link to="/khoa-hoc" className="btn-primary">Khám phá khóa học</Link>}>
              Các khóa học bạn mua sẽ xuất hiện ở đây.
            </EmptyState>
          ) : (
            <>
              <ul className="grid gap-x-6 gap-y-10 sm:grid-cols-2 xl:grid-cols-3">
                {courses.data?.items.map((c) => (
                  <li key={c.courseId}>
                    <Link to={`/hoc/${c.slug}`} className="group flex flex-col gap-3">
                      <ResponsiveImg url={c.thumbnailUrl} sizes="(min-width: 1024px) 320px, (min-width: 640px) 46vw, 92vw" width={800} height={450} className="aspect-video w-full rounded-(--radius-card) object-cover" />
                      <h3 className="line-clamp-2 text-lg font-bold group-hover:text-brand-700">{c.title}</h3>
                      <p className="text-sm text-ink-soft">{c.instructorName}</p>
                      <ProgressBar value={c.progressPercent} label={`Tiến độ ${c.title}`} />
                      <p className="text-sm text-ink-muted">
                        {c.status === 'Completed' ? `Hoàn thành ${c.completedAt ? formatDate(c.completedAt) : ''}`
                          : c.completedLessons === 0 ? 'Chưa bắt đầu' : `${c.completedLessons}/${c.totalLessons} bài`}
                        {c.hasCertificate && ' · Có chứng chỉ'}
                      </p>
                    </Link>
                  </li>
                ))}
              </ul>
              <div className="mt-8"><Pagination page={page} totalPages={courses.data?.totalPages ?? 1} onChange={setPage} /></div>
            </>
          )}
      </Panel>
    </div>
  )
}

export function QuizResultsPage() {
  useDocumentTitle('Kết quả kiểm tra')
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => learningService.quizResults(page), [page])

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Kết quả kiểm tra" description="Mọi lượt làm bài của bạn, mới nhất ở trên." />
      <Panel>
        {error ? <ErrorState message={error} onRetry={reload} />
          : loading ? <Skeleton className="h-48" />
          : data?.items.length === 0 ? <EmptyState title="Chưa có bài kiểm tra nào">Làm bài kiểm tra trong trình học để xem kết quả ở đây.</EmptyState>
          : (
            <>
              <DataTable head={<><th>Bài kiểm tra</th><th>Khóa học</th><th>Lượt</th><th>Thời gian nộp</th><th className="text-right">Điểm</th><th>Kết quả</th></>}>
                {data?.items.map((r) => (
                  <tr key={r.attemptId}>
                    <td className="font-semibold"><Link to={`/hoc/${r.courseSlug}/kiem-tra/${r.quizId}`} className="hover:text-brand-700">{r.quizTitle}</Link></td>
                    <td>{r.courseTitle}</td>
                    <td className="tabular-nums">{r.attemptNumber}</td>
                    <td className="text-ink-soft">{formatDateTime(r.submittedAt)}</td>
                    <td className="text-right font-bold tabular-nums">{Math.round(r.scorePercent)}%</td>
                    <td><span className={`font-semibold ${r.isPassed ? 'text-success' : 'text-danger'}`}>{r.isPassed ? 'Đạt' : `Chưa đạt (cần ${r.passingScore}%)`}</span></td>
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

function CertificateCard({ cert: c }: { cert: CertificateItem }) {
  const [downloading, setDownloading] = useState(false)
  const verifyUrl = certificateService.verifyPageUrl(c.certificateCode)

  const download = async () => {
    setDownloading(true)
    try {
      await certificateService.downloadMine(c.id, c.certificateCode)
    } catch (e) {
      toast.error(getErrorMessage(e, 'Không tải được chứng chỉ.'))
    } finally {
      setDownloading(false)
    }
  }

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(verifyUrl)
      toast.success('Đã sao chép liên kết xác minh')
    } catch {
      toast.info(verifyUrl)
    }
  }

  return (
    <li className="card overflow-hidden">
      <div className="flex items-center justify-between bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white">
        <span>EduViet</span><span>Chứng chỉ hoàn thành</span>
      </div>
      <div className="p-6">
        <Award className="size-8 text-accent-500" aria-hidden="true" />
        <p className="mt-3 text-xl font-bold">{c.courseTitle}</p>
        <p className="mt-2 text-sm text-ink-soft">Cấp ngày {formatDate(c.issuedAt)}{c.finalScore !== null && ` · Điểm ${Math.round(c.finalScore)}%`}</p>
        <p className="mt-1 font-bold tabular-nums tracking-wide">{c.certificateCode}</p>
        <div className="mt-5 flex flex-wrap gap-2">
          <button type="button" className="btn-primary" onClick={download} disabled={downloading}>
            {downloading ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />} Tải PDF
          </button>
          <Link to={`/xac-minh/${c.certificateCode}`} className="btn-soft">Trang xác minh</Link>
          <button type="button" className="btn-ghost" onClick={copy}><Link2 className="size-4" aria-hidden="true" />Sao chép liên kết</button>
        </div>
      </div>
    </li>
  )
}

export function CertificatesPage() {
  useDocumentTitle('Chứng chỉ')
  const { data, loading, error, reload } = useAsync(() => learningService.certificates(), [])
  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Chứng chỉ" description="Chứng chỉ được cấp tự động khi bạn hoàn thành bài học bắt buộc và đạt bài kiểm tra." />
      {error ? <ErrorState message={error} onRetry={reload} />
        : loading ? <Skeleton className="h-40" />
        : data?.length === 0 ? (
          <EmptyState title="Bạn chưa có chứng chỉ" action={<Link to="/hoc-vien" className="btn-primary">Tiếp tục học</Link>}>
            Hoàn thành 100% bài học bắt buộc và đạt các bài kiểm tra bắt buộc để nhận chứng chỉ PDF có mã xác minh.
          </EmptyState>
        ) : (
          <ul className="grid gap-6 md:grid-cols-2">
            {data?.map((c) => <CertificateCard key={c.id} cert={c} />)}
          </ul>
        )}
    </div>
  )
}
