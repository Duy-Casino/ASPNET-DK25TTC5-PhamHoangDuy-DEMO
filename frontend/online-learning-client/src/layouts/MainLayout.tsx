import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router-dom'
import { BookOpen, LogOut, Menu, Search, ShoppingCart, User as UserIcon, X } from 'lucide-react'
import { toast } from 'sonner'
import { Avatar, Logo } from '../components/ui'
import { useAsync } from '../hooks/useAsync'
import { useAuth, useCart } from '../hooks/useAuth'
import { catalogService } from '../services/api'

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
    <form role="search" onSubmit={submit} className="flex w-full overflow-hidden rounded-xl bg-white">
      <label htmlFor="site-search" className="sr-only">Tìm khóa học</label>
      <Search className="ml-4 size-5 shrink-0 self-center text-ink-muted" aria-hidden="true" />
      <input
        id="site-search"
        type="search"
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Bạn muốn học gì hôm nay?"
        className="min-h-11 min-w-0 flex-1 bg-transparent px-3 text-base text-ink placeholder:text-ink-muted focus:outline-none"
      />
      <button type="submit" className="border-l border-line px-4 text-sm font-bold text-ink transition-colors hover:bg-surface-2 sm:px-5">
        Tìm kiếm
      </button>
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

  const topLink = 'inline-flex min-h-11 items-center gap-2 rounded-(--radius-control) px-3 text-sm font-semibold text-white transition-colors hover:bg-white/15'
  const myCourses = isAuthenticated ? '/hoc-vien' : '/dang-nhap'

  return (
    <header className="sticky top-0 z-40 bg-brand-600 text-white">
      <div className="container-page flex flex-wrap items-center gap-x-4 gap-y-3 py-3">
        <Logo inverted />
        {/* Ô tìm kiếm luôn hiện — xuống hàng riêng trên điện thoại */}
        <div className="order-last w-full md:order-none md:w-auto md:max-w-xl md:flex-1">
          <SearchBox />
        </div>
        <div className="ml-auto flex items-center gap-1">
          <Link to="/xac-minh" className="hidden min-h-11 items-center rounded-(--radius-control) border-[1.5px] border-white px-4 text-sm font-bold text-white transition-colors hover:bg-white hover:text-brand-700 xl:inline-flex">
            Xác minh chứng chỉ
          </Link>
          <Link to={myCourses} className="hidden min-h-11 items-center gap-2 rounded-(--radius-control) px-3 text-sm font-semibold text-white transition-colors hover:bg-white/15 lg:inline-flex">
            <BookOpen className="size-5" aria-hidden="true" /> Khóa học của tôi
          </Link>
          <Link to="/gio-hang" className="relative rounded-(--radius-control) p-2.5 text-white transition-colors hover:bg-white/15 active:scale-95" aria-label={`Giỏ hàng, ${cart.count} khóa học`}>
            <ShoppingCart className="size-6" aria-hidden="true" />
            {cart.count > 0 && (
              <span className="absolute -top-0.5 -right-0.5 flex size-5 items-center justify-center rounded-full bg-sun-300 text-xs font-bold text-ink">
                {cart.count}
              </span>
            )}
          </Link>
          {!initializing && (isAuthenticated ? (
            <UserMenu />
          ) : (
            <span className="hidden items-center text-sm font-semibold sm:inline-flex">
              <Link to="/dang-nhap" className={topLink}>Đăng nhập</Link>
              <span aria-hidden="true" className="text-white/60">|</span>
              <Link to="/dang-ky" className={topLink}>Đăng ký</Link>
            </span>
          ))}
          <button
            type="button"
            className="rounded-(--radius-control) p-2.5 text-white hover:bg-white/15 lg:hidden"
            aria-label={mobileOpen ? 'Đóng menu' : 'Mở menu'}
            aria-expanded={mobileOpen}
            onClick={() => setMobileOpen((o) => !o)}
          >
            {mobileOpen ? <X className="size-6" /> : <Menu className="size-6" />}
          </button>
        </div>
      </div>
      {mobileOpen && (
        <nav className="container-page flex flex-col gap-1 pb-4 lg:hidden" aria-label="Menu điện thoại">
          <NavLink to="/khoa-hoc" className={topLink}>Tất cả khóa học</NavLink>
          <NavLink to={myCourses} className={topLink}>Khóa học của tôi</NavLink>
          <NavLink to="/xac-minh" className={topLink}>Xác minh chứng chỉ</NavLink>
          {!isAuthenticated && (
            <div className="mt-2 flex gap-2">
              <Link to="/dang-nhap" className="btn flex-1 border-[1.5px] border-white text-white"><UserIcon className="size-4" aria-hidden="true" />Đăng nhập</Link>
              <Link to="/dang-ky" className="btn flex-1 bg-white text-brand-700">Đăng ký</Link>
            </div>
          )}
        </nav>
      )}
    </header>
  )
}

