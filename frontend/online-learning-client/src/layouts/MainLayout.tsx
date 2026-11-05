import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { LogOut, Menu, Search, ShoppingCart, User as UserIcon, X } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, Logo } from '../components/ui'
import { useAuth, useCart } from '../hooks/useAuth'

function SearchBox({ onDone }: { onDone?: () => void }) {
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
    <form role="search" onSubmit={submit} className="relative w-full">
      <label htmlFor="site-search" className="sr-only">Tìm khóa học</label>
      <Search className="pointer-events-none absolute top-1/2 left-4 size-5 -translate-y-1/2 text-ink-muted" aria-hidden="true" />
      <input
        id="site-search"
        type="search"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Tìm khóa học, giảng viên…"
        className="min-h-11 w-full rounded-(--radius-control) bg-surface-2 pr-4 pl-12 text-base text-ink placeholder:text-ink-muted focus:bg-white focus:ring-4 focus:ring-brand-100 focus:outline-none"
      />
    </form>
  )
}

function UserMenu() {
  const { user, logout } = useAuth()
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const navigate = useNavigate()

  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent | KeyboardEvent) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !ref.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [open])

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
        className="flex items-center gap-2 rounded-(--radius-control) p-1 transition-transform active:scale-95"
      >
        <Avatar name={user.fullName} src={user.avatarUrl} size={38} />
        <span className="sr-only">Tài khoản của {user.fullName}</span>
      </button>
      {open && (
        <div
          role="menu"
          className="rise-in absolute right-0 z-50 mt-2 w-64 origin-top-right rounded-(--radius-card) bg-white p-2 shadow-[0_12px_40px_rgb(11_27_51/0.16)]"
          style={{ animationDuration: '180ms' }}
        >
          <div className="px-3 py-2">
            <p className="font-semibold">{user.fullName}</p>
            <p className="truncate text-sm text-ink-muted">{user.email}</p>
          </div>
          <MenuLink to="/hoc-vien" onClick={() => setOpen(false)}>Khóa học của tôi</MenuLink>
          {(user.roles.includes('Instructor') || user.roles.includes('Admin')) && (
            <MenuLink to="/giang-day" onClick={() => setOpen(false)}>Khu vực giảng dạy</MenuLink>
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

function MenuLink({ to, children, onClick }: { to: string; children: string; onClick: () => void }) {
  return (
    <Link role="menuitem" to={to} onClick={onClick} className="block rounded-lg px-3 py-2.5 hover:bg-surface-2">
      {children}
    </Link>
  )
}

function Header() {
  const { isAuthenticated, initializing } = useAuth()
  const { cart } = useCart()
  const [mobileOpen, setMobileOpen] = useState(false)
  const location = useLocation()

  useEffect(() => {
    setMobileOpen(false)
  }, [location.pathname])

  const navClass = ({ isActive }: { isActive: boolean }) =>
    `rounded-(--radius-control) px-4 py-2 font-semibold transition-colors ${isActive ? 'bg-brand-50 text-brand-700' : 'text-ink hover:bg-surface-2'}`

  return (
    <header className="sticky top-0 z-40 bg-white/95 backdrop-saturate-150">
      <div className="container-page flex h-18 items-center gap-4">
        <Logo />
        <nav className="hidden items-center gap-1 lg:flex" aria-label="Chính">
          <NavLink to="/khoa-hoc" className={navClass}>Khóa học</NavLink>
          <NavLink to="/xac-minh" className={navClass}>Xác minh chứng chỉ</NavLink>
        </nav>
        <div className="hidden max-w-md flex-1 md:block">
          <SearchBox />
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Link to="/gio-hang" className="relative rounded-(--radius-control) p-2.5 transition-colors hover:bg-surface-2 active:scale-95" aria-label={`Giỏ hàng, ${cart.count} khóa học`}>
            <ShoppingCart className="size-6" aria-hidden="true" />
            {cart.count > 0 && (
              <span className="absolute -top-0.5 -right-0.5 flex size-5 items-center justify-center rounded-full bg-accent-500 text-xs font-bold text-ink">
                {cart.count}
              </span>
            )}
          </Link>
          {!initializing && (isAuthenticated ? (
            <UserMenu />
          ) : (
            <div className="hidden items-center gap-2 sm:flex">
              <Link to="/dang-nhap" className="btn-ghost">Đăng nhập</Link>
              <Link to="/dang-ky" className="btn-primary">Đăng ký</Link>
            </div>
          ))}
          <button
            type="button"
            className="rounded-(--radius-control) p-2.5 hover:bg-surface-2 lg:hidden"
            aria-label={mobileOpen ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={mobileOpen}
            onClick={() => setMobileOpen((o) => !o)}
          >
            {mobileOpen ? <X className="size-6" /> : <Menu className="size-6" />}
          </button>
        </div>
      </div>
      {mobileOpen && (
        <div className="container-page flex flex-col gap-3 pb-6 lg:hidden">
          <SearchBox onDone={() => setMobileOpen(false)} />
          <NavLink to="/khoa-hoc" className={navClass}>Khóa học</NavLink>
          <NavLink to="/xac-minh" className={navClass}>Xác minh chứng chỉ</NavLink>
          {!isAuthenticated && (
            <div className="flex gap-2">
              <Link to="/dang-nhap" className="btn-soft flex-1"><UserIcon className="size-4" aria-hidden="true" />Đăng nhập</Link>
              <Link to="/dang-ky" className="btn-primary flex-1">Đăng ký</Link>
            </div>
          )}
        </div>
      )}
    </header>
  )
}

/** Chân trang: mặt sau của tấm thẻ — mép trên cắt hình vỏ sò. */
function Footer() {
  return (
    <footer className="mt-auto bg-brand-700 pt-24 text-white">
      <div className="container-page">
        <div className="relative bg-brand-50 px-6 pt-16 pb-8 text-ink sm:px-12">
          <svg className="absolute -top-[23px] left-0 h-6 w-full text-brand-50" viewBox="0 0 1200 24" preserveAspectRatio="none" aria-hidden="true">
            <path fill="currentColor" d={`M0 24 ${Array.from({ length: 12 }, (_, i) => `A50 24 0 0 1 ${(i + 1) * 100} 24`).join(' ')} Z`} />
          </svg>
          <div className="grid gap-12 md:grid-cols-2">
            <div>
              <p className="text-3xl font-extrabold tracking-tight">Học hôm nay, dùng ngay ngày mai.</p>
              <p className="mt-4 max-w-md text-ink-soft">
                EduViet là nơi giảng viên Việt chia sẻ kinh nghiệm thực tế. Mỗi chứng chỉ có mã riêng để nhà tuyển dụng tự kiểm tra.
              </p>
              <div className="mt-6"><Logo /></div>
            </div>
            <div className="grid grid-cols-2 gap-8 text-base">
              <ul className="flex flex-col gap-3">
                <li className="font-bold">Khám phá</li>
                <li><Link className="hover:text-brand-700" to="/khoa-hoc">Tất cả khóa học</Link></li>
                <li><Link className="hover:text-brand-700" to="/khoa-hoc?sort=popular">Bán chạy</Link></li>
                <li><Link className="hover:text-brand-700" to="/khoa-hoc?free=true">Miễn phí</Link></li>
                <li><Link className="hover:text-brand-700" to="/xac-minh">Xác minh chứng chỉ</Link></li>
              </ul>
              <ul className="flex flex-col gap-3">
                <li className="font-bold">Liên hệ</li>
                <li><a className="hover:text-brand-700" href="mailto:hotro@eduviet.local">hotro@eduviet.local</a></li>
                <li><a className="hover:text-brand-700" href="tel:19001234">1900 1234</a></li>
                <li>TP. Hồ Chí Minh</li>
              </ul>
            </div>
          </div>
          <p className="mt-16 text-center text-sm text-ink-soft">
            © {new Date().getFullYear()} EduViet. Ảnh minh họa: Unsplash qua picsum.photos.
          </p>
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
