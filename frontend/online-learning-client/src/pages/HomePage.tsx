import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowRight, BookOpen, ChevronLeft, ChevronRight, Clock, QrCode } from 'lucide-react'
import { Avatar, ErrorState, Skeleton } from '../components/ui'
import { useAsync, useDocumentTitle } from '../hooks/useAsync'
import { useAuth } from '../hooks/useAuth'
import { catalogService } from '../services/api'
import type { CourseSummary, HomeData } from '../types/api'
import { discountPercent, formatDuration, formatNumber, formatPrice } from '../utils/format'

/* ------------------------------------------------------------------ */
/* Hàng cuộn ngang dùng chung: vuốt trên điện thoại, nút ‹ › trên máy tính */
/* ------------------------------------------------------------------ */
function useRail() {
  const ref = useRef<HTMLDivElement>(null)
  const [state, setState] = useState({ page: 0, pages: 1, atStart: true, atEnd: true })

  const measure = useCallback(() => {
    const el = ref.current
    if (!el) return
    const pages = Math.max(1, Math.round(el.scrollWidth / el.clientWidth))
    setState({
      page: Math.min(pages - 1, Math.round(el.scrollLeft / el.clientWidth)),
      pages,
      atStart: el.scrollLeft <= 4,
      atEnd: el.scrollLeft + el.clientWidth >= el.scrollWidth - 4,
    })
  }, [])

  useEffect(() => {
    const el = ref.current
    if (!el) return
    measure()
    el.addEventListener('scroll', measure, { passive: true })
    const ro = new ResizeObserver(measure)
    ro.observe(el)
    return () => {
      el.removeEventListener('scroll', measure)
      ro.disconnect()
    }
  }, [measure])

  const go = (dir: 1 | -1) => {
    const el = ref.current
    if (el) el.scrollBy({ left: dir * el.clientWidth, behavior: 'smooth' })
  }
  const goTo = (page: number) => {
    const el = ref.current
    if (el) el.scrollTo({ left: page * el.clientWidth, behavior: 'smooth' })
  }
  return { ref, ...state, go, goTo }
}

function RailButtons({ rail, label }: { rail: ReturnType<typeof useRail>; label: string }) {
  const cls = 'flex size-11 items-center justify-center rounded-full border border-line bg-white text-ink transition-[opacity,scale] active:scale-95 disabled:opacity-35'
  return (
    <div className="flex items-center gap-2">
      <button type="button" className={cls} onClick={() => rail.go(-1)} disabled={rail.atStart} aria-label={`${label}: xem trước`}>
        <ChevronLeft className="size-5" aria-hidden="true" />
      </button>
      <button type="button" className={cls} onClick={() => rail.go(1)} disabled={rail.atEnd} aria-label={`${label}: xem tiếp`}>
        <ChevronRight className="size-5" aria-hidden="true" />
      </button>
    </div>
  )
}

function Rail({ id, title, link, children, boxed = false }: { id: string; title: string; link?: string; children: ReactNode; boxed?: boolean }) {
  const rail = useRail()
  return (
    <section className="container-page pt-10" aria-labelledby={id}>
      <div className={boxed ? 'rounded-2xl border border-line bg-white p-5 sm:p-6' : ''}>
        <div className="mb-5 flex items-center justify-between gap-4">
          <h2 id={id} className="text-2xl font-extrabold tracking-tight">{title}</h2>
          <div className="flex items-center gap-4">
            {link && <Link to={link} className="text-sm font-semibold text-brand-700 hover:underline">Xem thêm</Link>}
            <div className="hidden md:block"><RailButtons rail={rail} label={title} /></div>
          </div>
        </div>
        <div ref={rail.ref} className="no-scrollbar -mx-1 flex snap-x snap-mandatory gap-4 overflow-x-auto px-1 pb-1">
          {children}
        </div>
      </div>
    </section>
  )
}

function Thumb({ course }: { course: CourseSummary }) {
  return course.thumbnailUrl ? (
    <img src={course.thumbnailUrl} alt="" width={800} height={450} loading="lazy"
      className="size-full object-cover transition-transform duration-500 ease-(--ease-out-strong) group-hover:scale-[1.04]" />
  ) : (
    <span className="flex size-full items-end bg-brand-600 p-4 text-xl font-extrabold text-white">{course.category.name}</span>
  )
}

function courseTag(c: CourseSummary): string {
  const off = discountPercent(c.price, c.discountPrice)
  if (c.effectivePrice === 0) return 'Miễn phí'
  if (off !== null) return `Giảm ${off}%`
  if (c.isFeatured) return 'Nổi bật'
  return c.category.name
}

