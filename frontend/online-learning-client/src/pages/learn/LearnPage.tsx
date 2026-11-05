import { createContext, useContext, useState } from 'react'
import { Link, Navigate, NavLink, Outlet, useParams } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Circle, ClipboardCheck, FileText, ListTree, PlayCircle, X } from 'lucide-react'
import { ErrorState, Logo, Spinner } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { learningService } from '../../services/dashboardApi'
import type { LearnCourse, LearnQuizBrief, Progress } from '../../types/dashboard'
import { formatClock } from '../../utils/format'
import NotFoundPage from '../NotFoundPage'

interface LearnContextValue {
  course: LearnCourse
  /** Cập nhật đề cương + tiến độ sau khi hoàn thành bài/nộp bài */
  refresh: () => void
  applyProgress: (lessonId: string, completed: boolean, p: Progress) => void
}
const LearnContext = createContext<LearnContextValue | null>(null)
export function useLearn() {
  const ctx = useContext(LearnContext)
  if (!ctx) throw new Error('useLearn phải nằm trong LearnPage')
  return ctx
}

/** Trình học: thanh trên (tiến độ), nội dung bài, đề cương bên phải (sheet trên điện thoại). */
export default function LearnPage() {
  const { slug = '' } = useParams()
  const { data, error, status, reload, setData } = useAsync(() => learningService.course(slug), [slug])
  const [outlineOpen, setOutlineOpen] = useState(false)
  useDocumentTitle(data?.title)

  if (status === 404) return <NotFoundPage />
  if (status === 403)
    return (
      <div className="container-page py-24">
        <h1 className="text-title font-extrabold">Bạn chưa sở hữu khóa học này</h1>
        <p className="mt-4 text-lg text-ink-soft">Mua khóa học để mở toàn bộ bài học và bài kiểm tra.</p>
        <Link to={`/khoa-hoc/${slug}`} className="btn-primary mt-8">Xem khóa học</Link>
      </div>
    )
  if (error) return <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>
  if (!data) return <div className="flex min-h-svh items-center justify-center"><Spinner className="size-8 text-brand-600" /></div>

  const applyProgress = (lessonId: string, completed: boolean, p: Progress) =>
    setData({
      ...data,
      progressPercent: p.progressPercent,
      completedLessons: p.completedLessons,
      status: p.status,
      hasCertificate: data.hasCertificate || !!p.certificateCode,
      sections: data.sections.map((s) => ({ ...s, lessons: s.lessons.map((l) => (l.id === lessonId ? { ...l, isCompleted: completed } : l)) })),
    })

  return (
    <LearnContext.Provider value={{ course: data, refresh: reload, applyProgress }}>
      <div className="flex min-h-svh flex-col bg-white">
        <header className="sticky top-0 z-30 flex h-16 items-center gap-3 bg-ink px-4 text-white sm:px-6">
          <Link to="/hoc-vien" className="flex items-center gap-2 rounded-(--radius-control) px-2 py-1.5 hover:bg-white/10" aria-label="Về khóa học của tôi">
            <ArrowLeft className="size-5" aria-hidden="true" />
            <span className="hidden sm:inline"><Logo inverted asLink={false} /></span>
          </Link>
          <p className="min-w-0 flex-1 truncate font-bold">{data.title}</p>
          {data.isPreviewMode ? (
            <span className="rounded-(--radius-control) bg-sun-300 px-3 py-1 text-sm font-semibold text-ink">Chế độ xem trước</span>
          ) : (
            <div className="hidden items-center gap-3 sm:flex" aria-label={`Đã hoàn thành ${Math.round(data.progressPercent)}%`}>
              <span className="h-2 w-32 overflow-hidden rounded-full bg-white/20">
                <span className="block h-full rounded-full bg-sun-300 transition-[width] duration-500" style={{ width: `${data.progressPercent}%` }} />
              </span>
              <span className="text-sm tabular-nums">{Math.round(data.progressPercent)}%</span>
            </div>
          )}
          <button type="button" className="rounded-(--radius-control) p-2 hover:bg-white/10 lg:hidden" onClick={() => setOutlineOpen(true)} aria-label="Mở nội dung khóa học">
            <ListTree className="size-5" />
          </button>
        </header>

        <div className="flex flex-1">
          <main id="main" className="min-w-0 flex-1">
            <Outlet />
          </main>
          <aside className="hidden w-[360px] shrink-0 bg-surface-2 lg:block" aria-label="Nội dung khóa học">
            <div className="sticky top-16 max-h-[calc(100svh-4rem)] overflow-y-auto">
              <Outline />
            </div>
          </aside>
        </div>

        {outlineOpen && (
          <div className="fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Nội dung khóa học">
            <button type="button" className="absolute inset-0 bg-ink/50" aria-label="Đóng" onClick={() => setOutlineOpen(false)} />
            <div className="rise-in absolute inset-y-0 right-0 w-[min(360px,90vw)] overflow-y-auto bg-surface-2" onClick={(e) => (e.target as HTMLElement).closest('a') && setOutlineOpen(false)}>
              <div className="flex items-center justify-between p-4">
                <p className="font-bold">Nội dung khóa học</p>
                <button type="button" className="rounded-full p-2 hover:bg-surface-3" onClick={() => setOutlineOpen(false)} aria-label="Đóng"><X className="size-5" /></button>
              </div>
              <Outline />
            </div>
          </div>
        )}
      </div>
    </LearnContext.Provider>
  )
}

