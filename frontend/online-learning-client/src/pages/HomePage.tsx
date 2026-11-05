import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowRight, QrCode } from 'lucide-react'
import { CourseGrid } from '../components/course/CourseCard'
import { Avatar, ErrorState, RatingStars, Skeleton } from '../components/ui'
import { useAsync, useDocumentTitle } from '../hooks/useAsync'
import { useAuth } from '../hooks/useAuth'
import { useReveal } from '../hooks/useReveal'
import { catalogService } from '../services/api'
import type { HomeData } from '../types/api'
import { formatDuration, formatNumber, formatPrice } from '../utils/format'

const ALPHABET = '23456789ABCDEFGHJKMNPQRSTUVWXYZ'

/** Mã minh họa ổn định theo tên — cùng định dạng mã chứng chỉ thật EDV-YYYY-XXXXXXXX. */
function sampleCode(name: string): string {
  let h = 2166136261
  for (const ch of name.normalize('NFC')) h = Math.imul(h ^ ch.codePointAt(0)!, 16777619)
  let out = ''
  for (let i = 0; i < 8; i++) {
    h = Math.imul(h ^ (h >>> 15), 2246822507)
    out += ALPHABET[Math.abs(h) % ALPHABET.length]
  }
  return `EDV-${new Date().getFullYear()}-${out}`
}

/* ------------------------------------------------------------------ */
/* Hero: tấm chứng chỉ đặt trên nền ảnh tối — gõ tên để thấy tên mình */
/* ------------------------------------------------------------------ */
function CertificateHero({ data }: { data: HomeData | null }) {
  const { isAuthenticated, initializing } = useAuth()
  const showSignUp = !initializing && !isAuthenticated
  const [name, setName] = useState('')
  const shownName = name.trim() || 'Tên của bạn'
  const code = useMemo(() => sampleCode(name.trim() || 'EduViet'), [name])
  const course = data?.featured[0] ?? data?.bestSelling[0]

  return (
    <section className="relative isolate overflow-hidden bg-ink" aria-labelledby="hero-title">
      <img
        src="https://picsum.photos/id/1005/2400/1400"
        alt=""
        width={2400}
        height={1400}
        fetchPriority="high"
        className="drift absolute inset-0 -z-10 size-full object-cover brightness-[0.55]"
      />
      <div className="container-page flex min-h-[calc(100svh-4.5rem)] items-center py-10 sm:py-16">
        <div className="rise-in mx-auto w-full max-w-5xl bg-white text-ink">
          <h1 id="hero-title" className="px-5 pt-8 text-[clamp(2.1rem,1.2rem+3.4vw,3.6rem)] leading-[1.02] font-extrabold tracking-[-0.03em] sm:px-10 sm:pt-12">
            Học online, nhận chứng chỉ thật
          </h1>

          <div className="grid gap-8 px-5 pt-6 pb-8 sm:px-10 sm:pb-10 md:grid-cols-[1.2fr_1fr] md:gap-10">
            <div className="flex flex-col md:justify-center">
              <p className="max-w-[36ch] text-lg text-ink-soft">
                Khóa học do giảng viên Việt hướng dẫn. Học xong, chứng chỉ của bạn có mã riêng để ai cũng kiểm tra được.
              </p>
              <div className="mt-8 flex flex-wrap items-center gap-3">
                <Link to="/khoa-hoc" className="btn-primary min-h-12 px-7 text-base">
                  Khám phá khóa học <ArrowRight className="size-5" aria-hidden="true" />
                </Link>
                {/* Chỉ mời đăng ký khi chắc chắn là khách (đã khôi phục phiên xong và chưa đăng nhập) */}
                {showSignUp && (
                  <Link to="/dang-ky" className="btn-ghost min-h-12 text-base underline-offset-4 hover:underline">Đăng ký miễn phí</Link>
                )}
              </div>
            </div>

            {/* Phần "chứng nhận" — thứ khách tương tác được */}
            <div className="flex flex-col justify-between gap-6 bg-brand-50 p-5 sm:p-6">
              <div>
                <p className="-mx-5 -mt-5 mb-5 flex justify-between gap-3 bg-brand-600 px-5 py-2.5 text-sm font-semibold text-white sm:-mx-6 sm:-mt-6 sm:px-6">
                  <span>EduViet</span><span>Chứng chỉ hoàn thành</span>
                </p>
                <p className="text-sm text-ink-soft">Chứng nhận</p>
                <p className="mt-1 min-h-[2.4em] text-2xl leading-tight font-extrabold break-words text-brand-700" aria-live="polite">
                  {shownName}
                </p>
                <p className="mt-3 text-sm text-ink-soft">đã hoàn thành khóa học</p>
                <p className="mt-1 font-bold">{course?.title ?? 'Lập trình C# từ cơ bản đến nâng cao'}</p>
              </div>
              <div className="flex items-end justify-between gap-3">
                <div>
                  <p className="text-sm text-ink-soft">Mã xác minh</p>
                  <p className="font-bold tabular-nums tracking-wide">{code}</p>
                </div>
                <QrCode className="size-12 text-ink" aria-hidden="true" />
              </div>
              <div>
                <label htmlFor="hero-name" className="text-sm font-semibold">Thử in tên bạn lên chứng chỉ</label>
                <input
                  id="hero-name"
                  value={name}
                  maxLength={40}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Ví dụ: Nguyễn Thu Hà"
                  className="input mt-1.5 min-h-11 bg-white"
                  autoComplete="name"
                />
              </div>
            </div>
          </div>

          {/* Mép dưới của chứng chỉ: số liệu thật */}
          <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 bg-surface-2 px-5 py-4 text-sm text-ink-soft sm:px-10">
            {data ? (
              <>
                <span><b className="text-ink">{formatNumber(data.stats.totalCourses)}</b> khóa học đang mở</span>
                <span><b className="text-ink">{formatNumber(data.stats.totalInstructors)}</b> giảng viên</span>
                <span><b className="text-ink">{formatNumber(data.stats.totalStudents)}</b> học viên đã đăng ký</span>
              </>
            ) : (
              <Skeleton className="h-5 w-2/3" />
            )}
          </div>
        </div>
      </div>
    </section>
  )
}

