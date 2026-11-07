import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import {
  Award, BarChart3, CreditCard, BookOpen, ClipboardList, FolderTree, IdCard, LayoutDashboard, LogOut, Menu, MessageSquare, MessagesSquare, Receipt, ShieldCheck,
  Tag, User, Users, X, type LucideIcon,
} from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, Logo } from '../components/ui'
import { useAuth } from '../hooks/useAuth'
import { messagingService } from '../services/api'

interface NavItem { to: string; label: string; icon: LucideIcon; end?: boolean; badge?: 'messages' }

const areas: Record<'student' | 'instructor' | 'admin', { title: string; items: NavItem[] }> = {
  student: {
    title: 'Học viên',
    items: [
      { to: '/hoc-vien', label: 'Khóa học của tôi', icon: BookOpen, end: true },
      { to: '/hoc-vien/ket-qua', label: 'Kết quả kiểm tra', icon: ClipboardList },
      { to: '/hoc-vien/chung-chi', label: 'Chứng chỉ', icon: Award },
      { to: '/hoc-vien/tin-nhan', label: 'Tin nhắn', icon: MessagesSquare, badge: 'messages' },
      { to: '/tai-khoan/don-hang', label: 'Lịch sử thanh toán', icon: Receipt },
      { to: '/tai-khoan', label: 'Hồ sơ cá nhân', icon: User, end: true },
    ],
  },
  instructor: {
    title: 'Giảng dạy',
    items: [
      { to: '/giang-day', label: 'Tổng quan', icon: BarChart3, end: true },
      { to: '/giang-day/khoa-hoc', label: 'Khóa học', icon: BookOpen },
      { to: '/giang-day/hoc-vien', label: 'Học viên', icon: Users },
      { to: '/giang-day/danh-gia', label: 'Đánh giá', icon: MessageSquare },
      { to: '/giang-day/tin-nhan', label: 'Tin nhắn', icon: MessagesSquare, badge: 'messages' },
      { to: '/giang-day/ho-so', label: 'Hồ sơ giảng viên', icon: IdCard },
    ],
  },
  admin: {
    title: 'Quản trị',
    items: [
      { to: '/quan-tri', label: 'Tổng quan', icon: LayoutDashboard, end: true },
      { to: '/quan-tri/khoa-hoc', label: 'Duyệt khóa học', icon: ShieldCheck },
      { to: '/quan-tri/nguoi-dung', label: 'Người dùng', icon: Users },
      { to: '/quan-tri/danh-muc', label: 'Danh mục', icon: FolderTree },
      { to: '/quan-tri/don-hang', label: 'Đơn hàng', icon: Receipt },
      { to: '/quan-tri/giao-dich', label: 'Giao dịch', icon: CreditCard },
      { to: '/quan-tri/chung-chi', label: 'Chứng chỉ', icon: Award },
      { to: '/quan-tri/ma-giam-gia', label: 'Mã giảm giá', icon: Tag },
    ],
  },
}

