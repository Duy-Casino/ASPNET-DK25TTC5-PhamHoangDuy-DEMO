import { Fragment, useCallback, useEffect, useLayoutEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, CheckCheck, MessageCircle, Send } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, EmptyState, ErrorState, Skeleton, Spinner, VerifiedBadge } from '../../components/ui'
import { useDocumentTitle } from '../../hooks/useAsync'
import { useAuth } from '../../hooks/useAuth'
import { messagingService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import type { ChatMessage, ConversationDetail, ConversationListItem } from '../../types/api'
import { formatRelative } from '../../utils/format'

const POLL_MS = 8000
const MAX = 2000
const dayFmt = new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Asia/Ho_Chi_Minh' })
const timeFmt = new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' })

/** Gọi lại `fn` định kỳ khi tab đang hiển thị (và ngay khi quay lại tab). */
function usePolling(fn: () => void, enabled = true) {
  useEffect(() => {
    if (!enabled) return
    const tick = () => { if (document.visibilityState === 'visible') fn() }
    const id = window.setInterval(tick, POLL_MS)
    document.addEventListener('visibilitychange', tick)
    return () => { window.clearInterval(id); document.removeEventListener('visibilitychange', tick) }
  }, [fn, enabled])
}

/**
 * Hộp thư trao đổi học viên ↔ giảng viên. `area` quyết định danh sách:
 * khu học viên hiện các cuộc trao đổi mình hỏi giảng viên; khu giảng dạy hiện câu hỏi học viên gửi tới mình.
 */
export function MessagesPage({ area }: { area: 'student' | 'instructor' }) {
  useDocumentTitle('Tin nhắn')
  const { id } = useParams()
  const base = area === 'student' ? '/hoc-vien/tin-nhan' : '/giang-day/tin-nhan'
  const [list, setList] = useState<ConversationListItem[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)

  const loadList = useCallback(() => {
    messagingService.list()
      .then((r) => { setList(r.items.filter((c) => c.iAmInstructor === (area === 'instructor'))); setListError(null) })
      .catch((e) => setListError(getErrorMessage(e)))
  }, [area])
  useEffect(() => { loadList() }, [loadList])
  usePolling(loadList)

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">Tin nhắn</h1>
        <p className="mt-2 text-ink-soft">
          {area === 'student' ? 'Trao đổi trực tiếp với giảng viên về khóa học.' : 'Câu hỏi học viên gửi tới bạn. Tỷ lệ và thời gian trả lời hiển thị trên hồ sơ công khai.'}
        </p>
      </div>
      <div className="card grid h-[min(720px,calc(100svh-220px))] min-h-[480px] overflow-hidden md:grid-cols-[320px_1fr]">
        <aside className={`min-h-0 flex-col border-line/70 md:flex md:border-r ${id ? 'hidden' : 'flex'}`} aria-label="Danh sách trao đổi">
          <ConversationList items={list} error={listError} onRetry={loadList} base={base} activeId={id} area={area} />
        </aside>
        <section className={`min-h-0 flex-col md:flex ${id ? 'flex' : 'hidden'}`} aria-label="Nội dung trao đổi">
          {id ? <Thread key={id} id={id} base={base} onChanged={loadList} />
            : <div className="flex flex-1 flex-col items-center justify-center gap-3 p-8 text-center text-ink-soft">
                <MessageCircle className="size-10 text-ink-muted" aria-hidden="true" />
                <p>Chọn một cuộc trao đổi để xem tin nhắn.</p>
              </div>}
        </section>
      </div>
    </div>
  )
}