/* ------------------------------------------------------------------ */
/* Banner đôi */
/* ------------------------------------------------------------------ */
function BannerCarousel({ courses }: { courses: CourseSummary[] }) {
  const rail = useRail()
  if (courses.length === 0) return null
  return (
    <section className="container-page pt-6" aria-label="Khóa học nổi bật">
      <div ref={rail.ref} className="no-scrollbar flex snap-x snap-mandatory gap-4 overflow-x-auto">
        {courses.map((c, i) => (
          <Link
            key={c.id}
            to={`/khoa-hoc/${c.slug}`}
            className="group relative aspect-[16/10] shrink-0 basis-full snap-start overflow-hidden rounded-2xl bg-surface-3 sm:aspect-video lg:basis-[calc(50%-8px)]"
          >
            {c.thumbnailUrl && (
              <img src={c.thumbnailUrl} alt="" width={1200} height={675} loading={i < 2 ? 'eager' : 'lazy'} fetchPriority={i === 0 ? 'high' : undefined}
                className="absolute inset-0 size-full object-cover transition-transform duration-700 ease-(--ease-out-strong) group-hover:scale-[1.03]" />
            )}
            <span className="absolute right-3 bottom-3 left-3 flex flex-col gap-2 rounded-xl bg-white p-4 sm:right-auto sm:max-w-[78%] sm:p-5">
              <span className="self-start rounded-md bg-sun-300 px-2.5 py-1 text-xs font-extrabold text-ink">{courseTag(c)}</span>
              <span className="line-clamp-2 text-lg leading-tight font-extrabold tracking-tight sm:text-2xl">{c.title}</span>
              <span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-soft">
                <span>{c.instructor.fullName}</span>
                <span className="font-extrabold text-brand-700">{formatPrice(c.effectivePrice)}</span>
              </span>
            </span>
          </Link>
        ))}
      </div>
      {rail.pages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-4">
          <button type="button" onClick={() => rail.go(-1)} disabled={rail.atStart} aria-label="Banner trước"
            className="flex size-11 items-center justify-center rounded-full border border-line bg-white transition-[opacity,scale] active:scale-95 disabled:opacity-35">
            <ChevronLeft className="size-5" aria-hidden="true" />
          </button>
          <div className="flex gap-1" role="group" aria-label="Chọn trang banner">
            {Array.from({ length: rail.pages }, (_, i) => (
              <button key={i} type="button" aria-label={`Trang banner ${i + 1}`} aria-current={i === rail.page ? 'true' : undefined}
                onClick={() => rail.goTo(i)} className="flex h-11 items-center px-1">
                <span className={`block h-2 rounded-full transition-[width,background-color] duration-300 ${i === rail.page ? 'w-7 bg-brand-600' : 'w-2 bg-ink-muted/40'}`} />
              </button>
            ))}
          </div>
          <button type="button" onClick={() => rail.go(1)} disabled={rail.atEnd} aria-label="Banner tiếp theo"
            className="flex size-11 items-center justify-center rounded-full border border-line bg-white transition-[opacity,scale] active:scale-95 disabled:opacity-35">
            <ChevronRight className="size-5" aria-hidden="true" />
          </button>
        </div>
      )}
    </section>
  )
}

/* ------------------------------------------------------------------ */
/* Thẻ */
/* ------------------------------------------------------------------ */
function PosterCard({ course }: { course: CourseSummary }) {
  return (
    <Link to={`/khoa-hoc/${course.slug}`} className="group flex w-44 shrink-0 snap-start flex-col gap-2.5 sm:w-52">
      <span className="aspect-[3/4] overflow-hidden rounded-xl bg-surface-3"><Thumb course={course} /></span>
      <span className="line-clamp-2 leading-snug font-bold group-hover:text-brand-700">{course.title}</span>
      <span className="text-sm font-extrabold text-brand-700">{formatPrice(course.effectivePrice)}</span>
    </Link>
  )
}

function RankCard({ course, rank }: { course: CourseSummary; rank: number }) {
  return (
    <Link to={`/khoa-hoc/${course.slug}`} className="group flex shrink-0 snap-start flex-col gap-2" aria-label={`Hạng ${rank}: ${course.title}`}>
      <span className="flex items-end">
        <span className="rank-outline relative z-10 -mr-6 w-24 text-right text-[8.5rem] leading-[0.78] font-black tabular-nums" aria-hidden="true">{rank}</span>
        <span className="aspect-[3/4] w-40 overflow-hidden rounded-xl bg-surface-3 sm:w-44"><Thumb course={course} /></span>
      </span>
      <span className="ml-18 flex w-40 flex-col gap-0.5 sm:w-44">
        <span className="line-clamp-1 text-sm font-bold group-hover:text-brand-700">{course.title}</span>
        <span className="text-xs text-ink-soft">{formatNumber(course.enrollmentCount)} học viên</span>
      </span>
    </Link>
  )
}

