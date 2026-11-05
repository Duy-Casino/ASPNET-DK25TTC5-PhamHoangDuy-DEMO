import { useId, type InputHTMLAttributes, type ReactNode, type TextareaHTMLAttributes } from 'react'
import { Link } from 'react-router-dom'
import { Loader2, Star } from 'lucide-react'
import { initials } from '../../utils/format'

export function Logo({ inverted = false, asLink = true }: { inverted?: boolean; asLink?: boolean }) {
  const content = (
    <>
      <svg width="30" height="30" viewBox="0 0 32 32" aria-hidden="true">
        <rect width="32" height="32" rx="8" fill={inverted ? '#fff' : '#005CF8'} />
        <path d="M9 10h14v3H12.5v2.5H21v3h-8.5V22H23v3H9z" fill={inverted ? '#005CF8' : '#fff'} />
        <circle cx="25" cy="8" r="3" fill="#EF8000" />
      </svg>
      <span className={`text-xl ${inverted ? 'text-white' : 'text-ink'}`}>EduViet</span>
    </>
  )
  const cls = 'inline-flex items-center gap-2 font-extrabold tracking-tight'
  return asLink
    ? <Link to="/" className={cls} aria-label="EduViet — trang chủ">{content}</Link>
    : <span className={cls}>{content}</span>
}

export function Spinner({ className = '' }: { className?: string }) {
  return <Loader2 className={`animate-spin ${className}`} aria-hidden="true" />
}

export function RatingStars({ value, size = 16, showValue = true, count, light = false }: { value: number; size?: number; showValue?: boolean; count?: number; light?: boolean }) {
  const rounded = Math.round(value * 2) / 2
  return (
    <span className="inline-flex items-center gap-1.5" aria-label={`${value.toFixed(1)} trên 5 sao`}>
      {showValue && <span className={`font-bold ${light ? "text-sun-300" : "text-accent-700"}`}>{value.toFixed(1)}</span>}
      <span className="inline-flex" aria-hidden="true">
        {[1, 2, 3, 4, 5].map((i) => (
          <Star
            key={i}
            width={size}
            height={size}
            className={i <= rounded ? 'fill-accent-500 text-accent-500' : i - 0.5 === rounded ? 'fill-accent-200 text-accent-500' : 'fill-surface-3 text-surface-3'}
          />
        ))}
      </span>
      {count !== undefined && <span className={light ? "text-brand-100" : "text-ink-muted"}>({count})</span>}
    </span>
  )
}

export function Avatar({ name, src, size = 40 }: { name: string; src?: string | null; size?: number }) {
  if (src) {
    return <img src={src} alt="" width={size} height={size} className="shrink-0 rounded-full object-cover" style={{ width: size, height: size }} />
  }
  return (
    <span
      aria-hidden="true"
      className="inline-flex shrink-0 items-center justify-center rounded-full bg-brand-100 font-bold text-brand-700"
      style={{ width: size, height: size, fontSize: size * 0.38 }}
    >
      {initials(name)}
    </span>
  )
}

export function Skeleton({ className = '' }: { className?: string }) {
  return <div className={`animate-pulse rounded-(--radius-card) bg-surface-3 ${className}`} aria-hidden="true" />
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="flex flex-col items-start gap-4 rounded-(--radius-card) bg-surface-2 p-8 sm:p-12">
      <h3 className="text-2xl font-bold">{title}</h3>
      {children && <div className="max-w-xl text-ink-soft">{children}</div>}
      {action}
    </div>
  )
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div role="alert" className="flex flex-col items-start gap-4 rounded-(--radius-card) bg-danger-50 p-8">
      <h3 className="text-xl font-bold text-danger">Không tải được dữ liệu</h3>
      <p className="text-ink">{message}</p>
      {onRetry && <button type="button" className="btn-primary" onClick={onRetry}>Thử lại</button>}
    </div>
  )
}

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  error?: string
  hint?: string
}

/** Ô nhập có nhãn, lỗi và gợi ý gắn với aria-describedby. */
export function Field({ label, error, hint, id, className = '', ...props }: FieldProps) {
  const autoId = useId()
  const inputId = id ?? autoId
  const describedBy = [hint && `${inputId}-hint`, error && `${inputId}-error`].filter(Boolean).join(' ') || undefined
  return (
    <div className={`flex flex-col gap-1.5 ${className}`}>
      <label htmlFor={inputId} className="text-sm font-semibold text-ink">{label}</label>
      <input
        id={inputId}
        aria-invalid={!!error}
        aria-describedby={describedBy}
        className="input"
        {...props}
      />
      {hint && !error && <p id={`${inputId}-hint`} className="text-sm text-ink-muted">{hint}</p>}
      {error && <p id={`${inputId}-error`} className="text-sm font-medium text-danger">{error}</p>}
    </div>
  )
}

interface TextAreaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string
  error?: string
}

export function TextArea({ label, error, id, ...props }: TextAreaProps) {
  const autoId = useId()
  const inputId = id ?? autoId
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-sm font-semibold text-ink">{label}</label>
      <textarea id={inputId} aria-invalid={!!error} className="input min-h-28 py-3" {...props} />
      {error && <p className="text-sm font-medium text-danger">{error}</p>}
    </div>
  )
}

export function Pagination({ page, totalPages, onChange }: { page: number; totalPages: number; onChange: (page: number) => void }) {
  if (totalPages <= 1) return null
  const pages = Array.from({ length: totalPages }, (_, i) => i + 1).filter(
    (p) => p === 1 || p === totalPages || Math.abs(p - page) <= 1,
  )
  return (
    <nav className="flex flex-wrap items-center gap-2" aria-label="Phân trang">
      <button type="button" className="btn-ghost" disabled={page <= 1} onClick={() => onChange(page - 1)}>Trước</button>
      {pages.map((p, i) => (
        <span key={p} className="inline-flex items-center gap-2">
          {i > 0 && pages[i - 1] !== p - 1 && <span className="text-ink-muted">…</span>}
          <button
            type="button"
            aria-current={p === page ? 'page' : undefined}
            className={p === page ? 'btn-primary min-w-11 px-0' : 'btn-ghost min-w-11 px-0'}
            onClick={() => onChange(p)}
          >
            {p}
          </button>
        </span>
      ))}
      <button type="button" className="btn-ghost" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>Sau</button>
    </nav>
  )
}

const statusStyles: Record<string, string> = {
  Paid: 'bg-success-50 text-success',
  Pending: 'bg-warning-50 text-warning',
  Cancelled: 'bg-surface-3 text-ink-soft',
  Expired: 'bg-surface-3 text-ink-soft',
  Refunded: 'bg-danger-50 text-danger',
}
const statusLabels: Record<string, string> = {
  Paid: 'Đã thanh toán',
  Pending: 'Chờ thanh toán',
  Cancelled: 'Đã hủy',
  Expired: 'Hết hạn',
  Refunded: 'Đã hoàn tiền',
}

export function OrderStatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center rounded-(--radius-control) px-3 py-1 text-sm font-semibold ${statusStyles[status] ?? 'bg-surface-3'}`}>
      {statusLabels[status] ?? status}
    </span>
  )
}
