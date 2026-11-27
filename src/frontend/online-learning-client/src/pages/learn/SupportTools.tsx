import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { LifeBuoy } from 'lucide-react'
import { toast } from 'sonner'
import { Modal } from '../../components/dashboard'
import { Spinner } from '../../components/ui'
import { useAsync } from '../../hooks/useAsync'
import { getErrorMessage } from '../../services/apiClient'
import { supportService, supportStatusLabel, supportStatusStyle, supportTypeLabel, type SupportRequestType } from '../../services/learnerApi'
import type { QuizIntro } from '../../types/dashboard'
import { formatDateTime } from '../../utils/format'

/**
 * Bước tiếp theo khi chưa đạt bài kiểm tra:
 * còn lượt → xem lại câu sai, ôn bài, làm lại; hết lượt → ôn bài và gửi yêu cầu cấp thêm lượt cho quản trị viên.
 */
export function QuizNextSteps({ quiz, onRequested }: { quiz: QuizIntro; onRequested: () => void }) {
  const [open, setOpen] = useState(false)
  const [message, setMessage] = useState('Em đã ôn lại các bài học và xem đáp án các lượt trước, xin được cấp thêm lượt làm bài.')
  const [sending, setSending] = useState(false)
  if (quiz.isPassed) return null
  const remaining = quiz.maxAttempts === null ? null : Math.max(0, quiz.maxAttempts - quiz.attemptsUsed)
  const exhausted = !quiz.canAttempt

  const send = async (e: FormEvent) => {
    e.preventDefault()
    setSending(true)
    try {
      await supportService.create({ courseId: quiz.courseId, type: 'QuizRetake', message: message.trim(), quizId: quiz.id })
      toast.success('Đã gửi yêu cầu. Quản trị viên sẽ xem xét và báo lại cho bạn.')
      setOpen(false)
      onRequested()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  if (!exhausted) {
    if (quiz.attemptsUsed === 0) return null
    return (
      <section className="mt-6 rounded-(--radius-card) bg-brand-50 p-5" aria-label="Bước tiếp theo">
        <h2 className="font-bold">Chưa đạt? Đây là cách làm tiếp</h2>
        <ol className="mt-2 list-decimal space-y-1 pl-5 text-sm">
          <li>Xem lại đáp án và giải thích của các câu sai (bấm vào lượt đã làm ở mục Lịch sử).</li>
          <li><Link to={`/hoc/${quiz.courseSlug}`} className="font-semibold text-brand-700 hover:underline">Ôn lại bài học</Link> liên quan.</li>
          <li>Làm lại khi đã sẵn sàng{remaining !== null && <> — bạn còn <b>{remaining}</b> lượt</>}.</li>
        </ol>
      </section>
    )
  }

  return (
    <section className="mt-6 rounded-(--radius-card) bg-danger-50 p-5" aria-label="Đã hết lượt làm bài">
      <h2 className="font-bold text-danger">Bạn đã dùng hết {quiz.maxAttempts} lượt nhưng chưa đạt</h2>
      <p className="mt-1 text-sm">Khóa học chỉ hoàn thành và cấp chứng chỉ khi đạt bài kiểm tra bắt buộc. Bạn có thể:</p>
      <ol className="mt-2 list-decimal space-y-1 pl-5 text-sm">
        <li>Xem lại đáp án, giải thích của các lượt đã làm và <Link to={`/hoc/${quiz.courseSlug}`} className="font-semibold text-brand-700 hover:underline">ôn lại bài học</Link>.</li>
        <li>Gửi yêu cầu cấp thêm lượt; quản trị viên xem xét và phản hồi qua thông báo.</li>
      </ol>
      {quiz.hasOpenRetakeRequest ? (
        <p className="mt-4 inline-flex rounded-(--radius-control) bg-warning-50 px-3 py-2 text-sm font-semibold text-warning">Đã gửi yêu cầu cấp thêm lượt — đang chờ xử lý</p>
      ) : open ? (
        <form onSubmit={send} className="mt-4 flex flex-col gap-2" noValidate>
          <label htmlFor="retake-msg" className="text-sm font-semibold">Lời nhắn cho quản trị viên</label>
          <textarea id="retake-msg" className="input min-h-20 bg-white py-2" maxLength={1000} value={message} onChange={(e) => setMessage(e.target.value)} />
          <div className="flex gap-2">
            <button type="submit" className="btn-primary" disabled={sending || message.trim().length < 10}>{sending && <Spinner className="size-4" />}Gửi yêu cầu</button>
            <button type="button" className="btn-ghost" onClick={() => setOpen(false)}>Hủy</button>
          </div>
        </form>
      ) : (
        <button type="button" className="btn-primary mt-4" onClick={() => setOpen(true)}>Gửi yêu cầu cấp thêm lượt</button>
      )}
    </section>
  )
}

const reportTypes: SupportRequestType[] = ['VideoIssue', 'ProgressIssue', 'CertificateIssue', 'Other']

/** Nút "Báo sự cố" trên thanh trình học: gửi yêu cầu hỗ trợ và xem trạng thái các yêu cầu của khóa học. */
export function ReportIssueButton({ courseId, lessonId }: { courseId: string; lessonId?: string }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button type="button" className="inline-flex items-center gap-1.5 rounded-(--radius-control) px-3 py-1.5 text-sm font-semibold hover:bg-white/10" onClick={() => setOpen(true)}>
        <LifeBuoy className="size-4" aria-hidden="true" /><span className="hidden sm:inline">Báo sự cố</span><span className="sr-only sm:hidden">Báo sự cố</span>
      </button>
      {open && <ReportDialog courseId={courseId} lessonId={lessonId} onClose={() => setOpen(false)} />}
    </>
  )
}

function ReportDialog({ courseId, lessonId, onClose }: { courseId: string; lessonId?: string; onClose: () => void }) {
  const mine = useAsync(() => supportService.mine(courseId), [courseId])
  const [type, setType] = useState<SupportRequestType>('VideoIssue')
  const [message, setMessage] = useState('')
  const [sending, setSending] = useState(false)

  const send = async (e: FormEvent) => {
    e.preventDefault()
    if (message.trim().length < 10) { toast.error('Mô tả sự cố ít nhất 10 ký tự.'); return }
    setSending(true)
    try {
      await supportService.create({ courseId, type, message: message.trim(), lessonId: lessonId ?? null })
      toast.success('Đã gửi báo cáo. Quản trị viên sẽ xử lý và báo lại qua thông báo.')
      setMessage('')
      mine.reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  return (
    <Modal title="Báo sự cố khi học" onClose={onClose}>
      <form onSubmit={send} className="flex flex-col gap-4 text-ink" noValidate>
        <label className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold">Loại sự cố</span>
          <select className="input" value={type} onChange={(e) => setType(e.target.value as SupportRequestType)}>
            {reportTypes.map((t) => <option key={t} value={t}>{supportTypeLabel[t]}</option>)}
          </select>
        </label>
        <label className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold">Mô tả</span>
          <textarea className="input min-h-24 py-2" maxLength={1000} value={message} onChange={(e) => setMessage(e.target.value)}
            placeholder="Ví dụ: bài 2 phát đến phút thứ 3 thì dừng; đã thử tải lại trang" />
        </label>
        <button type="submit" className="btn-primary self-start" disabled={sending}>{sending && <Spinner className="size-4" />}Gửi báo cáo</button>
      </form>
      {!!mine.data?.length && (
        <section className="mt-8 text-ink" aria-label="Yêu cầu đã gửi">
          <h3 className="font-bold">Yêu cầu đã gửi</h3>
          <ul className="mt-3 flex flex-col gap-3">
            {mine.data.map((r) => (
              <li key={r.id} className="rounded-(--radius-control) bg-surface-2 p-3 text-sm">
                <p className="flex flex-wrap items-center gap-2">
                  <b>{supportTypeLabel[r.type]}</b>
                  <span className={`rounded px-2 py-0.5 text-xs font-semibold ${supportStatusStyle[r.status]}`}>{supportStatusLabel[r.status]}</span>
                  <span className="text-ink-muted">{formatDateTime(r.createdAt)}</span>
                </p>
                <p className="mt-1 text-ink-soft">{r.message}</p>
                {r.response && <p className="mt-1">Phản hồi: {r.response}</p>}
              </li>
            ))}
          </ul>
        </section>
      )}
    </Modal>
  )
}