function MarketCard({ course }: { course: CourseSummary }) {
  const off = discountPercent(course.price, course.discountPrice)
  return (
    <Link to={`/khoa-hoc/${course.slug}`} className="group flex flex-col overflow-hidden rounded-xl border border-line bg-white transition-colors hover:border-brand-300">
      <span className="aspect-video overflow-hidden bg-surface-3"><Thumb course={course} /></span>
      <span className="flex flex-1 flex-col gap-1.5 p-4">
        <span className="line-clamp-2 leading-snug font-bold group-hover:text-brand-700">{course.title}</span>
        <span className="flex items-baseline gap-2">
          <span className="font-extrabold text-brand-700">{formatPrice(course.effectivePrice)}</span>
          {off !== null && <span className="text-sm text-ink-muted line-through">{formatPrice(course.price)}</span>}
        </span>
        <span className="mt-auto flex flex-wrap items-center gap-x-3 gap-y-1 pt-1 text-sm text-ink-soft">
          <span className="inline-flex items-center gap-1"><BookOpen className="size-4" aria-hidden="true" />{course.totalLessons} bài</span>
          <span className="inline-flex items-center gap-1"><Clock className="size-4" aria-hidden="true" />{formatDuration(course.totalDurationSeconds)}</span>
        </span>
      </span>
    </Link>
  )
}

/* ------------------------------------------------------------------ */
/* Các khối nội dung */
/* ------------------------------------------------------------------ */
function uniqueCourses(data: HomeData): CourseSummary[] {
  const seen = new Map<string, CourseSummary>()
  for (const c of [...data.featured, ...data.bestSelling, ...data.newest]) if (!seen.has(c.id)) seen.set(c.id, c)
  return [...seen.values()]
}

/** Mỗi lĩnh vực một khối "Xem thêm", lấy từ các khóa học đã có trong dữ liệu trang chủ. */
function CategorySections({ data }: { data: HomeData }) {
  const groups = new Map<string, { name: string; slug: string; items: CourseSummary[] }>()
  for (const c of uniqueCourses(data)) {
    const g = groups.get(c.category.slug) ?? { name: c.category.name, slug: c.category.slug, items: [] }
    g.items.push(c)
    groups.set(c.category.slug, g)
  }
  const list = [...groups.values()].filter((g) => g.items.length >= 2).sort((a, b) => b.items.length - a.items.length).slice(0, 4)
  return (
    <>
      {list.map((g) => (
        <section key={g.slug} className="container-page pt-10" aria-labelledby={`cat-${g.slug}`}>
          <div className="mb-5 flex items-center justify-between gap-4">
            <h2 id={`cat-${g.slug}`} className="text-2xl font-extrabold tracking-tight">{g.name}</h2>
            <Link to={`/khoa-hoc?category=${g.slug}`} className="text-sm font-semibold text-brand-700 hover:underline">Xem thêm</Link>
          </div>
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {g.items.slice(0, 4).map((c) => <MarketCard key={c.id} course={c} />)}
          </div>
        </section>
      ))}
    </>
  )
}

function Instructors({ data }: { data: HomeData }) {
  if (data.featuredInstructors.length === 0) return null
  return (
    <Rail id="ins-title" title="Giảng viên nổi bật">
      {data.featuredInstructors.slice(0, 10).map((ins) => (
        <Link key={ins.id} to={`/giang-vien/${ins.id}`} className="group flex w-64 shrink-0 snap-start items-center gap-3 rounded-xl border border-line bg-white p-4 transition-colors hover:border-brand-300">
          <Avatar name={ins.fullName} src={ins.avatarUrl} size={56} />
          <span className="min-w-0">
            <span className="block truncate font-bold group-hover:text-brand-700">{ins.fullName}</span>
            <span className="block text-sm text-ink-soft">{ins.totalCourses} khóa · {formatNumber(ins.totalStudents)} học viên</span>
          </span>
        </Link>
      ))}
    </Rail>
  )
}

