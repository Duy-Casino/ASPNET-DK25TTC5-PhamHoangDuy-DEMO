import { useState, type FormEvent } from 'react'
import { CheckCircle2, Clock3, MessageCircleQuestion, Pencil, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, EmptyState, ErrorState, Pagination, Skeleton, Spinner } from '../../components/ui'
import { useAsync } from '../../hooks/useAsync'
import { getErrorMessage } from '../../services/apiClient'
import { lessonQaService, noteService, type LessonNote, type LessonQuestion } from '../../services/learnerApi'
import { formatClock, formatRelative } from '../../utils/format'

// =================== Hỏi đáp theo bài học ===================
export function LessonQA({ lessonId, readOnly }: { lessonId: string; readOnly: boolean }) {
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => lessonQaService.list(lessonId, page), [lessonId, page])
  const [content, setContent] = useState('')
  const [sending, setSending] = useState(false)

  const ask = async (e: FormEvent) => {
    e.preventDefault()
    if (content.trim().length < 5) { toast.error('Câu hỏi tối thiểu 5 ký tự.'); return }
    setSending(true)
    try {
      await lessonQaService.ask(lessonId, content.trim())
      setContent('')
      toast.success('Đã gửi câu hỏi. Giảng viên và các bạn cùng khóa sẽ thấy câu hỏi của bạn.')
      if (page !== 1) setPage(1)
      else reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {!readOnly && (
        <form onSubmit={ask} className="flex flex-col gap-2" noValidate>
          <label htmlFor="ask" className="font-semibold">Đặt câu hỏi về bài học này</label>
          <textarea id="ask" className="input min-h-24 py-3" maxLength={2000} value={content} onChange={(e) => setContent(e.target.value)}
            placeholder="Mô tả chỗ bạn chưa hiểu, càng cụ thể càng dễ được giải đáp" />
          <button type="submit" className="btn-primary self-start" disabled={sending || !content.trim()}>{sending && <Spinner className="size-4" />}Gửi câu hỏi</button>
        </form>
      )}
      {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-32" /> : data?.items.length === 0 ? (
        <p className="flex items-center gap-2 text-ink-soft"><MessageCircleQuestion className="size-5" aria-hidden="true" />Chưa có câu hỏi nào cho bài học này.</p>
      ) : (
        <ul className="flex flex-col gap-4">
          {data?.items.map((q) => <QuestionItem key={q.id} question={q} readOnly={readOnly} onChanged={reload} />)}
        </ul>
      )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />
    </div>
  )
}