export function LearnIndex() {
  const { course } = useLearn()
  if (course.resumeLessonId) return <Navigate to={`bai/${course.resumeLessonId}`} replace />
  return <div className="p-12 text-lg text-ink-soft">Khóa học chưa có bài học.</div>
}

function Outline() {
  const { course } = useLearn()
  return (
    <nav className="pb-10">
      {!course.isPreviewMode && (
        <div className="px-5 pt-5 pb-3">
          <p className="text-sm text-ink-soft">{course.completedLessons}/{course.requiredLessons} bài bắt buộc · {Math.round(course.progressPercent)}%</p>
          {course.status === 'Completed' && (
            <div className="mt-2 bg-success-50 p-3 text-sm">
              <p className="font-semibold text-success">Bạn đã hoàn thành khóa học!</p>
              {course.hasCertificate && <Link to="/hoc-vien/chung-chi" className="mt-1 inline-block font-semibold text-brand-700 underline">Xem và tải chứng chỉ</Link>}
            </div>
          )}
        </div>
      )}
      {course.sections.map((s) => (
        <div key={s.id} className="mt-2">
          <p className="px-5 py-2 font-bold">{s.title}</p>
          <ul>
            {s.lessons.map((l) => (
              <li key={l.id}>
                <NavLink
                  to={`/hoc/${course.slug}/bai/${l.id}`}
                  className={({ isActive }) => `flex items-start gap-3 px-5 py-3 text-sm transition-colors ${isActive ? 'bg-brand-100 font-semibold text-brand-800' : 'hover:bg-surface-3'}`}
                >
                  {l.isCompleted
                    ? <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" aria-label="Đã hoàn thành" />
                    : <Circle className="mt-0.5 size-4 shrink-0 text-ink-muted" aria-label="Chưa hoàn thành" />}
                  <span className="flex-1">{l.title}</span>
                  <span className="flex items-center gap-1 text-xs text-ink-muted">
                    {l.type === 'Video' ? <PlayCircle className="size-3.5" aria-hidden="true" /> : <FileText className="size-3.5" aria-hidden="true" />}
                    {formatClock(l.durationSeconds)}
                  </span>
                </NavLink>
              </li>
            ))}
            {s.quizzes.map((q) => <QuizLink key={q.id} quiz={q} slug={course.slug} />)}
          </ul>
        </div>
      ))}
      {course.courseQuizzes.length > 0 && (
        <div className="mt-4">
          <p className="px-5 py-2 font-bold">Kiểm tra cuối khóa</p>
          <ul>{course.courseQuizzes.map((q) => <QuizLink key={q.id} quiz={q} slug={course.slug} />)}</ul>
        </div>
      )}
    </nav>
  )
}

function QuizLink({ quiz, slug }: { quiz: LearnQuizBrief; slug: string }) {
  return (
    <li>
      <NavLink
        to={`/hoc/${slug}/kiem-tra/${quiz.id}`}
        className={({ isActive }) => `flex items-start gap-3 px-5 py-3 text-sm transition-colors ${isActive ? 'bg-brand-100 font-semibold text-brand-800' : 'hover:bg-surface-3'}`}
      >
        <ClipboardCheck className={`mt-0.5 size-4 shrink-0 ${quiz.isPassed ? 'text-success' : 'text-accent-700'}`} aria-hidden="true" />
        <span className="flex-1">
          {quiz.title}
          {quiz.isRequired && <span className="ml-1 text-xs text-ink-muted">(bắt buộc)</span>}
        </span>
        <span className="text-xs text-ink-muted tabular-nums">{quiz.bestScore !== null ? `${Math.round(quiz.bestScore)}%` : `Đạt ${quiz.passingScore}%`}</span>
      </NavLink>
    </li>
  )
}