function ConversationList({ items, error, onRetry, base, activeId, area }: {
  items: ConversationListItem[] | null; error: string | null; onRetry: () => void; base: string; activeId?: string; area: 'student' | 'instructor'
}) {
  if (error && !items) return <div className="p-4"><ErrorState message={error} onRetry={onRetry} /></div>
  if (!items) return <div className="flex flex-col gap-3 p-4">{Array.from({ length: 5 }, (_, i) => <Skeleton key={i} className="h-16" />)}</div>
  if (items.length === 0) {
    return (
      <div className="p-4">
        <EmptyState title="Chưa có tin nhắn"
          action={area === 'student' ? <Link to="/khoa-hoc" className="btn-primary">Tìm khóa học</Link> : undefined}>
          {area === 'student'
            ? 'Mở trang khóa học hoặc trang giảng viên và chọn "Nhắn tin cho giảng viên" để đặt câu hỏi.'
            : 'Khi học viên đặt câu hỏi, cuộc trao đổi sẽ hiện ở đây.'}
        </EmptyState>
      </div>
    )
  }
  return (
    <ul className="min-h-0 flex-1 overflow-y-auto">
      {items.map((c) => {
        const active = c.id === activeId
        return (
          <li key={c.id}>
            <Link to={`${base}/${c.id}`} aria-current={active ? 'page' : undefined}
              className={`flex gap-3 border-b border-line/60 px-4 py-3.5 transition-colors ${active ? 'bg-brand-50' : 'hover:bg-surface-2'}`}>
              <Avatar name={c.other.fullName} src={c.other.avatarUrl} size={44} />
              <span className="min-w-0 flex-1">
                <span className="flex items-center gap-2">
                  <span className={`truncate ${c.hasUnread ? 'font-bold' : 'font-semibold'}`}>{c.other.fullName}</span>
                  {c.other.isVerified && <VerifiedBadge compact />}
                  <span className="ml-auto shrink-0 text-xs text-ink-muted">{formatRelative(c.lastMessageAt)}</span>
                </span>
                {c.course && <span className="block truncate text-xs text-brand-700">{c.course.title}</span>}
                <span className={`mt-0.5 flex items-center gap-2 text-sm ${c.hasUnread ? 'font-semibold text-ink' : 'text-ink-soft'}`}>
                  <span className="truncate">{c.lastMessageIsMine && 'Bạn: '}{c.lastMessagePreview}</span>
                  {c.hasUnread && <span className="ml-auto size-2.5 shrink-0 rounded-full bg-brand-600" aria-label="Chưa đọc" />}
                </span>
              </span>
            </Link>
          </li>
        )
      })}
    </ul>
  )
}