function VerifyStrip() {
  const navigate = useNavigate()
  const [code, setCode] = useState('')
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const v = code.trim().toUpperCase()
    navigate(v ? `/xac-minh/${encodeURIComponent(v)}` : '/xac-minh')
  }
  return (
    <section className="container-page pt-12" aria-labelledby="verify-title">
      <div className="flex flex-col gap-5 rounded-2xl bg-accent-50 p-6 sm:p-8 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex items-start gap-4">
          <QrCode className="mt-1 size-10 shrink-0 text-accent-700" aria-hidden="true" />
          <div>
            <h2 id="verify-title" className="text-2xl font-extrabold tracking-tight">Xác minh chứng chỉ EduViet</h2>
            <p className="mt-1 text-ink-soft">Nhập mã dạng EDV-2026-XXXXXXXX hoặc quét mã QR in trên chứng chỉ.</p>
          </div>
        </div>
        <form onSubmit={submit} className="flex w-full gap-2 lg:max-w-md">
          <label htmlFor="home-verify" className="sr-only">Mã chứng chỉ</label>
          <input id="home-verify" value={code} onChange={(e) => setCode(e.target.value)} placeholder="EDV-2026-…" className="input min-h-12 min-w-0 flex-1 bg-white" />
          <button type="submit" className="btn-accent min-h-12 px-6">Kiểm tra</button>
        </form>
      </div>
    </section>
  )
}

function GuestCta() {
  return (
    <section className="container-page pt-6" aria-labelledby="cta-title">
      <div className="flex flex-col gap-5 rounded-2xl bg-brand-600 p-6 text-white sm:p-8 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 id="cta-title" className="text-2xl font-extrabold tracking-tight">Bắt đầu với một khóa học miễn phí</h2>
          <p className="mt-1 text-brand-50">Tạo tài khoản trong một phút, lưu tiến độ trên mọi thiết bị.</p>
        </div>
        <Link to="/dang-ky" className="btn min-h-12 bg-white px-7 text-base text-brand-700 hover:bg-brand-50">
          Đăng ký miễn phí <ArrowRight className="size-5" aria-hidden="true" />
        </Link>
      </div>
    </section>
  )
}

function HomeSkeleton() {
  return (
    <div className="container-page pt-6" aria-hidden="true">
      <div className="grid gap-4 lg:grid-cols-2">
        <Skeleton className="aspect-video w-full rounded-2xl" />
        <Skeleton className="hidden aspect-video w-full rounded-2xl lg:block" />
      </div>
      <Skeleton className="mt-10 h-8 w-56" />
      <div className="mt-5 flex gap-4 overflow-hidden">
        {Array.from({ length: 6 }, (_, i) => <Skeleton key={i} className="aspect-[3/4] w-52 shrink-0 rounded-xl" />)}
      </div>
    </div>
  )
}

export default function HomePage() {
  useDocumentTitle('Khóa học trực tuyến có chứng chỉ')
  const { data, loading, error, reload } = useAsync(() => catalogService.home(), [])
  const { isAuthenticated, initializing } = useAuth()

  const banners = data ? (data.featured.length > 0 ? data.featured : data.bestSelling).slice(0, 6) : []
  const specials = data ? uniqueCourses(data).filter((c) => !banners.some((b) => b.id === c.id)).slice(0, 10) : []

  return (
    <div className="bg-surface-2 pb-16">
      <div className="container-page flex flex-wrap items-end justify-between gap-x-6 gap-y-1 pt-6">
        <h1 className="text-2xl font-extrabold tracking-tight sm:text-[1.75rem]">Khóa học trực tuyến có chứng chỉ</h1>
        {data && (
          <p className="text-sm text-ink-soft">
            {formatNumber(data.stats.totalCourses)} khóa học · {formatNumber(data.stats.totalInstructors)} giảng viên · {formatNumber(data.stats.totalStudents)} học viên
          </p>
        )}
      </div>

      {loading && <HomeSkeleton />}
      {error && <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>}
      {data && (
        <>
          <BannerCarousel courses={banners} />
          {specials.length > 0 && (
            <Rail id="special-title" title="Khóa học đặc biệt" link="/khoa-hoc?sort=popular" boxed>
              {specials.map((c) => <PosterCard key={c.id} course={c} />)}
            </Rail>
          )}
          {data.bestSelling.length > 0 && (
            <Rail id="trend-title" title="Xu hướng tuần này" link="/khoa-hoc?sort=popular">
              {data.bestSelling.slice(0, 10).map((c, i) => <RankCard key={c.id} course={c} rank={i + 1} />)}
            </Rail>
          )}
          <CategorySections data={data} />
          {data.newest.length > 0 && (
            <Rail id="new-title" title="Mới xuất bản" link="/khoa-hoc?sort=newest">
              {data.newest.slice(0, 10).map((c) => <PosterCard key={c.id} course={c} />)}
            </Rail>
          )}
          <Instructors data={data} />
          <VerifyStrip />
          {/* Lời mời đăng ký chỉ dành cho khách */}
          {!initializing && !isAuthenticated && <GuestCta />}
        </>
      )}
    </div>
  )
}
