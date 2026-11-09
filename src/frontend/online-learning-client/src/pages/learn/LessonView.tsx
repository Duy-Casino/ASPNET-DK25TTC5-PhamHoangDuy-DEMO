import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import DOMPurify from 'dompurify'
import { CheckCircle2, ChevronLeft, ChevronRight, Download } from 'lucide-react'
import { toast } from 'sonner'
import { ErrorState, Skeleton, Spinner } from '../../components/ui'
import { useAsync } from '../../hooks/useAsync'
import { learningService } from '../../services/dashboardApi'
import { getErrorMessage } from '../../services/apiClient'
import { useLearn } from './LearnPage'

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
        <div className="bg-black">
          <video
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

        {lesson.content && (
          <div
            className="mt-8 max-w-[70ch] space-y-4 text-lg leading-relaxed [&_a]:text-brand-700 [&_a]:underline [&_h2]:text-2xl [&_h2]:font-bold [&_li]:ml-6 [&_ol]:list-decimal [&_ul]:list-disc"
            dangerouslySetInnerHTML={{ __html: DOMPurify.sanitize(lesson.content) }}
          />
        )}

        {lesson.resources.length > 0 && (
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
