import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode, type RefObject } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import {
  ArrowRight, Bell, BookOpen, ChevronDown, GraduationCap, Home, LogOut, Mail, MapPin, Menu, Phone, Search, ShieldCheck, ShoppingCart, X,
} from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, DecorBlobs, Logo, Spinner } from '../components/ui'
import type { Notification } from '../types/api'
import { formatRelative } from '../utils/format'
import { useAsync } from '../hooks/useAsync'
import { useAuth, useCart } from '../hooks/useAuth'
import { site } from '../config/site'
import { catalogService, notificationService } from '../services/api'

/** Đóng một vùng nổi khi bấm ra ngoài hoặc nhấn Escape. */
function useDismiss(open: boolean, close: () => void, ref: RefObject<HTMLElement | null>) {
  useEffect(() => {
    if (!open) return
    const onDown = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) close() }
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') close() }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDown)
      document.removeEventListener('keydown', onKey)
    }
  }, [open, close, ref])
}

function SearchBox({ onDone, autoFocus = false }: { onDone?: () => void; autoFocus?: boolean }) {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [q, setQ] = useState(params.get('keyword') ?? '')

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const keyword = q.trim()
    navigate(keyword ? `/khoa-hoc?keyword=${encodeURIComponent(keyword)}` : '/khoa-hoc')
    onDone?.()
  }

  return (
    <form role="search" onSubmit={submit} className="flex w-full items-center gap-2">
      <label htmlFor="site-search" className="sr-only">Tìm khóa học</label>
      <div className="relative flex-1">
        <Search className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-ink-muted" aria-hidden="true" />
        <input
          id="site-search"
          type="search"
          value={q}
          autoFocus={autoFocus}
          onChange={(e) => setQ(e.target.value)}
          placeholder="Tìm khóa học, giảng viên, kỹ năng…"
          className="input pl-12"
        />
      </div>
      <button type="submit" className="btn-primary min-h-12 px-6">Tìm kiếm</button>
    </form>
  )
}

function UserMenu() {
  const { user, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  useDismiss(open, () => setOpen(false), ref)

  if (!user) return null

  const doLogout = async () => {
    setOpen(false)
    await logout()
    toast.success('Đã đăng xuất')
    navigate('/')
  }

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
        className="flex items-center gap-2 rounded-full p-1 transition-transform active:scale-95"
      >
        <Avatar name={user.fullName} src={user.avatarUrl} size={38} />
        <span className="sr-only">Tài khoản của {user.fullName}</span>
      </button>
      {open && (
        <div role="menu" className="rise-in card absolute right-0 z-50 mt-2 w-64 origin-top-right p-2 shadow-(--shadow-lift)" style={{ animationDuration: '180ms' }}>
          <div className="px-3 py-2">
            <p className="font-semibold">{user.fullName}</p>
            <p className="truncate text-sm text-ink-muted">{user.email}</p>
          </div>
          <MenuLink to="/hoc-vien" onClick={() => setOpen(false)}>Khóa học của tôi</MenuLink>
          <MenuLink to="/hoc-vien/tin-nhan" onClick={() => setOpen(false)}>Tin nhắn với giảng viên</MenuLink>
          {(user.roles.includes('Instructor') || user.roles.includes('Admin')) && (
            <>
              <MenuLink to="/giang-day" onClick={() => setOpen(false)}>Khu vực giảng dạy</MenuLink>
              <MenuLink to="/giang-day/tin-nhan" onClick={() => setOpen(false)}>Tin nhắn từ học viên</MenuLink>
            </>
          )}
          {user.roles.includes('Admin') && <MenuLink to="/quan-tri" onClick={() => setOpen(false)}>Quản trị hệ thống</MenuLink>}
          <MenuLink to="/tai-khoan" onClick={() => setOpen(false)}>Hồ sơ cá nhân</MenuLink>
          <MenuLink to="/tai-khoan/don-hang" onClick={() => setOpen(false)}>Lịch sử mua hàng</MenuLink>
          <button type="button" role="menuitem" onClick={doLogout} className="flex w-full items-center gap-2 rounded-lg px-3 py-2.5 text-left text-danger hover:bg-danger-50">
            <LogOut className="size-4" aria-hidden="true" /> Đăng xuất
          </button>
        </div>
      )}
    </div>
  )
}

