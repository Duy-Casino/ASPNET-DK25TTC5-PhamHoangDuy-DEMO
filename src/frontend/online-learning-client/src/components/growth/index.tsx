import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Heart, Timer } from 'lucide-react'
import { toast } from 'sonner'
import { useWishlist } from '../../hooks/useWishlist'

/** Nút trái tim thêm/bỏ khóa học khỏi danh sách yêu thích. Chưa đăng nhập thì chuyển tới trang đăng nhập. */
export function WishlistButton({ courseId, title, variant = 'icon', className = '' }: {
  courseId: string; title: string; variant?: 'icon' | 'button'; className?: string
}) {
  const { has, toggle, enabled } = useWishlist()
  const navigate = useNavigate()
  const active = has(courseId)
  const label = active ? `Bỏ "${title}" khỏi yêu thích` : `Thêm "${title}" vào yêu thích`

  const onClick = (e: React.MouseEvent) => {
    e.preventDefault()
    e.stopPropagation()
    if (!enabled) {
      toast.info('Vui lòng đăng nhập để lưu khóa học yêu thích')
      navigate(`/dang-nhap?next=${encodeURIComponent(window.location.pathname)}`)
      return
    }
    void toggle(courseId, title)
  }

  if (variant === 'button') {
    return (
      <button type="button" aria-pressed={active} onClick={onClick} className={`btn-outline min-h-12 ${className}`}>
        <Heart className={`size-5 ${active ? 'fill-danger text-danger' : ''}`} aria-hidden="true" />
        {active ? 'Đã yêu thích' : 'Yêu thích'}
      </button>
    )
  }
  return (
    <button type="button" aria-pressed={active} aria-label={label} title={label} onClick={onClick}
      className={`grid size-9 place-items-center rounded-full bg-white/95 shadow-(--shadow-soft) transition-transform active:scale-90 ${className}`}>
      <Heart className={`size-[18px] ${active ? 'fill-danger text-danger' : 'text-ink-soft'}`} aria-hidden="true" />
    </button>
  )
}

function remaining(until: string, now: number) {
  const ms = new Date(until).getTime() - now
  if (ms <= 0) return null
  const d = Math.floor(ms / 86_400_000)
  const h = Math.floor((ms % 86_400_000) / 3_600_000)
  const m = Math.floor((ms % 3_600_000) / 60_000)
  const s = Math.floor((ms % 60_000) / 1000)
  const hms = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
  return { d, hms, short: d > 0 ? `${d} ngày` : `${h > 0 ? `${h} giờ ` : ''}${m} phút` }
}

/** Đồng hồ đếm ngược flash sale. Hết giờ thì ẩn (giá được máy chủ tính lại khi đặt hàng). */
export function FlashSaleCountdown({ endsAt, compact = false }: { endsAt: string; compact?: boolean }) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), compact ? 30_000 : 1000)
    return () => window.clearInterval(id)
  }, [compact])
  const r = remaining(endsAt, now)
  if (!r) return null
  if (compact) {
    return <span className="inline-flex items-center gap-1 rounded-full bg-danger px-2.5 py-1 text-xs font-bold text-white"><Timer className="size-3.5" aria-hidden="true" />Còn {r.short}</span>
  }
  return (
    <div className="flex items-center gap-3 rounded-(--radius-control) bg-danger-50 px-4 py-3 text-danger" role="timer" aria-live="off">
      <Timer className="size-5 shrink-0" aria-hidden="true" />
      <span className="text-sm font-semibold">
        Flash sale kết thúc sau <span className="tabular-nums">{r.d > 0 ? `${r.d} ngày ` : ''}{r.hms}</span>
      </span>
    </div>
  )
}
