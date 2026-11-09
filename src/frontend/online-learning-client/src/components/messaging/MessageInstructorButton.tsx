import { useEffect, useId, useRef, useState, type FormEvent } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { MessageCircle, Send, X } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, Spinner, VerifiedBadge } from '../ui'
import { useAuth } from '../../hooks/useAuth'
import { messagingService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'

interface Props {
  instructor: { id: string; fullName: string; avatarUrl: string | null; isVerified?: boolean }
  /** Khóa học đang xem — giảng viên biết câu hỏi về khóa nào */
  course?: { id: string; title: string } | null
  label?: string
  className?: string
}

const MAX = 2000

/**
 * Nút "Nhắn tin cho giảng viên": mở hộp soạn tin nhắn đầu tiên, gửi xong chuyển tới hộp thư.
 * Khách chưa đăng nhập được đưa tới trang đăng nhập rồi quay lại đúng trang này.
 */
export function MessageInstructorButton({ instructor, course, label = 'Nhắn tin cho giảng viên', className = 'btn-outline' }: Props) {
  const { user, isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [open, setOpen] = useState(false)

  if (user?.id === instructor.id) return null

  const onClick = () => {
    if (!isAuthenticated) {
      navigate(`/dang-nhap?next=${encodeURIComponent(location.pathname + location.search)}`)
      return
    }
    setOpen(true)
  }

  return (
    <>
      <button type="button" className={className} onClick={onClick}>
        <MessageCircle className="size-4" aria-hidden="true" /> {label}
      </button>
      {open && <ComposeDialog instructor={instructor} course={course ?? null} onClose={() => setOpen(false)} />}
    </>
  )
}

function ComposeDialog({ instructor, course, onClose }: { instructor: Props['instructor']; course: Props['course']; onClose: () => void }) {
  const [text, setText] = useState('')
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()
  const ref = useRef<HTMLTextAreaElement>(null)
  const id = useId()

  useEffect(() => {
    ref.current?.focus()
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape' && !sending) onClose() }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [onClose, sending])

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const body = text.trim()
    if (!body) { setError('Nhập nội dung tin nhắn.'); return }
    setSending(true)
    setError(null)
    try {
      const c = await messagingService.start(instructor.id, body, course?.id)
      toast.success('Đã gửi tin nhắn cho giảng viên')
      navigate(`/hoc-vien/tin-nhan/${c.id}`)
    } catch (err) {
      setError(getErrorMessage(err))
      setSending(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby={`${id}-t`}>
      <button type="button" className="absolute inset-0 bg-ink/50" aria-label="Đóng" onClick={() => !sending && onClose()} />
      <form onSubmit={submit} className="rise-in card relative w-full rounded-b-none p-6 sm:max-w-lg sm:rounded-(--radius-card) sm:p-7" style={{ animationDuration: '220ms' }}>
        <div className="mb-5 flex items-start justify-between gap-4">
          <div className="flex items-center gap-3">
            <Avatar name={instructor.fullName} src={instructor.avatarUrl} size={48} />
            <div>
              <h2 id={`${id}-t`} className="flex items-center gap-1.5 text-lg font-semibold">
                {instructor.fullName} {instructor.isVerified && <VerifiedBadge compact />}
              </h2>
              <p className="text-sm text-ink-soft">Gửi câu hỏi cho giảng viên</p>
            </div>
          </div>
          <button type="button" onClick={onClose} disabled={sending} className="rounded-full p-2 hover:bg-surface-2" aria-label="Đóng"><X className="size-5" /></button>
        </div>
        {course && <p className="mb-3 rounded-lg bg-surface-2 px-3 py-2 text-sm text-ink-soft">Về khóa học: <b className="text-ink">{course.title}</b></p>}
        <label htmlFor={`${id}-m`} className="text-sm font-semibold">Tin nhắn</label>
        <textarea
          id={`${id}-m`}
          ref={ref}
          value={text}
          maxLength={MAX}
          onChange={(e) => setText(e.target.value)}
          rows={5}
          aria-invalid={!!error}
          aria-describedby={`${id}-h`}
          placeholder="Ví dụ: Em mới bắt đầu, khóa này có phù hợp không ạ?"
          className="input mt-1.5 min-h-32 py-3"
        />
        <div id={`${id}-h`} className="mt-1.5 flex justify-between gap-3 text-sm">
          {error ? <span className="font-medium text-danger" role="alert">{error}</span> : <span className="text-ink-muted">Không chia sẻ mật khẩu hay thông tin thanh toán qua tin nhắn.</span>}
          <span className="shrink-0 text-ink-muted tabular-nums">{text.length}/{MAX}</span>
        </div>
        <div className="mt-5 flex justify-end gap-2">
          <button type="button" className="btn-ghost" onClick={onClose} disabled={sending}>Hủy</button>
          <button type="submit" className="btn-primary" disabled={sending || !text.trim()}>
            {sending ? <Spinner className="size-4" /> : <Send className="size-4" aria-hidden="true" />} Gửi tin nhắn
          </button>
        </div>
      </form>
    </div>
  )
}