function QuestionItem({ question: q, readOnly, onChanged }: { question: LessonQuestion; readOnly: boolean; onChanged: () => void }) {
  const [reply, setReply] = useState('')
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)

  const run = async (fn: () => Promise<unknown>, ok?: string) => {
    setBusy(true)
    try {
      await fn()
      if (ok) toast.success(ok)
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  const send = (e: FormEvent) => {
    e.preventDefault()
    if (reply.trim().length < 5) { toast.error('Câu trả lời tối thiểu 5 ký tự.'); return }
    void run(async () => { await lessonQaService.answer(q.id, reply.trim()); setReply(''); setOpen(false) }, 'Đã gửi câu trả lời')
  }

  return (
    <li className="card p-4" aria-label={`Câu hỏi của ${q.author.fullName}`}>
      <div className="flex items-start gap-3">
        <Avatar name={q.author.fullName} src={q.author.avatarUrl} size={36} />
        <div className="min-w-0 flex-1">
          <p className="text-sm"><b>{q.author.fullName}</b> <span className="text-ink-muted">· {formatRelative(q.createdAt)}</span>
            {q.isResolved && <span className="ml-2 inline-flex items-center gap-1 font-semibold text-success"><CheckCircle2 className="size-4" aria-hidden="true" />Đã giải đáp</span>}</p>
          <p className="mt-1 whitespace-pre-line">{q.content}</p>

          {q.answers.length > 0 && (
            <ul className="mt-3 flex flex-col gap-3 border-l-2 border-line pl-4">
              {q.answers.map((a) => (
                <li key={a.id} className={a.isInstructor ? 'rounded-(--radius-control) bg-brand-50 p-3' : ''}>
                  <p className="text-sm"><b>{a.author.fullName}</b>
                    {a.isInstructor && <span className="ml-2 rounded bg-brand-600 px-1.5 py-0.5 text-xs font-bold text-white">Giảng viên</span>}
                    <span className="text-ink-muted"> · {formatRelative(a.createdAt)}</span>
                    {(a.isMine || q.canModerate) && !readOnly && (
                      <button type="button" className="ml-2 text-xs text-ink-muted hover:text-danger" disabled={busy}
                        onClick={() => window.confirm('Xóa câu trả lời này?') && run(() => lessonQaService.removeAnswer(a.id))}>Xóa</button>
                    )}
                  </p>
                  <p className="mt-1 whitespace-pre-line">{a.content}</p>
                </li>
              ))}
            </ul>
          )}

          {!readOnly && (
            <div className="mt-3 flex flex-wrap gap-2 text-sm">
              <button type="button" className="font-semibold text-brand-700 hover:underline" onClick={() => setOpen((o) => !o)}>Trả lời</button>
              {q.canModerate && (
                <button type="button" className="font-semibold text-ink-soft hover:underline" disabled={busy}
                  onClick={() => run(() => lessonQaService.setResolved(q.id, !q.isResolved))}>
                  {q.isResolved ? 'Bỏ đánh dấu đã giải đáp' : 'Đánh dấu đã giải đáp'}
                </button>
              )}
              {q.canModerate && (
                <button type="button" className="text-ink-muted hover:text-danger" disabled={busy}
                  onClick={() => window.confirm('Xóa câu hỏi và các câu trả lời?') && run(() => lessonQaService.removeQuestion(q.id), 'Đã xóa câu hỏi')}>Xóa</button>
              )}
            </div>
          )}
          {open && (
            <form onSubmit={send} className="mt-3 flex flex-col gap-2" noValidate>
              <label htmlFor={`reply-${q.id}`} className="sr-only">Câu trả lời</label>
              <textarea id={`reply-${q.id}`} className="input min-h-20 py-2" maxLength={2000} value={reply} onChange={(e) => setReply(e.target.value)} placeholder="Viết câu trả lời" />
              <button type="submit" className="btn-soft self-start" disabled={busy || !reply.trim()}>{busy && <Spinner className="size-4" />}Gửi trả lời</button>
            </form>
          )}
        </div>
      </div>
    </li>
  )
}

// =================== Ghi chú cá nhân ===================
export function LessonNotes({ lessonId, getTime, seek, hasVideo }: {
  lessonId: string; getTime: () => number | null; seek: (seconds: number) => void; hasVideo: boolean
}) {
  const { data, loading, error, reload, setData } = useAsync(() => noteService.list(lessonId), [lessonId])
  const [content, setContent] = useState('')
  const [withTime, setWithTime] = useState(true)
  const [saving, setSaving] = useState(false)
  const [editing, setEditing] = useState<LessonNote | null>(null)
  const [editText, setEditText] = useState('')

  const sorted = (list: LessonNote[]) =>
    [...list].sort((a, b) => (a.positionSeconds ?? Infinity) - (b.positionSeconds ?? Infinity) || a.createdAt.localeCompare(b.createdAt))

  const add = async (e: FormEvent) => {
    e.preventDefault()
    if (!content.trim()) return
    setSaving(true)
    try {
      const t = hasVideo && withTime ? getTime() : null
      const note = await noteService.add(lessonId, content.trim(), t === null ? null : Math.floor(t))
      setData(sorted([...(data ?? []), note]))
      setContent('')
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  const saveEdit = async () => {
    if (!editing || !editText.trim()) return
    try {
      const n = await noteService.update(editing.id, editText.trim(), editing.positionSeconds)
      setData(sorted((data ?? []).map((x) => (x.id === n.id ? n : x))))
      setEditing(null)
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  const remove = async (id: string) => {
    if (!window.confirm('Xóa ghi chú này?')) return
    try {
      await noteService.remove(id)
      setData((data ?? []).filter((x) => x.id !== id))
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <form onSubmit={add} className="flex flex-col gap-2" noValidate>
        <label htmlFor="note" className="font-semibold">Ghi chú mới <span className="font-normal text-ink-muted">(chỉ mình bạn thấy)</span></label>
        <textarea id="note" className="input min-h-20 py-3" maxLength={2000} value={content} onChange={(e) => setContent(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) void add(e as unknown as FormEvent) }}
          placeholder="Ghi lại ý chính… (Ctrl + Enter để lưu)" />
        <div className="flex flex-wrap items-center gap-4">
          <button type="submit" className="btn-primary" disabled={saving || !content.trim()}>{saving && <Spinner className="size-4" />}Lưu ghi chú</button>
          {hasVideo && (
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" className="size-4 accent-brand-600" checked={withTime} onChange={(e) => setWithTime(e.target.checked)} />
              Gắn mốc thời gian video hiện tại
            </label>
          )}
        </div>
      </form>

      {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-24" /> : data?.length === 0 ? (
        <EmptyState title="Chưa có ghi chú">Ghi chú gắn mốc thời gian giúp bạn mở lại đúng đoạn video cần xem.</EmptyState>
      ) : (
        <ul className="flex flex-col gap-3">
          {data?.map((n) => (
            <li key={n.id} className="card flex items-start gap-3 p-4">
              {n.positionSeconds !== null ? (
                <button type="button" className="inline-flex shrink-0 items-center gap-1 rounded-full bg-brand-50 px-2.5 py-1 text-sm font-bold text-brand-800 tabular-nums hover:bg-brand-100"
                  onClick={() => seek(n.positionSeconds!)} aria-label={`Tua video đến ${formatClock(n.positionSeconds)}`}>
                  <Clock3 className="size-4" aria-hidden="true" />{formatClock(n.positionSeconds)}
                </button>
              ) : <span className="shrink-0 rounded-full bg-surface-2 px-2.5 py-1 text-sm text-ink-muted">Chung</span>}
              <div className="min-w-0 flex-1">
                {editing?.id === n.id ? (
                  <div className="flex flex-col gap-2">
                    <label htmlFor={`edit-${n.id}`} className="sr-only">Sửa ghi chú</label>
                    <textarea id={`edit-${n.id}`} className="input min-h-20 py-2" maxLength={2000} value={editText} onChange={(e) => setEditText(e.target.value)} />
                    <div className="flex gap-2">
                      <button type="button" className="btn-soft" onClick={saveEdit}>Lưu</button>
                      <button type="button" className="btn-ghost" onClick={() => setEditing(null)}>Hủy</button>
                    </div>
                  </div>
                ) : <p className="whitespace-pre-line">{n.content}</p>}
              </div>
              {editing?.id !== n.id && (
                <div className="flex shrink-0 gap-1">
                  <button type="button" className="btn-ghost min-h-9 px-2" aria-label="Sửa ghi chú" onClick={() => { setEditing(n); setEditText(n.content) }}><Pencil className="size-4" /></button>
                  <button type="button" className="btn-ghost min-h-9 px-2 text-danger" aria-label="Xóa ghi chú" onClick={() => remove(n.id)}><Trash2 className="size-4" /></button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