function Thread({ id, base, onChanged }: { id: string; base: string; onChanged: () => void }) {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [data, setData] = useState<ConversationDetail | null>(null)
  const [older, setOlder] = useState<ChatMessage[]>([])
  const [hasMoreOlder, setHasMoreOlder] = useState(false)
  const [error, setError] = useState<{ message: string; notFound: boolean } | null>(null)
  const [text, setText] = useState('')
  const [sending, setSending] = useState(false)
  const [loadingOlder, setLoadingOlder] = useState(false)
  const scroller = useRef<HTMLDivElement>(null)
  const stickToBottom = useRef(true)
  const firstLoad = useRef(true)

  const load = useCallback(() => {
    messagingService.get(id)
      .then((d) => {
        setData(d)
        setError(null)
        if (firstLoad.current) { setHasMoreOlder(d.hasMore); firstLoad.current = false; onChanged() }
      })
      .catch((e) => setError({ message: getErrorMessage(e), notFound: (e as { response?: { status?: number } }).response?.status === 404 }))
  }, [id, onChanged])
  useEffect(() => { load() }, [load])
  usePolling(load, !error?.notFound)

  // Nhập "mới nhất" (polling) với các tin cũ đã tải thêm, bỏ trùng theo id
  const messages = (() => {
    const seen = new Set<string>()
    return [...older, ...(data?.messages ?? [])].filter((m) => (seen.has(m.id) ? false : (seen.add(m.id), true)))
  })()

  useLayoutEffect(() => {
    const el = scroller.current
    if (el && stickToBottom.current) el.scrollTop = el.scrollHeight
  }, [messages.length])

  const onScroll = () => {
    const el = scroller.current
    if (el) stickToBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80
  }

  const loadOlder = async () => {
    const first = messages[0]
    if (!first) return
    setLoadingOlder(true)
    const el = scroller.current
    const prevHeight = el?.scrollHeight ?? 0
    try {
      const page = await messagingService.get(id, first.createdAt)
      stickToBottom.current = false
      setOlder((cur) => [...page.messages, ...cur])
      setHasMoreOlder(page.hasMore)
      requestAnimationFrame(() => { if (el) el.scrollTop = el.scrollHeight - prevHeight })
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setLoadingOlder(false)
    }
  }

  const send = async (e?: FormEvent) => {
    e?.preventDefault()
    const body = text.trim()
    if (!body || sending) return
    setSending(true)
    try {
      const m = await messagingService.send(id, body)
      stickToBottom.current = true
      setData((d) => (d ? { ...d, messages: [...d.messages, m] } : d))
      setText('')
      onChanged()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  const onKey = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) { e.preventDefault(); void send() }
  }

  if (error && !data) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-4 p-8 text-center">
        <p className="text-ink-soft">{error.notFound ? 'Không tìm thấy cuộc trao đổi này.' : error.message}</p>
        <button type="button" className="btn-outline" onClick={() => navigate(base)}>Về danh sách</button>
      </div>
    )
  }
  if (!data) return <div className="flex flex-1 items-center justify-center"><Spinner className="size-6 text-brand-600" /></div>

  const lastMine = [...messages].reverse().find((m) => m.senderId === user?.id)
  const seen = !!(lastMine && data.otherLastReadAt && new Date(data.otherLastReadAt) >= new Date(lastMine.createdAt))

  return (
    <>
      <header className="flex items-center gap-3 border-b border-line/70 px-4 py-3">
        <Link to={base} className="rounded-full p-2 hover:bg-surface-2 md:hidden" aria-label="Về danh sách"><ArrowLeft className="size-5" /></Link>
        <Avatar name={data.other.fullName} src={data.other.avatarUrl} size={40} />
        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-1.5 font-semibold">
            {data.iAmInstructor ? data.other.fullName : <Link to={`/giang-vien/${data.other.id}`} className="hover:text-brand-700">{data.other.fullName}</Link>}
            {data.other.isVerified && <VerifiedBadge compact />}
          </p>
          <p className="truncate text-sm text-ink-soft">
            {data.course ? <>Về khóa học: <Link to={`/khoa-hoc/${data.course.slug}`} className="text-brand-700 hover:underline">{data.course.title}</Link></> : data.other.headline}
          </p>
        </div>
      </header>

      <div ref={scroller} onScroll={onScroll} className="min-h-0 flex-1 overflow-y-auto bg-surface-2/60 px-4 py-4" aria-live="polite">
        {hasMoreOlder && (
          <div className="mb-4 flex justify-center">
            <button type="button" className="btn-ghost text-sm" onClick={loadOlder} disabled={loadingOlder}>
              {loadingOlder && <Spinner className="size-4" />} Xem tin nhắn cũ hơn
            </button>
          </div>
        )}
        <ol className="flex flex-col gap-2">
          {messages.map((m, i) => {
            const mine = m.senderId === user?.id
            const day = dayFmt.format(new Date(m.createdAt))
            const newDay = i === 0 || dayFmt.format(new Date(messages[i - 1].createdAt)) !== day
            return (
              <Fragment key={m.id}>
                {newDay && <li className="my-2 text-center text-xs font-medium text-ink-muted first-letter:uppercase">{day}</li>}
                <li className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
                  <div className={`max-w-[85%] rounded-2xl px-4 py-2.5 sm:max-w-[70%] ${mine ? 'rounded-br-md bg-brand-600 text-white' : 'rounded-bl-md border border-line/70 bg-white'}`}>
                    <p className="leading-relaxed break-words whitespace-pre-line">{m.body}</p>
                    <p className={`mt-1 text-right text-[11px] ${mine ? 'text-brand-100' : 'text-ink-muted'}`}>
                      <span className="sr-only">{mine ? 'Bạn gửi lúc ' : `${data.other.fullName} gửi lúc `}</span>{timeFmt.format(new Date(m.createdAt))}
                    </p>
                  </div>
                </li>
              </Fragment>
            )
          })}
        </ol>
        {seen && <p className="mt-1 flex items-center justify-end gap-1 text-xs text-ink-muted"><CheckCheck className="size-3.5" aria-hidden="true" /> Đã xem</p>}
      </div>

      <form onSubmit={send} className="flex items-end gap-2 border-t border-line/70 p-3">
        <label htmlFor="chat-input" className="sr-only">Nội dung tin nhắn</label>
        <textarea id="chat-input" value={text} onChange={(e) => setText(e.target.value)} onKeyDown={onKey} maxLength={MAX} rows={1}
          placeholder="Nhập tin nhắn… (Enter để gửi, Shift+Enter xuống dòng)"
          className="input max-h-40 min-h-12 flex-1 resize-none py-3 [field-sizing:content]" />
        <button type="submit" className="btn-primary min-h-12 px-4" disabled={sending || !text.trim()} aria-label="Gửi tin nhắn">
          {sending ? <Spinner className="size-4" /> : <Send className="size-4" aria-hidden="true" />}
        </button>
      </form>
    </>
  )
}
