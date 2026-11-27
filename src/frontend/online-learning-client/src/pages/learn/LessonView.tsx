import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import DOMPurify from 'dompurify'
import { AlertTriangle, CheckCircle2, ChevronLeft, ChevronRight, Download } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorState, Skeleton, Spinner } from '../../components/ui'
import { useAsync } from '../../hooks/useAsync'
import { learningService } from '../../services/dashboardApi'
import { getErrorMessage } from '../../services/apiClient'
import { useLearn } from './LearnPage'
import { LessonNotes, LessonQA } from './LessonTools'

const tabs = [
  { key: 'tong-quan', label: 'Tổng quan' },
  { key: 'hoi-dap', label: 'Hỏi đáp' },
  { key: 'ghi-chu', label: 'Ghi chú' },
] as const
type TabKey = (typeof tabs)[number]['key']

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

export default function LessonView() {
  const { lessonId = '' } = useParams()
  const { course, applyProgress } = useLearn()
  const navigate = useNavigate()
  const { data: lesson, loading, error, reload, setData } = useAsync(() => learningService.lesson(lessonId), [lessonId])
  const videoRef = useRef<HTMLVideoElement>(null)
  const lastSaved = useRef(0)
  const [saving, setSaving] = useState(false)
  const [videoError, setVideoError] = useState(false)
  const [params, setParams] = useSearchParams()
  const tab: TabKey = tabs.some((t) => t.key === params.get('tab')) ? (params.get('tab') as TabKey) : 'tong-quan'
  const setTab = (t: TabKey) => setParams(t === 'tong-quan' ? {} : { tab: t }, { replace: true })

  useEffect(() => setVideoError(false), [lessonId])

  const seek = (seconds: number) => {
    const v = videoRef.current
    if (!v) return
    v.currentTime = seconds
    void v.play().catch(() => undefined)
    v.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }

  // Lưu vị trí xem video mỗi 15 giây, khi tạm dừng và khi rời trang
  useEffect(() => {
    const video = videoRef.current
    if (!video || !lesson || course.isPreviewMode) return
    const save = () => {
      const pos = Math.floor(video.currentTime)
      if (Math.abs(pos - lastSaved.current) >= 5) {
        lastSaved.current = pos
        void learningService.savePosition(lesson.id, pos).catch(() => undefined)
      }
    }
    const id = window.setInterval(save, 15000)
    video.addEventListener('pause', save)
    return () => {
      window.clearInterval(id)
      video.removeEventListener('pause', save)
      save()
    }
  }, [lesson, course.isPreviewMode])

  const setCompleted = async (completed: boolean, silent = false) => {
    if (!lesson || course.isPreviewMode) return
    setSaving(true)
    try {
      const p = completed ? await learningService.complete(lesson.id) : await learningService.uncomplete(lesson.id)
      setData({ ...lesson, isCompleted: completed })
      applyProgress(lesson.id, completed, p)
      if (p.courseCompleted && completed && p.certificateCode) {
        toast.success(`Chúc mừng! Bạn đã hoàn thành khóa học và nhận chứng chỉ ${p.certificateCode}.`, {
          action: { label: 'Xem chứng chỉ', onClick: () => navigate('/hoc-vien/chung-chi') },
          duration: 10000,
        })
      } else if (p.courseCompleted && completed) toast.success('Chúc mừng! Bạn đã hoàn thành khóa học.')
      else if (!silent) toast.success(completed ? 'Đã đánh dấu hoàn thành' : 'Đã bỏ đánh dấu')
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  if (error) return <div className="p-6 sm:p-10"><ErrorState message={error} onRetry={reload} /></div>
  if (loading || !lesson) return <div className="p-6 sm:p-10"><Skeleton className="aspect-video w-full" /><Skeleton className="mt-6 h-8 w-1/2" /></div>

  return (
    <article>
      {lesson.type === 'Video' && lesson.videoUrl && (
        <div className="relative bg-black">
          {videoError && (
            <div className="absolute inset-0 z-[1] flex flex-col items-center justify-center gap-3 bg-ink/90 p-6 text-center text-white" role="alert">
              <AlertTriangle className="size-10 text-sun-300" aria-hidden="true" />
              <p className="max-w-md font-semibold">Không phát được video. Có thể kết nối mạng chập chờn hoặc liên kết video đã hết hạn.</p>
              <button type="button" className="btn-primary" onClick={() => { setVideoError(false); reload() }}>Tải lại video</button>
              <p className="text-sm text-white/70">Nếu vẫn lỗi, hãy đặt câu hỏi ở mục "Hỏi đáp" để giảng viên kiểm tra.</p>
            </div>
          )}
          <video
            onError={() => setVideoError(true)}
            key={lesson.id}
            ref={videoRef}
            src={lesson.videoUrl}
            controls
            playsInline
            className="mx-auto aspect-video max-h-[70svh] w-full"
            onLoadedMetadata={(e) => {
              if (lesson.lastPositionSeconds > 5 && lesson.lastPositionSeconds < lesson.durationSeconds - 5) {
                e.currentTarget.currentTime = lesson.lastPositionSeconds
              }
            }}
            onEnded={() => !lesson.isCompleted && setCompleted(true, true)}
          />
        </div>
      )}

      <div className="mx-auto max-w-4xl px-4 py-8 sm:px-8 sm:py-10">
        <p className="text-sm text-ink-soft">{lesson.sectionTitle}</p>
        <div className="mt-2 flex flex-wrap items-start justify-between gap-4">
          <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">{lesson.title}</h1>
          {!course.isPreviewMode && (
            <button
              type="button"
              disabled={saving}
              onClick={() => setCompleted(!lesson.isCompleted)}
              className={lesson.isCompleted ? 'btn bg-success-50 text-success hover:bg-success-50' : 'btn-primary'}
              aria-pressed={lesson.isCompleted}
            >
              {saving ? <Spinner className="size-4" /> : <CheckCircle2 className="size-4" aria-hidden="true" />}
              {lesson.isCompleted ? 'Đã hoàn thành' : 'Đánh dấu hoàn thành'}
            </button>
          )}
        </div>

        <div className="mt-8 flex gap-1 border-b border-line" role="tablist" aria-label="Nội dung bài học">
          {tabs.map((t) => (
            <button key={t.key} type="button" role="tab" aria-selected={tab === t.key} onClick={() => setTab(t.key)}
              className={`-mb-px border-b-2 px-4 py-2 font-semibold transition-colors ${tab === t.key ? 'border-brand-600 text-brand-700' : 'border-transparent text-ink-soft hover:text-ink'}`}>
              {t.label}
            </button>
          ))}
        </div>

        {tab === 'hoi-dap' && <div className="mt-6"><LessonQA lessonId={lesson.id} readOnly={false} /></div>}
        {tab === 'ghi-chu' && (
          <div className="mt-6">
            <LessonNotes lessonId={lesson.id} hasVideo={lesson.type === 'Video' && !!lesson.videoUrl}
              getTime={() => (videoRef.current ? videoRef.current.currentTime : null)} seek={seek} />
          </div>
        )}

        {tab === 'tong-quan' && lesson.content && (
          <div
            className="mt-8 max-w-[70ch] space-y-4 text-lg leading-relaxed [&_a]:text-brand-700 [&_a]:underline [&_h2]:text-2xl [&_h2]:font-bold [&_li]:ml-6 [&_ol]:list-decimal [&_ul]:list-disc"
            dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(lesson.content) }}
          />
        )}

        {tab === 'tong-quan' && lesson.resources.length > 0 && (
          <section className="mt-10" aria-labelledby="res-h">
            <h2 id="res-h" className="text-xl font-bold">Tài liệu</h2>
            <ul className="mt-4 flex flex-col gap-2">
              {lesson.resources.map((r) => (
                <li key={r.id}>
                  <a href={r.fileUrl} target="_blank" rel="noopener noreferrer" download
                    className="flex items-center gap-3 bg-surface-2 px-4 py-3 transition-colors hover:bg-brand-50">
                    <Download className="size-5 text-brand-600" aria-hidden="true" />
                    <span className="flex-1 font-medium">{r.fileName}</span>
                    <span className="text-sm text-ink-muted">{formatSize(r.fileSize)}</span>
                  </a>
                </li>
              ))}
            </ul>
          </section>
        )}

        <nav className="mt-12 flex flex-wrap justify-between gap-3" aria-label="Chuyển bài">
          {lesson.previousLessonId ? (
            <Link to={`/hoc/${course.slug}/bai/${lesson.previousLessonId}`} className="btn-soft"><ChevronLeft className="size-4" aria-hidden="true" />Bài trước</Link>
          ) : <span />}
          {lesson.nextLessonId ? (
            <Link to={`/hoc/${course.slug}/bai/${lesson.nextLessonId}`} className="btn-primary" onClick={() => !lesson.isCompleted && !course.isPreviewMode && void setCompleted(true, true)}>
              Bài tiếp theo<ChevronRight className="size-4" aria-hidden="true" />
            </Link>
          ) : (
            course.courseQuizzes[0] && (
              <Link to={`/hoc/${course.slug}/kiem-tra/${course.courseQuizzes[0].id}`} className="btn-accent">Làm bài kiểm tra cuối khóa<ChevronRight className="size-4" aria-hidden="true" /></Link>
            )
          )}
        </nav>
      </div>
    </article>
  )
}