/** Chuông thông báo: số chưa đọc (cập nhật mỗi phút, khi quay lại tab, khi đổi trang) + danh sách 10 thông báo mới nhất. */
function NotificationBell() {
  const [open, setOpen] = useState(false)
  const [count, setCount] = useState(0)
  const [items, setItems] = useState<Notification[] | null>(null)
  const [failed, setFailed] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()
  const { pathname } = useLocation()
  useDismiss(open, () => setOpen(false), ref)

  const refreshCount = useCallback(() => {
    notificationService.unreadCount().then((r) => setCount(r.count)).catch(() => { /* bỏ qua: chuông chỉ là phụ */ })
  }, [])

  useEffect(() => {
    refreshCount()
    const id = window.setInterval(refreshCount, 60_000)
    const onFocus = () => refreshCount()
    window.addEventListener('focus', onFocus)
    return () => { window.clearInterval(id); window.removeEventListener('focus', onFocus) }
  }, [refreshCount])
  useEffect(() => { refreshCount(); setOpen(false) }, [pathname, refreshCount])

  useEffect(() => {
    if (!open) return
    setFailed(false)
    notificationService.mine(1).then((r) => setItems(r.items)).catch(() => setFailed(true))
  }, [open])

  const openItem = async (n: Notification) => {
    if (!n.isRead) {
      setItems((list) => list?.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)) ?? null)
      setCount((c) => Math.max(0, c - 1))
      notificationService.markRead(n.id).catch(() => refreshCount())
    }
    setOpen(false)
    if (n.link) {
      if (/^https?:\/\//i.test(n.link)) window.location.assign(n.link)
      else navigate(n.link)
    }
  }

  const markAll = async () => {
    setItems((list) => list?.map((x) => ({ ...x, isRead: true })) ?? null)
    setCount(0)
    try { await notificationService.markAllRead() } catch { refreshCount() }
  }

  return (
    <div ref={ref} className="relative">
      <button type="button" aria-haspopup="dialog" aria-expanded={open} onClick={() => setOpen((o) => !o)}
        aria-label={count > 0 ? `Thông báo, ${count} chưa đọc` : 'Thông báo'}
        className="relative flex size-11 items-center justify-center rounded-full text-ink-soft transition-colors hover:bg-surface-2 hover:text-ink active:scale-95">
        <Bell className="size-5" aria-hidden="true" />
        {count > 0 && (
          <span className="absolute top-1 right-1 flex min-w-5 items-center justify-center rounded-full bg-danger px-1 text-[11px] leading-5 font-bold text-white tabular-nums">
            {count > 9 ? '9+' : count}
          </span>
        )}
      </button>
      {open && (
        <div role="dialog" aria-label="Thông báo" className="rise-in card absolute right-0 z-50 mt-2 w-[min(380px,calc(100vw-2rem))] origin-top-right overflow-hidden shadow-(--shadow-lift)" style={{ animationDuration: '180ms' }}>
          <div className="flex items-center justify-between gap-3 border-b border-line/70 px-4 py-3">
            <p className="font-display font-semibold">Thông báo</p>
            <button type="button" className="text-sm font-semibold text-brand-700 hover:underline disabled:text-ink-muted disabled:no-underline"
              onClick={markAll} disabled={!items?.some((n) => !n.isRead)}>
              Đánh dấu đã đọc tất cả
            </button>
          </div>
          <div className="max-h-[420px] overflow-y-auto">
            {failed ? (
              <p className="p-6 text-center text-ink-soft">Không tải được thông báo. Thử lại sau.</p>
            ) : items === null ? (
              <div className="flex justify-center p-8"><Spinner className="size-5 text-brand-600" /></div>
            ) : items.length === 0 ? (
              <div className="flex flex-col items-center gap-2 p-8 text-center">
                <Bell className="size-8 text-ink-muted" aria-hidden="true" />
                <p className="font-semibold">Chưa có thông báo</p>
                <p className="text-sm text-ink-soft">Thông báo về đơn hàng, khóa học và chứng chỉ sẽ hiện ở đây.</p>
              </div>
            ) : (
              <ul>
                {items.map((n) => (
                  <li key={n.id}>
                    <button type="button" onClick={() => openItem(n)}
                      className={`flex w-full gap-3 border-b border-line/50 px-4 py-3 text-left transition-colors hover:bg-surface-2 ${n.isRead ? '' : 'bg-brand-50/60'}`}>
                      <span className={`mt-2 size-2 shrink-0 rounded-full ${n.isRead ? 'bg-transparent' : 'bg-brand-600'}`} aria-hidden="true" />
                      <span className="min-w-0 flex-1">
                        <span className="block font-semibold">{n.title}<span className="sr-only">{n.isRead ? '' : ' (chưa đọc)'}</span></span>
                        <span className="mt-0.5 line-clamp-2 block text-sm text-ink-soft">{n.message}</span>
                        <span className="mt-1 block text-xs text-ink-muted">{formatRelative(n.createdAt)}</span>
                      </span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function MenuLink({ to, children, onClick }: { to: string; children: string; onClick: () => void }) {
  return (
    <Link role="menuitem" to={to} onClick={onClick} className="block rounded-lg px-3 py-2.5 hover:bg-surface-2">
      {children}
    </Link>
  )
}

/** Mục điều hướng: biểu tượng + chữ, mục đang mở có gạch chân mint. */
const navItem = (active: boolean) =>
  `relative inline-flex min-h-11 items-center gap-2 px-3 text-[15px] font-medium transition-colors after:absolute after:inset-x-3 after:bottom-1 after:h-[3px] after:rounded-full after:transition-colors ${
    active ? 'text-brand-700 after:bg-mint-500' : 'text-ink-soft hover:text-ink after:bg-transparent'
  }`

function CoursesMenu() {
  const { data } = useAsync(() => catalogService.categories(), [])
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const hoverAt = useRef(0)
  const { pathname } = useLocation()
  useDismiss(open, () => setOpen(false), ref)
  useEffect(() => setOpen(false), [pathname])

  const categories = (data ?? []).filter((c) => c.parentId === null && c.isActive)
  return (
    <div ref={ref} className="relative" onMouseLeave={() => setOpen(false)}>
      <button
        type="button"
        aria-expanded={open}
        aria-controls="courses-menu"
        onClick={() => {
          // Vừa mở bằng hover thì cú click ngay sau đó giữ menu mở thay vì đóng lại
          if (Date.now() - hoverAt.current < 400) return
          setOpen((o) => !o)
        }}
        onMouseEnter={() => { if (!open) hoverAt.current = Date.now(); setOpen(true) }}
        className={navItem(pathname.startsWith('/khoa-hoc'))}
      >
        <GraduationCap className="size-[18px]" aria-hidden="true" /> Khóa học
        <ChevronDown className={`size-4 transition-transform ${open ? 'rotate-180' : ''}`} aria-hidden="true" />
      </button>
      {open && (
        <div id="courses-menu" className="absolute top-full left-0 z-50 pt-2">
          <div className="rise-in card w-[520px] p-3 shadow-(--shadow-lift)" style={{ animationDuration: '180ms' }}>
            <div className="grid grid-cols-2 gap-1">
              {categories.map((c) => (
                <Link key={c.id} to={`/khoa-hoc?category=${c.slug}`} className="flex items-center justify-between gap-3 rounded-lg px-3 py-2.5 hover:bg-surface-2">
                  <span className="font-medium">{c.name}</span>
                  <span className="text-xs text-ink-muted tabular-nums">{c.courseCount} khóa</span>
                </Link>
              ))}
            </div>
            <div className="mt-2 flex gap-2 border-t border-line/70 pt-3">
              <Link to="/khoa-hoc" className="btn-soft flex-1">Tất cả khóa học</Link>
              <Link to="/khoa-hoc?free=true" className="btn-outline flex-1">Khóa miễn phí</Link>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function Header() {
  const { isAuthenticated, initializing } = useAuth()
  const { cart } = useCart()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [searchOpen, setSearchOpen] = useState(false)
  const [scrolled, setScrolled] = useState(false)
  const location = useLocation()
  const { data: categories } = useAsync(() => catalogService.categories(), [])

  useEffect(() => {
    setMobileOpen(false)
    setSearchOpen(false)
  }, [location.pathname, location.search])

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [])

  useEffect(() => {
    if (!searchOpen) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') setSearchOpen(false) }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [searchOpen])

  const iconBtn = 'relative flex size-11 items-center justify-center rounded-full text-ink-soft transition-colors hover:bg-surface-2 hover:text-ink active:scale-95'
  const mobileLink = ({ isActive }: { isActive: boolean }) =>
    `flex min-h-12 items-center gap-3 rounded-xl px-3 font-medium ${isActive ? 'bg-brand-50 text-brand-700' : 'text-ink hover:bg-surface-2'}`

  return (
    <header className={`sticky top-0 z-40 bg-white/90 backdrop-blur-md transition-shadow duration-300 ${scrolled ? 'shadow-(--shadow-soft)' : 'border-b border-line/70'}`}>
      <div className="container-page flex h-[72px] items-center gap-6">
        <Logo />
        <nav className="hidden items-center gap-1 lg:flex" aria-label="Chính">
          <NavLink to="/" end className={({ isActive }) => navItem(isActive)}>
            <Home className="size-[18px]" aria-hidden="true" /> Trang chủ
          </NavLink>
          <CoursesMenu />
          <NavLink to="/xac-minh" className={({ isActive }) => navItem(isActive)}>
            <ShieldCheck className="size-[18px]" aria-hidden="true" /> Xác minh chứng chỉ
          </NavLink>
          {isAuthenticated && (
            <NavLink to="/hoc-vien" className={({ isActive }) => navItem(isActive)}>
              <BookOpen className="size-[18px]" aria-hidden="true" /> Khóa học của tôi
            </NavLink>
          )}
        </nav>

        <div className="ml-auto flex items-center gap-1 sm:gap-2">
          <button type="button" className={iconBtn} aria-label={searchOpen ? 'Đóng tìm kiếm' : 'Mở tìm kiếm'} aria-expanded={searchOpen} aria-controls="search-panel"
            onClick={() => setSearchOpen((o) => !o)}>
            {searchOpen ? <X className="size-5" aria-hidden="true" /> : <Search className="size-5" aria-hidden="true" />}
          </button>
          <Link to="/gio-hang" className={iconBtn} aria-label={`Giỏ hàng, ${cart.count} khóa học`}>
            <ShoppingCart className="size-5" aria-hidden="true" />
            {cart.count > 0 && (
              <span className="absolute top-0.5 right-0.5 flex size-5 items-center justify-center rounded-full bg-accent-500 text-xs font-bold text-ink">{cart.count}</span>
            )}
          </Link>
          {!initializing && isAuthenticated && <NotificationBell />}
          {!initializing && (isAuthenticated ? (
            <UserMenu />
          ) : (
            <div className="hidden items-center gap-2 sm:flex">
              <Link to="/dang-nhap" className="btn-ghost">Đăng nhập</Link>
              <Link to="/dang-ky" className="btn-primary">Đăng ký học</Link>
            </div>
          ))}
          <button type="button" className={`${iconBtn} lg:hidden`} aria-label={mobileOpen ? 'Đóng menu' : 'Mở menu'} aria-expanded={mobileOpen}
            onClick={() => setMobileOpen((o) => !o)}>
            {mobileOpen ? <X className="size-6" /> : <Menu className="size-6" />}
          </button>
        </div>
      </div>

      {searchOpen && (
        <div id="search-panel" className="rise-in border-t border-line/70 bg-white" style={{ animationDuration: '200ms' }}>
          <div className="container-page py-4"><SearchBox autoFocus onDone={() => setSearchOpen(false)} /></div>
        </div>
      )}

      {mobileOpen && (
        <nav className="max-h-[calc(100svh-72px)] overflow-y-auto border-t border-line/70 bg-white lg:hidden" aria-label="Menu điện thoại">
          <div className="container-page flex flex-col gap-1 py-4">
            <NavLink to="/" end className={mobileLink}><Home className="size-5" aria-hidden="true" />Trang chủ</NavLink>
            <NavLink to="/khoa-hoc" end className={mobileLink}><GraduationCap className="size-5" aria-hidden="true" />Tất cả khóa học</NavLink>
            <NavLink to="/xac-minh" className={mobileLink}><ShieldCheck className="size-5" aria-hidden="true" />Xác minh chứng chỉ</NavLink>
            {isAuthenticated && <NavLink to="/hoc-vien" className={mobileLink}><BookOpen className="size-5" aria-hidden="true" />Khóa học của tôi</NavLink>}
            {categories && categories.length > 0 && (
              <>
                <p className="mt-3 px-3 text-sm font-semibold text-ink-muted">Lĩnh vực</p>
                <div className="flex flex-wrap gap-2 px-3 pt-1">
                  {categories.filter((c) => c.parentId === null && c.isActive).map((c) => (
                    <Link key={c.id} to={`/khoa-hoc?category=${c.slug}`} className="chip min-h-9 px-3.5 text-sm">{c.name}</Link>
                  ))}
                </div>
              </>
            )}
            {!isAuthenticated && !initializing && (
              <div className="mt-4 flex gap-2">
                <Link to="/dang-nhap" className="btn-outline flex-1">Đăng nhập</Link>
                <Link to="/dang-ky" className="btn-primary flex-1">Đăng ký học</Link>
              </div>
            )}
          </div>
        </nav>
      )}
    </header>
  )
}

/** Dải mời hành động phía trên chân trang (ẩn ở các bước giỏ hàng/thanh toán để không làm rối). */
function CtaBand() {
  const { isAuthenticated, initializing } = useAuth()
  return (
    <section className="container-page pt-20" aria-labelledby="cta-band-title">
      <div className="relative isolate flex flex-col gap-6 overflow-hidden rounded-[24px] bg-surface-2 px-6 py-10 sm:px-12 md:flex-row md:items-center md:justify-between">
        <DecorBlobs variant="left" />
        <div className="max-w-xl">
          <h2 id="cta-band-title" className="text-2xl font-semibold sm:text-3xl">EduViet đồng hành cùng bạn trên hành trình học tập</h2>
          <p className="mt-3 text-ink-soft">Học theo tốc độ của bạn, làm bài kiểm tra và nhận chứng chỉ có mã xác minh khi hoàn thành.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <Link to="/khoa-hoc" className="btn-primary min-h-12 px-6">Khám phá khóa học <ArrowRight className="size-4" aria-hidden="true" /></Link>
          {!initializing && !isAuthenticated && <Link to="/dang-ky" className="btn-outline min-h-12 px-6">Đăng ký miễn phí</Link>}
        </div>
      </div>
    </section>
  )
}

function FooterCol({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div>
      <p className="font-display font-semibold">{title}</p>
      <ul className="mt-4 flex flex-col gap-3 text-ink-soft">{children}</ul>
    </div>
  )
}

function Footer() {
  const { pathname } = useLocation()
  const hideCta = ['/gio-hang', '/thanh-toan', '/don-hang'].some((p) => pathname.startsWith(p))
  const lnk = 'transition-colors hover:text-brand-700'
  return (
    <footer className="mt-auto">
      {!hideCta && <CtaBand />}
      <div className="container-page grid gap-10 py-14 sm:grid-cols-2 lg:grid-cols-[1.4fr_1fr_1fr_1fr]">
        <div className="flex flex-col gap-4">
          <Logo />
          <p className="max-w-sm text-ink-soft">
            Nền tảng khóa học trực tuyến của giảng viên Việt. Mỗi chứng chỉ có mã riêng để nhà tuyển dụng tự kiểm tra.
          </p>
          <ul className="flex flex-col gap-2.5 text-ink-soft">
            <li><a className={`inline-flex items-center gap-2 ${lnk}`} href={`mailto:${site.email}`}><Mail className="size-4 text-brand-600" aria-hidden="true" />{site.email}</a></li>
            <li><a className={`inline-flex items-center gap-2 ${lnk}`} href={site.phoneHref}><Phone className="size-4 text-brand-600" aria-hidden="true" />{site.phone}</a></li>
            <li className="inline-flex items-center gap-2"><MapPin className="size-4 text-brand-600" aria-hidden="true" />{site.address}</li>
          </ul>
          {site.social.length > 0 && (
            <ul className="flex flex-wrap gap-2">
              {site.social.map((s) => (
                <li key={s.href}><a href={s.href} target="_blank" rel="noopener noreferrer" className="chip min-h-9 px-3.5 text-sm">{s.label}</a></li>
              ))}
            </ul>
          )}
        </div>
        <FooterCol title="Khám phá">
          <li><Link className={lnk} to="/khoa-hoc">Tất cả khóa học</Link></li>
          <li><Link className={lnk} to="/khoa-hoc?sort=popular">Bán chạy</Link></li>
          <li><Link className={lnk} to="/khoa-hoc?free=true">Khóa miễn phí</Link></li>
          <li><Link className={lnk} to="/xac-minh">Xác minh chứng chỉ</Link></li>
        </FooterCol>
        <FooterCol title="Về EduViet">
          <li><Link className={lnk} to="/gioi-thieu">Giới thiệu</Link></li>
          <li><Link className={lnk} to="/lien-he">Liên hệ</Link></li>
          <li><Link className={lnk} to="/hoc-vien">Khóa học của tôi</Link></li>
          <li><Link className={lnk} to="/tai-khoan/don-hang">Lịch sử mua hàng</Link></li>
        </FooterCol>
        <FooterCol title="Chính sách">
          <li><Link className={lnk} to="/chinh-sach/dieu-khoan">Điều khoản sử dụng</Link></li>
          <li><Link className={lnk} to="/chinh-sach/hoan-tien">Chính sách hoàn tiền</Link></li>
          <li><Link className={lnk} to="/chinh-sach/bao-mat">Chính sách bảo mật</Link></li>
          <li>Thanh toán VNPay · MoMo</li>
        </FooterCol>
      </div>
      <div className="border-t border-line/70">
        <div className="container-page flex flex-col gap-2 py-5 text-sm text-ink-muted sm:flex-row sm:items-center sm:justify-between">
          <p>© {new Date().getFullYear()} {site.name}. Bảo lưu mọi quyền.</p>
          <p>Ảnh minh họa: Unsplash qua picsum.photos</p>
        </div>
      </div>
    </footer>
  )
}

export default function MainLayout() {
  const { pathname } = useLocation()
  useEffect(() => {
    window.scrollTo(0, 0)
  }, [pathname])

  return (
    <div className="flex min-h-svh flex-col">
      <a href="#main" className="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-white focus:p-3">
        Bỏ qua tới nội dung
      </a>
      <Header />
      <main id="main" className="flex-1">
        <Outlet />
      </main>
      <Footer />
    </div>
  )
}
