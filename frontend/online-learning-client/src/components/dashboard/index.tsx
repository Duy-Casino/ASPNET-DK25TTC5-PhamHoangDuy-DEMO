import { useEffect, type ReactNode } from 'react'
import { X } from 'lucide-react'
import {
  Area, AreaChart, Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import { formatNumber } from '../../utils/format'

/** Tiêu đề trang dashboard: tên trang + hành động bên phải. */
export function PageHeader({ title, description, actions }: { title: string; description?: string; actions?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-3xl font-extrabold tracking-tight">{title}</h1>
        {description && <p className="mt-2 text-ink-soft">{description}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}

export function Panel({ title, actions, children, className = '' }: { title?: string; actions?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={`rounded-(--radius-card) bg-white p-5 sm:p-6 ${className}`}>
      {(title || actions) && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          {title && <h2 className="text-lg font-bold">{title}</h2>}
          {actions}
        </div>
      )}
      {children}
    </section>
  )
}

/** Ô số liệu: một con số, một nhãn, một dòng phụ. */
export function StatCard({ label, value, sub, tone = 'default' }: { label: string; value: string; sub?: string; tone?: 'default' | 'brand' | 'accent' }) {
  const toneClass = tone === 'brand' ? 'bg-brand-600 text-white' : tone === 'accent' ? 'bg-sun-100' : 'bg-white'
  return (
    <div className={`min-w-0 rounded-(--radius-card) p-5 [container-type:inline-size] ${toneClass}`}>
      <p className={`text-sm ${tone === 'brand' ? 'text-brand-100' : 'text-ink-soft'}`}>{label}</p>
      <p className="mt-2 text-[clamp(1.1rem,13cqi,1.875rem)] leading-tight font-extrabold tracking-tight whitespace-nowrap tabular-nums">{value}</p>
      {sub && <p className={`mt-1 text-sm ${tone === 'brand' ? 'text-brand-100' : 'text-ink-muted'}`}>{sub}</p>}
    </div>
  )
}

export function ProgressBar({ value, label }: { value: number; label?: string }) {
  const pct = Math.max(0, Math.min(100, value))
  return (
    <div className="flex items-center gap-3">
      <span className="h-2 flex-1 overflow-hidden rounded-full bg-surface-3" role="progressbar" aria-valuenow={Math.round(pct)} aria-valuemin={0} aria-valuemax={100} aria-label={label ?? 'Tiến độ'}>
        <span className="block h-full rounded-full bg-brand-600 transition-[width] duration-500" style={{ width: `${pct}%` }} />
      </span>
      <span className="w-10 text-right text-sm font-semibold tabular-nums">{Math.round(pct)}%</span>
    </div>
  )
}

const badgeStyles: Record<string, string> = {
  Draft: 'bg-surface-3 text-ink-soft',
  Pending: 'bg-warning-50 text-warning',
  Published: 'bg-success-50 text-success',
  Rejected: 'bg-danger-50 text-danger',
  Active: 'bg-brand-50 text-brand-800',
  Completed: 'bg-success-50 text-success',
}
const badgeLabels: Record<string, string> = {
  Draft: 'Bản nháp', Pending: 'Chờ duyệt', Published: 'Đã xuất bản', Rejected: 'Bị từ chối', Active: 'Đang học', Completed: 'Hoàn thành',
}
export function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center rounded-(--radius-control) px-2.5 py-0.5 text-sm font-semibold whitespace-nowrap ${badgeStyles[status] ?? 'bg-surface-3'}`}>
      {badgeLabels[status] ?? status}
    </span>
  )
}

/** Hộp thoại: đóng bằng Esc, nút X hoặc bấm ra ngoài. */
export function Modal({ title, onClose, children, wide = false }: { title: string; onClose: () => void; children: ReactNode; wide?: boolean }) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKey)
    document.body.style.overflow = 'hidden'
    return () => {
      document.removeEventListener('keydown', onKey)
      document.body.style.overflow = ''
    }
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center sm:items-center sm:p-6" role="dialog" aria-modal="true" aria-label={title}>
      <button type="button" className="absolute inset-0 bg-ink/50" aria-label="Đóng" onClick={onClose} />
      <div className={`rise-in relative max-h-[92svh] w-full overflow-y-auto rounded-t-3xl bg-white p-6 shadow-[0_20px_60px_rgb(11_27_51/0.25)] sm:rounded-(--radius-card) sm:p-8 ${wide ? 'sm:max-w-3xl' : 'sm:max-w-lg'}`}
        style={{ animationDuration: '220ms' }}>
        <div className="mb-6 flex items-start justify-between gap-4">
          <h2 className="text-xl font-extrabold">{title}</h2>
          <button type="button" onClick={onClose} className="rounded-full p-2 hover:bg-surface-2" aria-label="Đóng"><X className="size-5" /></button>
        </div>
        {children}
      </div>
    </div>
  )
}

/** Bảng dữ liệu: cuộn ngang trên màn hình hẹp. */
export function DataTable({ head, children, minWidth = 720 }: { head: ReactNode; children: ReactNode; minWidth?: number }) {
  return (
    <div className="-mx-5 overflow-x-auto px-5 sm:-mx-6 sm:px-6">
      <table className="w-full text-left text-sm" style={{ minWidth }}>
        <thead className="text-ink-soft">
          <tr className="border-b border-line [&>th]:py-3 [&>th]:pr-4 [&>th]:font-semibold">{head}</tr>
        </thead>
        <tbody className="[&>tr]:border-b [&>tr]:border-line [&>tr>td]:py-3 [&>tr>td]:pr-4 [&>tr]:align-middle">{children}</tbody>
      </table>
    </div>
  )
}

const axis = { fontSize: 12, fill: '#6b7686' }
const compactVnd = (v: number) => (v >= 1_000_000 ? `${(v / 1_000_000).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} tr` : v >= 1000 ? `${Math.round(v / 1000)}k` : String(v))
const monthLabel = (m: string) => `T${Number(m.slice(5))}/${m.slice(2, 4)}`

/** Biểu đồ vùng doanh thu theo tháng (một chuỗi, màu thương hiệu). */
export function RevenueChart({ data }: { data: { month: string; revenue: number }[] }) {
  return (
    <div className="h-72" role="img" aria-label="Biểu đồ doanh thu 12 tháng">
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid vertical={false} stroke="#e8eef7" />
          <XAxis dataKey="month" tickFormatter={monthLabel} tick={axis} axisLine={false} tickLine={false} />
          <YAxis tickFormatter={compactVnd} tick={axis} axisLine={false} tickLine={false} width={48} />
          <Tooltip
            formatter={(v) => [`${formatNumber(Number(v))}đ`, 'Doanh thu']}
            labelFormatter={(m) => `Tháng ${monthLabel(String(m)).slice(1)}`}
            contentStyle={{ borderRadius: 12, border: 'none', boxShadow: '0 8px 30px rgb(11 27 51 / 0.15)' }}
          />
          <Area type="monotone" dataKey="revenue" stroke="#005cf8" strokeWidth={2.5} fill="#dceaff" />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  )
}

/** Biểu đồ cột số lượng theo tháng. */
export function CountChart({ data, dataKey, label }: { data: Record<string, string | number>[]; dataKey: string; label: string }) {
  return (
    <div className="h-64" role="img" aria-label={`Biểu đồ ${label.toLowerCase()} 12 tháng`}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
          <CartesianGrid vertical={false} stroke="#e8eef7" />
          <XAxis dataKey="month" tickFormatter={monthLabel} tick={axis} axisLine={false} tickLine={false} />
          <YAxis allowDecimals={false} tick={axis} axisLine={false} tickLine={false} width={32} />
          <Tooltip
            formatter={(v) => [formatNumber(Number(v)), label]}
            labelFormatter={(m) => `Tháng ${monthLabel(String(m)).slice(1)}`}
            cursor={{ fill: '#eff6ff' }}
            contentStyle={{ borderRadius: 12, border: 'none', boxShadow: '0 8px 30px rgb(11 27 51 / 0.15)' }}
          />
          <Bar dataKey={dataKey} fill="#ef8000" radius={[6, 6, 0, 0]} maxBarSize={28} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

export function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (v: boolean) => void; label: string }) {
  return (
    <label className="inline-flex cursor-pointer items-center gap-3">
      <input type="checkbox" className="peer sr-only" checked={checked} onChange={(e) => onChange(e.target.checked)} />
      <span className="relative h-6 w-11 rounded-full bg-surface-3 transition-colors peer-checked:bg-brand-600 peer-focus-visible:ring-4 peer-focus-visible:ring-brand-100 after:absolute after:top-0.5 after:left-0.5 after:size-5 after:rounded-full after:bg-white after:transition-transform peer-checked:after:translate-x-5" aria-hidden="true" />
      <span className="text-sm font-medium">{label}</span>
    </label>
  )
}

export function SelectField({ label, value, onChange, children, id }: { label: string; value: string; onChange: (v: string) => void; children: ReactNode; id?: string }) {
  return (
    <label className="flex flex-col gap-1.5" htmlFor={id}>
      <span className="text-sm font-semibold">{label}</span>
      <select id={id} value={value} onChange={(e) => onChange(e.target.value)} className="input">{children}</select>
    </label>
  )
}
