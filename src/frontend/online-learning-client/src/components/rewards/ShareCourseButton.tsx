import { useEffect, useId, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { Check, Copy, Gift, Share2, X } from 'lucide-react'
import { toast } from 'sonner'
import { Spinner } from '../ui'
import { useAuth } from '../../hooks/useAuth'
import { getErrorMessage } from '../../services/apiClient'
import { rewardService, shareUrl, type ShareChannel, type ShareLink } from '../../services/rewardApi'

interface Props {
  course: { id: string; title: string }
  label?: string
  className?: string
}

/**
 * Chia sẻ khóa học bằng liên kết riêng của học viên (kèm mã giới thiệu).
 * Điểm được cộng khi người khác mở liên kết (backend kiểm tra), tối đa một lần mỗi ngày.
 */
export function ShareCourseButton({ course, label = 'Chia sẻ', className = 'btn-outline' }: Props) {
  const { isAuthenticated } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [open, setOpen] = useState(false)

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
        <Share2 className="size-4" aria-hidden="true" /> {label}
      </button>
      {open && <ShareDialog course={course} onClose={() => setOpen(false)} />}
    </>
  )
}

export function ShareDialog({ course, onClose }: { course: Props['course']; onClose: () => void }) {
  const [links, setLinks] = useState<Partial<Record<ShareChannel, ShareLink>>>({})
  const [busy, setBusy] = useState<ShareChannel | null>(null)
  const [copied, setCopied] = useState(false)
  const id = useId()

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => { document.removeEventListener('keydown', onKey); document.body.style.overflow = '' }
  }, [onClose])

  /** Mỗi kênh một liên kết riêng để thống kê; dùng lại liên kết đã tạo trong ngày. */
  const linkFor = async (channel: ShareChannel) => {
    if (links[channel]) return links[channel]!
    const l = await rewardService.createShare(course.id, channel)
    setLinks((cur) => ({ ...cur, [channel]: l }))
    return l
  }

  const run = async (channel: ShareChannel, action: (url: string) => Promise<void> | void) => {
    setBusy(channel)
    try {
      await action(shareUrl(await linkFor(channel)))
    } catch (err) {
      if ((err as Error)?.name !== 'AbortError') toast.error(getErrorMessage(err))
    } finally {
      setBusy(null)
    }
  }

  const copy = async (url: string) => {
    await navigator.clipboard.writeText(url)
    setCopied(true)
    toast.success('Đã sao chép liên kết chia sẻ')
    setTimeout(() => setCopied(false), 2500)
  }

  const facebook = (url: string) => {
    window.open(`https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(url)}`, '_blank', 'noopener,noreferrer,width=640,height=560')
  }

  // Zalo không có trang chia sẻ web công khai: điện thoại dùng bảng chia sẻ của hệ điều hành (chọn Zalo),
  // máy tính sao chép liên kết rồi mở Zalo Web để dán
  const zalo = async (url: string) => {
    if (navigator.share) {
      await navigator.share({ title: course.title, text: `Khóa học "${course.title}" trên EduViet`, url })
      return
    }
    await navigator.clipboard.writeText(url)
    toast.success('Đã sao chép liên kết. Dán vào cuộc trò chuyện Zalo nhé!')
    window.open('https://chat.zalo.me/', '_blank', 'noopener,noreferrer')
  }

  const copyLink = links.CopyLink ? shareUrl(links.CopyLink) : null

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-labelledby={`${id}-t`}>
      <button type="button" className="absolute inset-0 bg-ink/50" aria-label="Đóng" onClick={onClose} />
      <div className="rise-in card relative w-full rounded-b-none p-6 sm:max-w-md sm:rounded-(--radius-card) sm:p-7" style={{ animationDuration: '220ms' }}>
        <div className="mb-4 flex items-start justify-between gap-4">
          <div>
            <h2 id={`${id}-t`} className="text-lg font-semibold">Chia sẻ khóa học</h2>
            <p className="mt-1 line-clamp-2 text-sm text-ink-soft">{course.title}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-full p-2 hover:bg-surface-2" aria-label="Đóng"><X className="size-5" /></button>
        </div>
        <p className="mb-5 flex items-start gap-2 rounded-(--radius-control) bg-mint-50 px-3 py-2.5 text-sm text-mint-700">
          <Gift className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          Nhận điểm khi bạn bè mở liên kết của bạn (tối đa một lần mỗi ngày). Bạn bè đăng ký và mua khóa học, bạn nhận thêm điểm giới thiệu.
        </p>
        <div className="grid grid-cols-3 gap-3">
          <button type="button" className="btn flex-col gap-1.5 bg-[#1877f2] py-3 text-white hover:bg-[#166fe0]" disabled={!!busy} onClick={() => run('Facebook', facebook)}>
            {busy === 'Facebook' ? <Spinner className="size-5" /> : <span className="text-lg leading-none font-bold" aria-hidden="true">f</span>} Facebook
          </button>
          <button type="button" className="btn flex-col gap-1.5 bg-[#0068ff] py-3 text-white hover:bg-[#005ee6]" disabled={!!busy} onClick={() => run('Zalo', zalo)}>
            {busy === 'Zalo' ? <Spinner className="size-5" /> : <span className="text-sm leading-none font-bold" aria-hidden="true">Zalo</span>} Zalo
          </button>
          <button type="button" className="btn-outline flex-col gap-1.5 py-3" disabled={!!busy} onClick={() => run('CopyLink', copy)}>
            {busy === 'CopyLink' ? <Spinner className="size-5" /> : copied ? <Check className="size-5 text-success" aria-hidden="true" /> : <Copy className="size-5" aria-hidden="true" />}
            {copied ? 'Đã chép' : 'Sao chép'}
          </button>
        </div>
        {copyLink && (
          <div className="mt-4">
            <label htmlFor={`${id}-l`} className="text-sm font-semibold">Liên kết của bạn</label>
            <input id={`${id}-l`} readOnly value={copyLink} onFocus={(e) => e.target.select()} className="input mt-1.5 text-sm" />
          </div>
        )}
      </div>
    </div>
  )
}