/** Khung dashboard: thanh bên theo vai trò (học viên / giảng viên / admin), nền surface-2. */
export default function DashboardLayout({ area }: { area: keyof typeof areas }) {
  const { user, hasRole, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const { pathname } = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    setOpen(false)
    window.scrollTo(0, 0)
  }, [pathname])

  // Số cuộc trao đổi chưa đọc cho mục "Tin nhắn" (cập nhật khi đổi trang và mỗi phút)
  const [unread, setUnread] = useState(0)
  useEffect(() => {
    const load = () => messagingService.unreadCount().then((r) => setUnread(r.count)).catch(() => {})
    load()
    const id = window.setInterval(load, 60_000)
    return () => window.clearInterval(id)
  }, [pathname])

  const switchers = [
    { key: 'student', to: '/hoc-vien', label: 'Học viên', show: true },
    { key: 'instructor', to: '/giang-day', label: 'Giảng dạy', show: hasRole('Instructor', 'Admin') },
    { key: 'admin', to: '/quan-tri', label: 'Quản trị', show: hasRole('Admin') },
  ].filter((s) => s.show)

  const sidebar = (
    <div className="flex h-full flex-col gap-6 p-5">
      <div className="flex items-center justify-between"><Logo /></div>
      {switchers.length > 1 && (
        <div className="flex gap-1 rounded-(--radius-control) bg-surface-2 p-1" role="tablist" aria-label="Khu vực">
          {switchers.map((s) => (
            <Link key={s.key} to={s.to} role="tab" aria-selected={s.key === area}
              className={`flex-1 rounded-(--radius-control) px-1 py-1.5 text-center text-[13px] font-semibold whitespace-nowrap transition-colors ${s.key === area ? 'bg-white text-brand-700' : 'text-ink-soft hover:text-ink'}`}>
              {s.label}
            </Link>
          ))}
        </div>
      )}
      <nav aria-label={areas[area].title} className="flex flex-col gap-1">
        {areas[area].items.map(({ to, label, icon: Icon, end, badge }) => (
          <NavLink key={to} to={to} end={end}
            className={({ isActive }) => `flex items-center gap-3 rounded-xl px-3 py-2.5 font-medium transition-colors ${isActive ? 'bg-brand-50 font-semibold text-brand-700' : 'text-ink-soft hover:bg-surface-2 hover:text-ink'}`}>
            <Icon className="size-5" aria-hidden="true" />{label}
            {badge === 'messages' && unread > 0 && (
              <span className="ml-auto min-w-6 rounded-full bg-brand-600 px-1.5 text-center text-xs leading-6 font-bold text-white tabular-nums">
                {unread > 99 ? '99+' : unread}<span className="sr-only"> cuộc trao đổi chưa đọc</span>
              </span>
            )}
          </NavLink>
        ))}
      </nav>
      <div className="mt-auto flex flex-col gap-3">
        <Link to="/khoa-hoc" className="btn-soft">Xem trang khóa học</Link>
        {user && (
          <div className="flex items-center gap-3">
            <Avatar name={user.fullName} src={user.avatarUrl} size={40} />
            <div className="min-w-0 flex-1">
              <p className="truncate font-semibold">{user.fullName}</p>
              <p className="truncate text-sm text-ink-muted">{user.email}</p>
            </div>
            <button type="button" className="rounded-full p-2 text-ink-soft hover:bg-danger-50 hover:text-danger" aria-label="Đăng xuất"
              onClick={async () => { await logout(); toast.success('Đã đăng xuất'); navigate('/') }}>
              <LogOut className="size-5" />
            </button>
          </div>
        )}
      </div>
    </div>
  )

  return (
    <div className="min-h-svh bg-surface-2 lg:grid lg:grid-cols-[260px_1fr]">
      <aside className="sticky top-0 hidden h-svh border-r border-line/70 bg-white lg:block">{sidebar}</aside>
      <header className="sticky top-0 z-30 flex h-16 items-center gap-3 border-b border-line/70 bg-white px-4 lg:hidden">
        <button type="button" className="rounded-(--radius-control) p-2 hover:bg-surface-2" onClick={() => setOpen(true)} aria-label="Mở menu">
          <Menu className="size-6" />
        </button>
        <Logo />
        <span className="ml-auto text-sm font-semibold text-ink-soft">{areas[area].title}</span>
      </header>
      {open && (
        <div className="fixed inset-0 z-50 lg:hidden" role="dialog" aria-modal="true" aria-label="Menu">
          <button type="button" className="absolute inset-0 bg-ink/50" aria-label="Đóng menu" onClick={() => setOpen(false)} />
          <div className="rise-in absolute inset-y-0 left-0 w-[min(300px,85vw)] bg-white" style={{ animationDuration: '200ms' }}>
            <button type="button" className="absolute top-4 right-4 rounded-full p-2 hover:bg-surface-2" onClick={() => setOpen(false)} aria-label="Đóng"><X className="size-5" /></button>
            {sidebar}
          </div>
        </div>
      )}
      <main id="main" className="min-w-0 px-4 py-8 sm:px-8 lg:px-10 lg:py-10">
        <div className="mx-auto max-w-6xl"><Outlet /></div>
      </main>
    </div>
  )
}