/** Thanh lĩnh vực bên dưới header, giống thanh danh mục của một sàn: cuộn ngang trên điện thoại. */
function CategoryBar() {
  const { data } = useAsync(() => catalogService.categories(), [])
  const { pathname } = useLocation()
  const [params] = useSearchParams()
  const onCourses = pathname === '/khoa-hoc'
  const current = onCourses ? (params.get('free') === 'true' ? '__free' : params.get('category') ?? '__all') : null

  const items = [
    { key: '__all', label: 'Tất cả', to: '/khoa-hoc' },
    ...(data ?? []).filter((c) => c.parentId === null && c.isActive).map((c) => ({ key: c.slug, label: c.name, to: `/khoa-hoc?category=${c.slug}` })),
    { key: '__free', label: 'Miễn phí', to: '/khoa-hoc?free=true' },
  ]

  return (
    <nav className="border-b border-line bg-white" aria-label="Lĩnh vực">
      <ul className="container-page no-scrollbar flex gap-7 overflow-x-auto">
        {items.map((it) => {
          const active = current === it.key
          return (
            <li key={it.key} className="shrink-0">
              <Link
                to={it.to}
                aria-current={active ? 'page' : undefined}
                className={`flex min-h-12 items-center border-b-[3px] text-[15px] font-semibold whitespace-nowrap transition-colors ${active ? 'border-brand-600 text-brand-700' : 'border-transparent text-ink hover:text-brand-700'}`}
              >
                {it.label}
              </Link>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}

function Footer() {
  const col = 'flex flex-col gap-3'
  const lnk = 'text-ink-soft hover:text-brand-700'
  return (
    <footer className="mt-auto border-t border-line bg-white">
      <div className="container-page grid gap-10 py-12 sm:grid-cols-2 lg:grid-cols-4">
        <div className={col}>
          <Logo />
          <p className="max-w-xs text-ink-soft">Nền tảng khóa học trực tuyến của giảng viên Việt. Mỗi chứng chỉ có mã riêng để nhà tuyển dụng tự kiểm tra.</p>
        </div>
        <ul className={col}>
          <li className="font-bold">Khám phá</li>
          <li><Link className={lnk} to="/khoa-hoc">Tất cả khóa học</Link></li>
          <li><Link className={lnk} to="/khoa-hoc?sort=popular">Bán chạy</Link></li>
          <li><Link className={lnk} to="/khoa-hoc?free=true">Miễn phí</Link></li>
        </ul>
        <ul className={col}>
          <li className="font-bold">Dành cho học viên</li>
          <li><Link className={lnk} to="/hoc-vien">Khóa học của tôi</Link></li>
          <li><Link className={lnk} to="/tai-khoan/don-hang">Lịch sử mua hàng</Link></li>
          <li><Link className={lnk} to="/xac-minh">Xác minh chứng chỉ</Link></li>
        </ul>
        <ul className={col}>
          <li className="font-bold">Liên hệ</li>
          <li><a className={lnk} href="mailto:hotro@eduviet.local">hotro@eduviet.local</a></li>
          <li><a className={lnk} href="tel:19001234">1900 1234</a></li>
          <li className="text-ink-soft">Thanh toán VNPay · MoMo</li>
        </ul>
      </div>
      <p className="border-t border-line py-5 text-center text-sm text-ink-muted">
        © {new Date().getFullYear()} EduViet. Ảnh minh họa: Unsplash qua picsum.photos.
      </p>
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
      <CategoryBar />
      <main id="main" className="flex-1">
        <Outlet />
      </main>
      <Footer />
    </div>
  )
}