/* ------------------------------------------------------------------ */
function Categories({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  return (
    <section ref={ref} className="container-page py-24 md:py-32" aria-labelledby="cat-title">
      <div className="grid gap-10 md:grid-cols-[1fr_2fr] md:gap-16">
        <div data-reveal>
          <h2 id="cat-title" className="text-title font-extrabold">Học theo lĩnh vực</h2>
          <p className="mt-4 max-w-sm text-lg text-ink-soft">
            Từ lập trình, thiết kế đến kỹ năng mềm. Chọn một lĩnh vực để xem toàn bộ khóa học trong đó.
          </p>
        </div>
        <ul className="flex flex-wrap content-start gap-3">
          {data.categories.filter((c) => c.courseCount > 0).map((c) => (
            <li key={c.id} data-reveal>
              <Link
                to={`/khoa-hoc?category=${c.slug}`}
                className="group inline-flex min-h-14 items-center gap-3 rounded-(--radius-control) bg-brand-50 px-6 text-lg font-bold text-brand-800 transition-[background-color,color,scale] duration-200 hover:bg-brand-600 hover:text-white active:scale-[0.97]"
              >
                {c.name}
                <span className="rounded-(--radius-control) bg-white px-2.5 py-0.5 text-sm font-semibold text-brand-700 tabular-nums">
                  {c.courseCount}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}

function CourseSection({ id, title, lead, courses, link }: { id: string; title: string; lead: string; courses: HomeData['featured']; link: string }) {
  const ref = useReveal<HTMLElement>()
  if (courses.length === 0) return null
  return (
    <section ref={ref} className="container-page py-20 md:py-28" aria-labelledby={id}>
      <div className="mb-10 flex flex-wrap items-end justify-between gap-4" data-reveal>
        <div>
          <h2 id={id} className="text-title font-extrabold">{title}</h2>
          <p className="mt-3 max-w-xl text-lg text-ink-soft">{lead}</p>
        </div>
        <Link to={link} className="btn-soft">Xem tất cả <ArrowRight className="size-4" aria-hidden="true" /></Link>
      </div>
      {/* Lấy bội số của 4 để hàng cuối không bị lẻ trên desktop */}
      <CourseGrid courses={courses.length >= 4 ? courses.slice(0, Math.min(8, Math.floor(courses.length / 4) * 4)) : courses} />
    </section>
  )
}

/** Bán chạy: một khóa lớn bên trái, danh sách gọn bên phải (khác bố cục lưới của các phần lân cận). */
function BestSelling({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  const [first, ...rest] = data.bestSelling
  if (!first) return null
  return (
    <section ref={ref} className="bg-surface-2 py-20 md:py-28" aria-labelledby="best-title">
      <div className="container-page">
        <h2 id="best-title" className="text-title font-extrabold" data-reveal>Học viên chọn nhiều nhất</h2>
        <div className="mt-10 grid gap-10 lg:grid-cols-[1.3fr_1fr]">
          <Link to={`/khoa-hoc/${first.slug}`} className="group flex flex-col gap-5" data-reveal>
            <div className="aspect-video overflow-hidden rounded-(--radius-card)">
              <img src={first.thumbnailUrl ?? ''} alt="" width={1200} height={675} loading="lazy"
                className="size-full object-cover transition-transform duration-700 ease-(--ease-out-strong) group-hover:scale-[1.03]" />
            </div>
            <div>
              <h3 className="text-3xl font-extrabold tracking-tight group-hover:text-brand-700">{first.title}</h3>
              <p className="mt-3 text-lg text-ink-soft">{first.shortDescription}</p>
              <div className="mt-4 flex flex-wrap items-center gap-x-6 gap-y-2">
                <RatingStars value={first.averageRating} count={first.reviewCount} />
                <span className="text-ink-soft">{formatNumber(first.enrollmentCount)} học viên · {formatDuration(first.totalDurationSeconds)}</span>
                <span className="text-2xl font-extrabold">{formatPrice(first.effectivePrice)}</span>
              </div>
            </div>
          </Link>
          <ul className="flex flex-col gap-2">
            {rest.slice(0, 5).map((c) => (
              <li key={c.id} data-reveal>
                <Link to={`/khoa-hoc/${c.slug}`} className="group grid grid-cols-[112px_1fr] gap-4 rounded-(--radius-card) p-2 transition-colors hover:bg-white sm:grid-cols-[144px_1fr]">
                  <img src={c.thumbnailUrl ?? ''} alt="" width={288} height={162} loading="lazy" className="aspect-video w-full rounded-lg object-cover" />
                  <div className="min-w-0">
                    <h3 className="line-clamp-2 font-bold group-hover:text-brand-700">{c.title}</h3>
                    <p className="mt-1 text-sm text-ink-soft">{c.instructor.fullName} · {formatNumber(c.enrollmentCount)} học viên</p>
                    <p className="mt-1 font-extrabold">{formatPrice(c.effectivePrice)}</p>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  )
}

/** Tổng số và "phiếu" cộng ra tổng đó: số khóa học theo từng lĩnh vực. */
function StatsReceipt({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  const rows = data.categories.filter((c) => c.courseCount > 0)
  const total = rows.reduce((s, c) => s + c.courseCount, 0)
  return (
    <section ref={ref} className="bg-brand-600 py-20 text-white md:py-28" aria-labelledby="stats-title">
      <div className="container-page grid gap-12 md:grid-cols-[1.2fr_1fr] md:items-stretch">
        <div className="flex flex-col justify-between gap-10" data-reveal>
          <div>
            <h2 id="stats-title" className="text-title font-extrabold">Thư viện đang mở</h2>
            <p className="mt-4 max-w-md text-lg text-brand-50">
              Mỗi khóa học đều qua kiểm duyệt trước khi xuất bản
              {data.stats.totalCertificates > 0 ? `, ${formatNumber(data.stats.totalCertificates)} chứng chỉ đã được cấp.` : ' và có đánh giá công khai từ học viên.'}
            </p>
          </div>
          <div>
            <p className="text-[clamp(7rem,4rem+14vw,15rem)] leading-[0.8] font-extrabold tracking-[-0.05em] tabular-nums">{total}</p>
            <p className="mt-4 text-lg text-brand-50">khóa học, dạy bởi {data.stats.totalInstructors} giảng viên</p>
          </div>
        </div>
        <div className="paper-receipt self-start bg-white px-6 pt-7 pb-10 text-ink sm:px-8" data-reveal>
          <p className="text-center font-extrabold">EduViet · Phiếu thư viện</p>
          <p className="mt-1 text-center text-sm text-ink-muted">Số khóa học theo lĩnh vực</p>
          <ul className="mt-6 flex flex-col gap-3 tabular-nums">
            {rows.map((c) => (
              <li key={c.id} className="flex items-baseline">
                <Link to={`/khoa-hoc?category=${c.slug}`} className="hover:text-brand-700">{c.name}</Link>
                <span className="dot-leader" aria-hidden="true" />
                <span className="font-semibold">{c.courseCount}</span>
              </li>
            ))}
          </ul>
          <p className="mt-6 flex items-baseline bg-ink px-3 py-2 font-extrabold text-white tabular-nums">
            Tổng cộng <span className="ml-auto">{total}</span>
          </p>
        </div>
      </div>
    </section>
  )
}

function Instructors({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  if (data.featuredInstructors.length === 0) return null
  return (
    <section ref={ref} className="container-page py-20 md:py-28" aria-labelledby="ins-title">
      <h2 id="ins-title" className="text-title font-extrabold" data-reveal>Người đứng lớp</h2>
      <p className="mt-3 max-w-xl text-lg text-ink-soft" data-reveal>Giảng viên đang làm nghề, dạy từ chính dự án họ đã làm.</p>
      <ul className="mt-12 grid gap-x-10 gap-y-12 md:grid-cols-3">
        {data.featuredInstructors.slice(0, 6).map((ins) => (
          <li key={ins.id} data-reveal>
            <Link to={`/giang-vien/${ins.id}`} className="group flex flex-col gap-4">
              <Avatar name={ins.fullName} src={ins.avatarUrl} size={72} />
              <div>
                <h3 className="text-2xl font-extrabold tracking-tight group-hover:text-brand-700">{ins.fullName}</h3>
                <p className="mt-1 text-ink-soft">{ins.headline}</p>
              </div>
              <p className="text-sm text-ink-soft">
                {ins.totalCourses} khóa học · {formatNumber(ins.totalStudents)} học viên
                {ins.totalReviews > 0 && <> · điểm <span className="font-semibold text-accent-700">{ins.averageRating.toFixed(1)}/5</span></>}
              </p>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  )
}

/** Đánh giá in trên những mẩu giấy xé, xếp chồng nghiêng. */
function Testimonials({ data }: { data: HomeData }) {
  const ref = useReveal<HTMLElement>()
  const items = data.testimonials.slice(0, 3)
  if (items.length === 0) return null
  const tilt = ['md:-rotate-3', 'md:rotate-2', 'md:rotate-[4deg]']
  return (
    <section ref={ref} className="overflow-hidden bg-accent-500 py-20 md:py-28" aria-labelledby="quote-title">
      <div className="container-page">
        <h2 id="quote-title" className="text-title font-extrabold text-ink" data-reveal>Học viên nói gì</h2>
        <ul className="mt-12 grid gap-6 md:grid-cols-3 md:gap-0">
          {items.map((t, i) => (
            <li key={t.id} data-reveal className={`${tilt[i]} ${i > 0 ? 'md:-ml-6' : ''} ${i === 1 ? 'md:mt-10' : ''}`}>
              <figure className="paper-torn flex h-full flex-col gap-4 bg-white px-7 py-10">
                <RatingStars value={t.rating} size={16} showValue={false} />
                <blockquote className="text-lg leading-relaxed">“{t.comment}”</blockquote>
                <figcaption className="mt-auto flex items-center gap-3">
                  <Avatar name={t.userFullName} src={t.userAvatarUrl} size={44} />
                  <span>
                    <span className="block font-bold">{t.userFullName}</span>
                    <Link to={`/khoa-hoc/${t.courseSlug}`} className="text-sm text-ink-soft hover:text-brand-700">{t.courseTitle}</Link>
                  </span>
                </figcaption>
              </figure>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}

/** Lời mời hành động: tấm thẻ hồi đáp đặt nghiêng, bị mép section cắt. */
function ReplyCard() {
  const ref = useReveal<HTMLElement>()
  return (
    <section ref={ref} className="overflow-hidden bg-brand-700 text-white" aria-labelledby="cta-title">
      <div className="container-page grid gap-10 pt-20 md:grid-cols-[1fr_1.3fr] md:items-end md:pt-28">
        <p className="max-w-sm pb-16 text-xl text-brand-50 md:text-2xl" data-reveal>
          Tạo tài khoản mất một phút. Khóa học miễn phí đầu tiên đã sẵn sàng cho bạn.
        </p>
        <div className="translate-y-10 rotate-1 bg-white px-8 py-12 text-center text-ink sm:px-12 md:rotate-2" data-reveal>
          <h2 id="cta-title" className="text-3xl font-extrabold tracking-tight">Bắt đầu học hôm nay</h2>
          <p className="mx-auto mt-4 max-w-sm text-lg text-ink-soft">
            Lưu tiến độ trên mọi thiết bị, nhận chứng chỉ có mã xác minh khi hoàn thành.
          </p>
          <Link to="/dang-ky" className="btn mt-8 min-h-14 w-full rounded-none bg-ink text-base text-white hover:bg-brand-800">
            Tạo tài khoản miễn phí
          </Link>
          <p className="mt-6 pb-8 text-sm text-ink-muted">
            Đã có tài khoản? <Link to="/dang-nhap" className="font-semibold text-brand-700 hover:underline">Đăng nhập</Link>
          </p>
        </div>
      </div>
    </section>
  )
}

function HomeSkeleton() {
  return (
    <div className="container-page py-24">
      <Skeleton className="mb-10 h-12 w-72" />
      <CourseGrid courses={[]} loading />
    </div>
  )
}

export default function HomePage() {
  useDocumentTitle('Khóa học trực tuyến có chứng chỉ')
  const { data, loading, error, reload } = useAsync(() => catalogService.home(), [])
  const { isAuthenticated, initializing } = useAuth()

  return (
    <>
      <CertificateHero data={data} />
      {loading && <HomeSkeleton />}
      {error && <div className="container-page py-24"><ErrorState message={error} onRetry={reload} /></div>}
      {data && (
        <>
          <Categories data={data} />
          <CourseSection id="featured-title" title="Khóa học nổi bật" lead="Được đội ngũ EduViet chọn vì nội dung thực tế và đánh giá tốt."
            courses={data.featured} link="/khoa-hoc?sort=popular" />
          <BestSelling data={data} />
          <StatsReceipt data={data} />
          <CourseSection id="newest-title" title="Mới xuất bản" lead="Những khóa học vừa qua kiểm duyệt."
            courses={data.newest} link="/khoa-hoc?sort=newest" />
          <Instructors data={data} />
          <Testimonials data={data} />
          {/* Thẻ mời tạo tài khoản chỉ dành cho khách */}
          {!initializing && !isAuthenticated && <ReplyCard />}
        </>
      )}
    </>
  )
}
