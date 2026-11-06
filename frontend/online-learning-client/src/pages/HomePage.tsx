import { useState, type FormEvent, type KeyboardEvent, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import {
  ArrowRight, Award, BarChart3, BookOpen, Briefcase, ChevronRight, Code2, CreditCard, FileSpreadsheet, Languages, Megaphone,
  MessagesSquare, MonitorSmartphone, Palette, PlayCircle, QrCode, Quote, Search, ShieldCheck, Users, type LucideIcon,
} from 'lucide-react'
import { CourseGrid } from '../components/course/CourseCard'
import { ResponsiveImg, Avatar, DecorBlobs, ErrorState, RatingStars, SectionHeading, Skeleton } from '../components/ui'
import { site } from '../config/site'
import { useAsync } from '../hooks/useAsync'
import { useSeo } from '../hooks/useSeo'
import { useReveal } from '../hooks/useReveal'
import { catalogService } from '../services/api'
import type { CourseSummary, HomeData } from '../types/api'
import { formatNumber, formatPrice } from '../utils/format'

const categoryIcons: Record<string, LucideIcon> = {
  'lap-trinh': Code2,
  'thiet-ke': Palette,
  'kinh-doanh': Briefcase,
  marketing: Megaphone,
  'ngoai-ngu': Languages,
  'tin-hoc-van-phong': FileSpreadsheet,
  'ky-nang-mem': MessagesSquare,
  'du-lieu-ai': BarChart3,
}
const iconFor = (slug: string) => categoryIcons[slug] ?? BookOpen

function uniqueCourses(data: HomeData): CourseSummary[] {
  const seen = new Map<string, CourseSummary>()
  for (const c of [...data.featured, ...data.bestSelling, ...data.newest]) if (!seen.has(c.id)) seen.set(c.id, c)
  return [...seen.values()]
}

/* ------------------------------------------------------------------ */
/* Hero: thông điệp bên trái, thẻ khóa học thật + nhãn nổi bên phải     */
/* ------------------------------------------------------------------ */
function FloatChip({ className, icon: Icon, tone, children }: { className: string; icon: LucideIcon; tone: string; children: ReactNode }) {
  return (
    <div className={`card absolute z-20 flex items-center gap-2.5 rounded-full py-2 pr-4 pl-2 text-sm font-semibold shadow-(--shadow-lift) ${className}`}>
      <span className={`flex size-8 items-center justify-center rounded-full ${tone}`}><Icon className="size-4" aria-hidden="true" /></span>
      {children}
    </div>
  )
}

function HeroVisual({ data }: { data: HomeData }) {
  const main = data.bestSelling[0] ?? data.featured[0]
  const back = data.featured.find((c) => c.id !== main?.id)
  if (!main) return null
  return (
    <div className="relative mx-auto h-[420px] w-full max-w-[520px] sm:h-[480px]" aria-hidden="true">
      <span className="absolute top-1/2 left-1/2 size-[340px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-white shadow-(--shadow-soft) sm:size-[420px]" />
      <span className="absolute top-1/2 left-1/2 size-[260px] -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-dashed border-brand-200 sm:size-[320px]" />

      {back?.thumbnailUrl && (
        <div className="card absolute top-6 right-2 w-[210px] rotate-6 p-2 sm:right-0 sm:w-[240px]">
          <ResponsiveImg url={back.thumbnailUrl} sizes="240px" maxWidth={480} width={480} height={270} className="aspect-video w-full rounded-lg object-cover" />
        </div>
      )}

      <div className="card absolute top-1/2 left-1/2 z-10 w-[290px] -translate-x-1/2 -translate-y-1/2 p-3 shadow-(--shadow-lift) sm:w-[330px]">
        {main.thumbnailUrl && (
          <ResponsiveImg url={main.thumbnailUrl} sizes="330px" maxWidth={640} width={660} height={371} eager fetchPriority="high" className="aspect-video w-full rounded-xl object-cover" />
        )}
        <div className="flex flex-col gap-1.5 px-1 pt-3 pb-1">
          <span className="chip self-start">{main.category.name}</span>
          <p className="line-clamp-2 font-display font-semibold">{main.title}</p>
          <p className="text-sm text-ink-soft">{main.instructor.fullName}</p>
          <div className="flex items-center justify-between pt-1">
            {main.reviewCount > 0 ? <RatingStars value={main.averageRating} size={13} /> : <span className="text-sm text-ink-muted">Mới</span>}
            <span className="font-bold text-brand-700">{formatPrice(main.effectivePrice)}</span>
          </div>
        </div>
      </div>

      <FloatChip className="float-slow top-10 left-0" icon={QrCode} tone="bg-mint-100 text-mint-700">Chứng chỉ có mã QR</FloatChip>
      <FloatChip className="float-slow-delay right-0 bottom-3" icon={CreditCard} tone="bg-brand-50 text-brand-700">VNPay · MoMo</FloatChip>
      {data.stats.totalStudents >= site.statThresholds.students ? (
        <FloatChip className="float-slow bottom-20 left-0 sm:-left-4" icon={Users} tone="bg-accent-50 text-accent-700">
          {formatNumber(data.stats.totalStudents)} học viên
        </FloatChip>
      ) : (
        <FloatChip className="float-slow bottom-3 left-0" icon={PlayCircle} tone="bg-accent-50 text-accent-700">Học thử miễn phí</FloatChip>
      )}
    </div>
  )
}

/**
 * Dải số liệu dưới hero. Chỉ khoe con số khi đã đủ lớn (ngưỡng trong config/site.ts);
 * khi còn nhỏ thì hiện ba cam kết của nền tảng để không làm khách mới mất niềm tin.
 */
function HeroFacts({ data }: { data: HomeData | null }) {
  const box = 'mt-2 grid w-full max-w-lg grid-cols-3 divide-x divide-line rounded-2xl bg-white/80 py-4 shadow-(--shadow-soft)'
  if (!data) return <div className={box}><div className="col-span-3 px-4"><Skeleton className="h-10 w-full" /></div></div>
  const t = site.statThresholds
  const s = data.stats
  const showNumbers = s.totalStudents >= t.students && s.totalCourses >= t.courses && s.totalInstructors >= t.instructors
  if (showNumbers) {
    return (
      <dl className={box}>
        {([[s.totalCourses, 'khóa học'], [s.totalInstructors, 'giảng viên'], [s.totalStudents, 'học viên']] as const).map(([n, label]) => (
          <div key={label} className="flex flex-col items-center gap-0.5 px-2">
            <dt className="order-2 text-sm text-ink-soft">{label}</dt>
            <dd className="order-1 font-display text-2xl font-semibold text-brand-700 tabular-nums">{formatNumber(n)}</dd>
          </div>
        ))}
      </dl>
    )
  }
  const facts: { icon: LucideIcon; label: string }[] = [
    { icon: PlayCircle, label: 'Học thử miễn phí' },
    { icon: CreditCard, label: 'VNPay · MoMo' },
    { icon: QrCode, label: 'Chứng chỉ có QR' },
  ]
  return (
    <ul className={box}>
      {facts.map(({ icon: Icon, label }) => (
        <li key={label} className="flex flex-col items-center gap-1.5 px-2 text-center">
          <Icon className="size-6 text-brand-600" aria-hidden="true" />
          <span className="text-sm font-medium text-ink-soft">{label}</span>
        </li>
      ))}
    </ul>
  )
}

function Hero({ data }: { data: HomeData | null }) {
  return (
    <section className="relative isolate overflow-hidden bg-surface-2" aria-labelledby="hero-title">
      <DecorBlobs variant="right" />
      <div className="container-page grid items-center gap-10 pt-12 pb-24 md:pt-16 lg:grid-cols-[1.05fr_1fr] lg:gap-12 lg:pb-32">
        <div className="rise-in flex flex-col items-start gap-6">
          <span className="chip bg-white py-1.5 text-sm shadow-(--shadow-soft)">
            <ShieldCheck className="size-4" aria-hidden="true" /> Nền tảng học trực tuyến có chứng chỉ xác minh
          </span>
          <h1 id="hero-title" className="text-display font-semibold">
            Học kỹ năng thực tế, nhận <span className="mark-mint">chứng chỉ có mã xác minh</span>
          </h1>
          <p className="max-w-xl text-lg text-ink-soft">
            Khóa học do giảng viên Việt đang làm nghề hướng dẫn. Học thử miễn phí, học trên mọi thiết bị và nhận chứng chỉ PDF
            mà nhà tuyển dụng tự kiểm tra được.
          </p>
          <div className="flex flex-wrap gap-3">
            <Link to="/khoa-hoc" className="btn-primary min-h-12 px-6 text-base">
              Khám phá khóa học <ArrowRight className="size-5" aria-hidden="true" />
            </Link>
            <Link to="/xac-minh" className="btn-outline min-h-12 px-6 text-base">
              <ShieldCheck className="size-5" aria-hidden="true" /> Xác minh chứng chỉ
            </Link>
          </div>
          <HeroFacts data={data} />
        </div>
        <div className="min-w-0">
          {data ? <HeroVisual data={data} /> : <Skeleton className="mx-auto h-[420px] w-full max-w-[520px]" />}
        </div>
      </div>
      {/* Sóng mint cuối hero */}
      <svg className="absolute inset-x-0 bottom-0 h-16 w-full text-white md:h-20" viewBox="0 0 1440 80" preserveAspectRatio="none" aria-hidden="true">
        <path d="M0 48C240 8 480 8 720 36s480 40 720 0v44H0z" className="fill-mint-100" />
        <path d="M0 64c240-28 480-28 720-4s480 28 720-4v24H0z" fill="currentColor" />
      </svg>
    </section>
  )
}

/* ------------------------------------------------------------------ */
function Benefits() {
  const ref = useReveal<HTMLElement>()
  const items: { icon: LucideIcon; tone: string; title: string; text: string; to: string; cta: string }[] = [
    { icon: PlayCircle, tone: 'bg-brand-50 text-brand-600', title: 'Học thử trước khi mua', text: 'Mỗi khóa có bài học thử miễn phí để bạn xem cách giảng trước khi quyết định.', to: '/khoa-hoc', cta: 'Xem khóa học' },
    { icon: MonitorSmartphone, tone: 'bg-mint-50 text-mint-700', title: 'Học mọi lúc, lưu tiến độ', text: 'Video, tài liệu và bài kiểm tra trên điện thoại hay máy tính; hệ thống nhớ bạn đang học tới đâu.', to: '/hoc-vien', cta: 'Vào khu học tập' },
    { icon: Award, tone: 'bg-accent-50 text-accent-700', title: 'Chứng chỉ có mã xác minh', text: 'Hoàn thành khóa là có chứng chỉ PDF kèm mã QR, ai cũng kiểm tra được trên EduViet.', to: '/xac-minh', cta: 'Xác minh chứng chỉ' },
  ]
  return (
    <section ref={ref} className="container-page py-20 md:py-24" aria-labelledby="benefit-title">
      <SectionHeading
        id="benefit-title"
        eyebrow="Vì sao chọn EduViet"
        title="Học trực tuyến nghiêm túc, có kết quả rõ ràng"
        action={<Link to="/khoa-hoc" className="inline-flex items-center gap-1 font-semibold text-brand-700 hover:gap-2 transition-[gap]">Tìm hiểu thêm <ChevronRight className="size-4" aria-hidden="true" /></Link>}
      />
      <div className="grid gap-6 md:grid-cols-3">
        {items.map(({ icon: Icon, tone, title, text, to, cta }) => (
          <article key={title} className="card card-hover flex flex-col gap-5 p-4" data-reveal>
            <div className={`relative flex aspect-[16/9] items-center justify-center overflow-hidden rounded-xl ${tone}`}>
              <span className="absolute -top-6 -right-6 size-24 rounded-full bg-white/60" aria-hidden="true" />
              <span className="absolute -bottom-8 -left-4 size-20 rounded-full bg-white/50" aria-hidden="true" />
              <span className="relative flex size-20 items-center justify-center rounded-2xl bg-white shadow-(--shadow-soft)">
                <Icon className="size-10" aria-hidden="true" />
              </span>
            </div>
            <div className="flex flex-1 flex-col gap-2 px-2">
              <h3 className="text-heading font-semibold">{title}</h3>
              <p className="text-ink-soft">{text}</p>
            </div>
            <Link to={to} className="btn-soft mx-2 mb-2 self-start">{cta} <ArrowRight className="size-4" aria-hidden="true" /></Link>
          </article>
        ))}
      </div>
    </section>
  )
}

/* ------------------------------------------------------------------ */
/* Tab lĩnh vực: mỗi tab là một thẻ chia đôi chữ / ảnh                   */
/* ------------------------------------------------------------------ */
function CategoryTabs({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  const groups = new Map<string, { name: string; slug: string; items: CourseSummary[] }>()
  for (const c of uniqueCourses(data)) {
    const g = groups.get(c.category.slug) ?? { name: c.category.name, slug: c.category.slug, items: [] }
    g.items.push(c)
    groups.set(c.category.slug, g)
  }
  const tabs = [...groups.values()].sort((a, b) => b.items.length - a.items.length).slice(0, 6)
  const [active, setActive] = useState(0)
  if (tabs.length === 0) return null
  const cur = tabs[Math.min(active, tabs.length - 1)]
  const top = [...cur.items].sort((a, b) => b.enrollmentCount - a.enrollmentCount)
  const hero = top[0]
  const total = data.categories.find((c) => c.slug === cur.slug)?.courseCount ?? cur.items.length
  const CurIcon = iconFor(cur.slug)

  const onKey = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return
    e.preventDefault()
    const next = (active + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length
    setActive(next)
    document.getElementById(`cat-tab-${next}`)?.focus()
  }

  return (
    <section ref={ref} className="container-page pb-20 md:pb-24" aria-labelledby="cat-title">
      <SectionHeading id="cat-title" align="center" title="Khóa học cho mọi lĩnh vực" lead="Chọn lĩnh vực bạn quan tâm để xem các khóa học được học nhiều nhất." />
      <div role="tablist" aria-label="Lĩnh vực" onKeyDown={onKey} className="no-scrollbar mb-8 flex gap-2 overflow-x-auto border-b border-line md:justify-center">
        {tabs.map((t, i) => {
          const Icon = iconFor(t.slug)
          const selected = i === active
          return (
            <button
              key={t.slug}
              id={`cat-tab-${i}`}
              type="button"
              role="tab"
              aria-selected={selected}
              aria-controls="cat-panel"
              tabIndex={selected ? 0 : -1}
              onClick={() => setActive(i)}
              className={`relative flex min-h-12 shrink-0 items-center gap-2 px-4 font-medium whitespace-nowrap transition-colors after:absolute after:inset-x-3 after:-bottom-px after:h-[3px] after:rounded-full ${selected ? 'text-brand-700 after:bg-brand-600' : 'text-ink-soft hover:text-ink after:bg-transparent'}`}
            >
              <Icon className="size-[18px]" aria-hidden="true" /> {t.name}
            </button>
          )
        })}
      </div>
      <div id="cat-panel" role="tabpanel" aria-labelledby={`cat-tab-${active}`} className="card relative isolate grid gap-8 overflow-hidden p-6 sm:p-10 lg:grid-cols-2 lg:items-center">
        <DecorBlobs variant="right" />
        <div className="flex flex-col gap-5">
          <span className="flex size-14 items-center justify-center rounded-2xl bg-brand-50 text-brand-600"><CurIcon className="size-7" aria-hidden="true" /></span>
          <h3 className="text-2xl font-semibold sm:text-3xl">{cur.name}</h3>
          <p className="text-lg text-ink-soft">{formatNumber(total)} khóa học đang mở trong lĩnh vực {cur.name.toLowerCase()}.</p>
          <ul className="flex flex-col divide-y divide-line/70">
            {top.slice(0, 3).map((c) => (
              <li key={c.id}>
                <Link to={`/khoa-hoc/${c.slug}`} className="group flex items-center justify-between gap-4 py-3">
                  <span className="font-medium group-hover:text-brand-700">{c.title}</span>
                  <span className="shrink-0 font-semibold text-brand-700">{formatPrice(c.effectivePrice)}</span>
                </Link>
              </li>
            ))}
          </ul>
          <Link to={`/khoa-hoc?category=${cur.slug}`} className="btn-primary self-start">Xem khóa học {cur.name} <ArrowRight className="size-4" aria-hidden="true" /></Link>
        </div>
        {hero && (
          <Link to={`/khoa-hoc/${hero.slug}`} className="group relative block">
            <div className="overflow-hidden rounded-2xl bg-surface-3">
              {hero.thumbnailUrl && (
                <ResponsiveImg url={hero.thumbnailUrl} sizes="(min-width: 1024px) 560px, 92vw" ratio={16 / 10} width={800} height={500}
                  className="aspect-[16/10] w-full object-cover transition-transform duration-700 ease-(--ease-out-strong) group-hover:scale-[1.03]" />
              )}
            </div>
            <div className="card absolute right-4 -bottom-5 left-4 flex items-center justify-between gap-4 px-5 py-3.5 shadow-(--shadow-lift) sm:left-auto sm:w-[320px]">
              <span className="min-w-0">
                <span className="block truncate font-semibold">{hero.title}</span>
                <span className="text-sm text-ink-soft">{formatNumber(hero.enrollmentCount)} học viên</span>
              </span>
              <span className="shrink-0 font-bold text-brand-700">{formatPrice(hero.effectivePrice)}</span>
            </div>
          </Link>
        )}
      </div>
    </section>
  )
}

/* ------------------------------------------------------------------ */
function CourseBlock({ id, eyebrow, title, lead, courses, link }: { id: string; eyebrow: string; title: string; lead: string; courses: CourseSummary[]; link: string }) {
  const ref = useReveal<HTMLElement>()
  if (courses.length === 0) return null
  const shown = courses.length >= 4 ? courses.slice(0, Math.min(8, Math.floor(courses.length / 4) * 4)) : courses
  return (
    <section ref={ref} className="container-page pb-20 md:pb-24" aria-labelledby={id}>
      <SectionHeading id={id} eyebrow={eyebrow} title={title} lead={lead}
        action={<Link to={link} className="btn-outline">Xem tất cả <ArrowRight className="size-4" aria-hidden="true" /></Link>} />
      <CourseGrid courses={shown} eagerCount={0} />
    </section>
  )
}

/** Hành trình học: 4 bước nối nhau bằng đường gạch. */
function Journey() {
  const ref = useReveal<HTMLElement>()
  const steps: { icon: LucideIcon; title: string; text: string }[] = [
    { icon: Search, title: 'Chọn khóa học', text: 'Tìm theo lĩnh vực, trình độ, giá; xem bài học thử miễn phí.' },
    { icon: CreditCard, title: 'Thanh toán an toàn', text: 'Thanh toán qua VNPay hoặc MoMo, vào học ngay sau khi thanh toán.' },
    { icon: PlayCircle, title: 'Học và làm bài kiểm tra', text: 'Học theo tiến độ riêng, làm quiz cuối chương để chắc kiến thức.' },
    { icon: Award, title: 'Nhận chứng chỉ', text: 'Chứng chỉ PDF có mã và QR, xác minh công khai trên EduViet.' },
  ]
  return (
    <section ref={ref} className="relative isolate overflow-hidden bg-surface-2 py-20 md:py-24" aria-labelledby="journey-title">
      <DecorBlobs variant="center" />
      <div className="container-page">
        <SectionHeading id="journey-title" align="center" eyebrow="Hành trình học tập" title="Từ lúc chọn khóa đến khi có chứng chỉ"
          lead="Mọi bước diễn ra trên EduViet, liền mạch trên cả điện thoại và máy tính." />
        <ol className="relative grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          <span className="absolute top-10 right-[12%] left-[12%] hidden border-t-2 border-dashed border-brand-200 lg:block" aria-hidden="true" />
          {steps.map(({ icon: Icon, title, text }, i) => (
            <li key={title} className="card card-hover relative flex flex-col items-center gap-3 p-6 text-center" data-reveal>
              <span className="relative flex size-14 items-center justify-center rounded-2xl bg-brand-600 text-white shadow-(--shadow-brand)">
                <Icon className="size-6" aria-hidden="true" />
                <span className="absolute -top-2 -right-2 flex size-6 items-center justify-center rounded-full bg-mint-300 text-xs font-bold text-ink">{i + 1}</span>
              </span>
              <h3 className="text-heading font-semibold">{title}</h3>
              <p className="text-ink-soft">{text}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}

function Testimonials({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  const items = data.testimonials.slice(0, 3)
  if (items.length === 0) return null
  return (
    <section ref={ref} className="container-page py-20 md:py-24" aria-labelledby="quote-title">
      <SectionHeading id="quote-title" align="center" eyebrow="Đánh giá" title="Học viên nói gì về EduViet" lead="Đánh giá thật từ học viên đã mua và học khóa học." />
      <ul className="grid gap-6 md:grid-cols-3">
        {items.map((t) => (
          <li key={t.id} data-reveal>
            <figure className="card card-hover flex h-full flex-col gap-4 p-7">
              <Quote className="size-8 text-mint-500" aria-hidden="true" />
              <RatingStars value={t.rating} size={16} showValue={false} />
              <blockquote className="text-lg leading-relaxed">“{t.comment}”</blockquote>
              <figcaption className="mt-auto flex items-center gap-3 border-t border-line/70 pt-4">
                <Avatar name={t.userFullName} src={t.userAvatarUrl} size={44} />
                <span className="min-w-0">
                  <span className="block font-semibold">{t.userFullName}</span>
                  <Link to={`/khoa-hoc/${t.courseSlug}`} className="block truncate text-sm text-ink-soft hover:text-brand-700">{t.courseTitle}</Link>
                </span>
              </figcaption>
            </figure>
          </li>
        ))}
      </ul>
    </section>
  )
}

function Instructors({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  if (data.featuredInstructors.length === 0) return null
  return (
    <section ref={ref} className="container-page pb-20 md:pb-24" aria-labelledby="ins-title">
      <SectionHeading id="ins-title" eyebrow="Giảng viên" title="Đội ngũ đứng lớp" lead="Giảng viên đang làm nghề, dạy từ chính dự án họ đã làm." />
      <ul className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {data.featuredInstructors.slice(0, 6).map((ins) => (
          <li key={ins.id} data-reveal>
            <Link to={`/giang-vien/${ins.id}`} className="group card card-hover flex h-full items-start gap-4 p-6">
              <Avatar name={ins.fullName} src={ins.avatarUrl} size={64} />
              <span className="flex min-w-0 flex-col gap-1">
                <span className="font-display text-lg font-semibold group-hover:text-brand-700">{ins.fullName}</span>
                {ins.headline && <span className="text-ink-soft">{ins.headline}</span>}
                <span className="mt-2 flex flex-wrap gap-2">
                  <span className="chip">{ins.totalCourses} khóa học</span>
                  <span className="chip bg-mint-50 text-mint-700">{formatNumber(ins.totalStudents)} học viên</span>
                  {ins.totalReviews > 0 && <span className="chip bg-accent-50 text-accent-700">{ins.averageRating.toFixed(1)}/5</span>}
                </span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}

/** Xác minh chứng chỉ: hình chứng chỉ mẫu bên trái, ô nhập mã bên phải. */
function VerifySplit({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  const navigate = useNavigate()
  const [code, setCode] = useState('')
  const submit = (e: FormEvent) => {
    e.preventDefault()
    const v = code.trim().toUpperCase()
    navigate(v ? `/xac-minh/${encodeURIComponent(v)}` : '/xac-minh')
  }
  const sampleCourse = data.bestSelling[0]?.title ?? data.featured[0]?.title
  return (
    <section ref={ref} className="container-page" aria-labelledby="verify-title">
      <div className="grid items-center gap-10 lg:grid-cols-2 lg:gap-16">
        <div className="relative isolate px-4 py-6 sm:px-10" data-reveal>
          <DecorBlobs variant="left" />
          <div className="card mx-auto max-w-md rotate-[-2deg] p-3 shadow-(--shadow-lift)">
            <div className="flex flex-col gap-4 rounded-xl border-2 border-brand-100 p-6 sm:p-8">
              <div className="flex items-center justify-between text-xs font-semibold tracking-wide text-ink-muted uppercase">
                <span>EduViet</span><span>Mẫu chứng chỉ</span>
              </div>
              <p className="text-sm text-ink-soft">Chứng nhận</p>
              <p className="font-display text-2xl font-semibold text-brand-700">Tên học viên</p>
              {sampleCourse && <p className="text-ink-soft">đã hoàn thành khóa học <span className="font-semibold text-ink">{sampleCourse}</span></p>}
              <div className="flex items-end justify-between border-t border-line/70 pt-4">
                <span>
                  <span className="block text-xs text-ink-muted">Mã xác minh</span>
                  <span className="font-semibold tracking-wide tabular-nums">EDV-YYYY-XXXXXXXX</span>
                </span>
                <QrCode className="size-12" aria-hidden="true" />
              </div>
            </div>
          </div>
        </div>
        <div className="flex flex-col items-start gap-5" data-reveal>
          <span className="chip bg-mint-50 text-mint-700"><ShieldCheck className="size-3.5" aria-hidden="true" /> Chứng chỉ</span>
          <h2 id="verify-title" className="text-title font-semibold">Chứng chỉ ai cũng kiểm tra được</h2>
          <p className="text-lg text-ink-soft">
            Mỗi chứng chỉ EduViet có một mã riêng dạng EDV-YYYY-XXXXXXXX và mã QR. Nhập mã bên dưới để xem chứng chỉ còn hiệu lực,
            cấp cho ai và cho khóa học nào.
            {data.stats.totalCertificates >= site.statThresholds.certificates && <> Đã có <b className="text-ink">{formatNumber(data.stats.totalCertificates)}</b> chứng chỉ được cấp.</>}
          </p>
          <form onSubmit={submit} className="flex w-full max-w-lg flex-col gap-2 sm:flex-row">
            <label htmlFor="home-verify" className="sr-only">Mã chứng chỉ</label>
            <input id="home-verify" value={code} onChange={(e) => setCode(e.target.value)} placeholder="Nhập mã, ví dụ EDV-2026-…" className="input min-w-0 flex-1" />
            <button type="submit" className="btn-primary min-h-12 px-6">Kiểm tra</button>
          </form>
        </div>
      </div>
    </section>
  )
}

function HomeSkeleton() {
  return (
    <div className="container-page py-20" aria-hidden="true">
      <Skeleton className="mb-10 h-10 w-72" />
      <CourseGrid courses={[]} loading skeletons={4} />
    </div>
  )
}

export default function HomePage() {
  useSeo({
    jsonLd: {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: site.name,
      url: window.location.origin,
      potentialAction: {
        '@type': 'SearchAction',
        target: `${window.location.origin}/khoa-hoc?keyword={search_term_string}`,
        'query-input': 'required name=search_term_string',
      },
    },
  })
  // Tự thử lại 2 lần khi API chưa sẵn sàng (vừa khởi động, mạng chập chờn) trước khi báo lỗi
  const { data, loading, error, reload } = useAsync(() => catalogService.home(), [], { retries: 2 })

  return (
    <>
      <Hero data={data} />
      {loading && <HomeSkeleton />}
      {error && <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>}
      {data && (
        <>
          <Benefits />
          <CategoryTabs data={data} />
          <CourseBlock id="featured-title" eyebrow="Nổi bật" title="Khóa học được chọn lọc" lead="Được đội ngũ EduViet chọn vì nội dung thực tế và đánh giá tốt."
            courses={data.featured} link="/khoa-hoc?sort=popular" />
          <Journey />
          <Testimonials data={data} />
          <Instructors data={data} />
          <CourseBlock id="newest-title" eyebrow="Mới cập nhật" title="Mới xuất bản" lead="Những khóa học vừa qua kiểm duyệt."
            courses={data.newest} link="/khoa-hoc?sort=newest" />
          <VerifySplit data={data} />
        </>
      )}
    </>
  )
}
