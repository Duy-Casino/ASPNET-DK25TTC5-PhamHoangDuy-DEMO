import type { ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  Award, BadgeCheck, Briefcase, CalendarDays, Clock, ExternalLink, GraduationCap, MessageCircle, Star, Users, BookOpen, type LucideIcon,
} from 'lucide-react'
import { CourseGrid } from '../components/course/CourseCard'
import { MessageInstructorButton } from '../components/messaging/MessageInstructorButton'
import { Avatar, DecorBlobs, ErrorState, RatingStars, Skeleton, VerifiedBadge } from '../components/ui'
import { useAsync } from '../hooks/useAsync'
import { useSeo } from '../hooks/useSeo'
import { catalogService } from '../services/api'
import type { InstructorDetail, InstructorExperience } from '../types/api'
import { formatDate, formatNumber } from '../utils/format'
import NotFoundPage from './NotFoundPage'

const monthYear = new Intl.DateTimeFormat('vi-VN', { month: '2-digit', year: 'numeric', timeZone: 'Asia/Ho_Chi_Minh' })

function period(e: InstructorExperience): string | null {
  if (e.type === 'Certification') return e.startYear ? `Cấp năm ${e.startYear}${e.endYear ? ` · hết hạn ${e.endYear}` : ''}` : null
  if (!e.startYear && !e.endYear) return null
  return `${e.startYear ?? '?'} – ${e.endYear ?? 'nay'}`
}

function replyTime(hours: number | null): string | null {
  if (hours === null) return null
  if (hours < 1) return 'dưới 1 giờ'
  if (hours < 24) return `khoảng ${Math.round(hours)} giờ`
  return `khoảng ${Math.round(hours / 24)} ngày`
}

export default function InstructorPage() {
  const { id = '' } = useParams()
  const profile = useAsync(() => catalogService.instructor(id), [id], { retries: 2 })
  const courses = useAsync(() => catalogService.search({ instructorId: id, pageSize: 24, sort: 'popular' }), [id])
  const p = profile.data
  useSeo({
    title: p?.fullName,
    description: p ? `${p.fullName}${p.headline ? ` — ${p.headline}` : ''}. ${p.totalCourses} khóa học trên EduViet.` : null,
    image: p?.avatarUrl,
    path: `/giang-vien/${id}`,
    noindex: profile.status === 404,
    jsonLd: p ? {
      '@context': 'https://schema.org',
      '@type': 'Person',
      name: p.fullName,
      ...(p.headline ? { jobTitle: p.headline } : {}),
      ...(p.avatarUrl ? { image: p.avatarUrl } : {}),
      url: `${window.location.origin}/giang-vien/${p.id}`,
      ...(p.expertise.length ? { knowsAbout: p.expertise } : {}),
      sameAs: Object.values(p.links).filter(Boolean),
    } : null,
  })

  if (profile.status === 404) return <NotFoundPage />
  if (profile.error) return <div className="container-page py-24"><ErrorState message={profile.error} onRetry={profile.reload} /></div>
  if (!p) return <ProfileSkeleton />

  const work = p.experiences.filter((e) => e.type === 'Work')
  const education = p.experiences.filter((e) => e.type === 'Education')
  const certs = p.experiences.filter((e) => e.type === 'Certification')
  const links = [
    { label: 'Website', href: p.links.website },
    { label: 'LinkedIn', href: p.links.linkedIn },
    { label: 'YouTube', href: p.links.youtube },
    { label: 'Facebook', href: p.links.facebook },
  ].filter((l): l is { label: string; href: string } => !!l.href)

  return (
    <>
      {/* ---------------- Đầu trang ---------------- */}
      <section className="relative isolate overflow-hidden border-b border-line/70 bg-surface-2">
        <DecorBlobs variant="right" />
        <div className="container-page py-10 md:py-14">
          <nav aria-label="Đường dẫn" className="mb-6 text-sm text-ink-muted">
            <Link to="/" className="hover:text-brand-700">Trang chủ</Link> <span aria-hidden="true">/</span> <span>Giảng viên</span>
          </nav>
          <div className="flex flex-col gap-8 md:flex-row md:items-start">
            <div className="relative self-start">
              <span className="block rounded-full bg-white p-1.5 shadow-(--shadow-lift)">
                <Avatar name={p.fullName} src={p.avatarUrl} size={136} />
              </span>
              {p.isVerified && (
                <span className="absolute right-1 bottom-2 flex size-9 items-center justify-center rounded-full bg-white shadow-(--shadow-soft)" title="Giảng viên đã xác minh">
                  <BadgeCheck className="size-7 fill-brand-600 text-white" aria-hidden="true" />
                </span>
              )}
            </div>
            <div className="flex min-w-0 flex-1 flex-col gap-4">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="text-title font-semibold">{p.fullName}</h1>
                {p.isVerified && <VerifiedBadge />}
              </div>
              {p.headline && <p className="text-xl text-ink-soft">{p.headline}</p>}
              <p className="flex flex-wrap gap-x-5 gap-y-1 text-ink-soft">
                {p.yearsOfExperience !== null && (
                  <span className="inline-flex items-center gap-1.5"><Briefcase className="size-4 text-brand-600" aria-hidden="true" />{p.yearsOfExperience} năm kinh nghiệm</span>
                )}
                <span className="inline-flex items-center gap-1.5"><CalendarDays className="size-4 text-brand-600" aria-hidden="true" />Giảng dạy trên EduViet từ {monthYear.format(new Date(p.memberSince))}</span>
              </p>
              {p.expertise.length > 0 && (
                <ul className="flex flex-wrap gap-2" aria-label="Chuyên môn">
                  {p.expertise.map((t) => <li key={t} className="chip bg-white px-3 py-1.5 text-sm shadow-(--shadow-soft)">{t}</li>)}
                </ul>
              )}
              <div className="mt-2 flex flex-wrap gap-2">
                <MessageInstructorButton instructor={p} className="btn-primary min-h-12 px-6" />
                {links.map((l) => (
                  <a key={l.label} href={l.href} target="_blank" rel="noopener noreferrer nofollow" className="btn-outline min-h-12">
                    {l.label} <ExternalLink className="size-4" aria-hidden="true" />
                  </a>
                ))}
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ---------------- Số liệu uy tín ---------------- */}
      <section className="container-page -mt-px pt-8" aria-label="Số liệu giảng dạy">
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          <TrustStat icon={Users} label="Học viên" value={formatNumber(p.totalStudents)} />
          <TrustStat icon={BookOpen} label="Khóa học" value={String(p.totalCourses)} />
          <TrustStat icon={Star} label={p.totalReviews ? `${formatNumber(p.totalReviews)} đánh giá` : 'Chưa có đánh giá'} value={p.totalReviews ? p.averageRating.toFixed(1) : '—'} />
          <TrustStat icon={Award} label="Chứng chỉ đã cấp" value={formatNumber(p.certificatesIssued)} />
          <TrustStat icon={MessageCircle} label={p.response ? 'Tỷ lệ trả lời tin nhắn' : 'Tỷ lệ trả lời: chưa đủ dữ liệu'}
            value={p.response ? `${p.response.replyRatePercent}%` : '—'} />
        </dl>
      </section>

      <div className="container-page grid gap-10 py-12 lg:grid-cols-[1fr_340px] lg:items-start">
        <div className="flex min-w-0 flex-col gap-12">
          {p.bio && (
            <Block id="about" title="Giới thiệu">
              <p className="max-w-[70ch] text-lg leading-relaxed whitespace-pre-line text-ink-soft">{p.bio}</p>
            </Block>
          )}

          {(work.length > 0 || education.length > 0) && (
            <Block id="experience" title="Quá trình làm việc và học tập">
              <div className="grid gap-8 md:grid-cols-2">
                {work.length > 0 && <Timeline icon={Briefcase} heading="Kinh nghiệm làm việc" items={work} />}
                {education.length > 0 && <Timeline icon={GraduationCap} heading="Học vấn" items={education} />}
              </div>
            </Block>
          )}

          {certs.length > 0 && (
            <Block id="certs" title="Chứng nhận chuyên môn">
              <ul className="grid gap-4 sm:grid-cols-2">
                {certs.map((c) => (
                  <li key={c.id} className="card flex items-start gap-4 p-5">
                    <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-accent-50 text-accent-700"><Award className="size-5" aria-hidden="true" /></span>
                    <span className="min-w-0">
                      <span className="block font-semibold">{c.title}</span>
                      {c.organization && <span className="block text-sm text-ink-soft">{c.organization}</span>}
                      {period(c) && <span className="block text-sm text-ink-muted">{period(c)}</span>}
                      {c.credentialUrl && (
                        <a href={c.credentialUrl} target="_blank" rel="noopener noreferrer nofollow" className="mt-1 inline-flex items-center gap-1 text-sm font-semibold text-brand-700 hover:underline">
                          Xem chứng nhận <ExternalLink className="size-3.5" aria-hidden="true" />
                        </a>
                      )}
                    </span>
                  </li>
                ))}
              </ul>
            </Block>
          )}

          <Block id="courses" title={`Khóa học của ${p.fullName}`}>
            <CourseGrid courses={courses.data?.items ?? []} loading={courses.loading} skeletons={3} columns={3} eagerCount={0} />
          </Block>

          <Reviews p={p} />
        </div>

        {/* ---------------- Cột phải: vì sao tin tưởng ---------------- */}
        <aside className="card flex flex-col gap-5 p-6 lg:sticky lg:top-24" aria-label="Thông tin tin cậy">
          <h2 className="text-heading font-semibold">Thông tin xác thực</h2>
          <ul className="flex flex-col gap-4 text-ink-soft">
            <TrustRow icon={BadgeCheck} ok={p.isVerified}>
              {p.isVerified
                ? <>Đã xác minh danh tính và kinh nghiệm{p.verifiedAt && <> ngày {formatDate(p.verifiedAt)}</>}</>
                : 'Chưa xác minh danh tính'}
            </TrustRow>
            <TrustRow icon={Star} ok={p.totalReviews > 0}>
              {p.totalReviews > 0 ? <>Điểm {p.averageRating.toFixed(1)}/5 từ {formatNumber(p.totalReviews)} đánh giá của học viên đã mua</> : 'Chưa có đánh giá từ học viên'}
            </TrustRow>
            <TrustRow icon={Clock} ok={!!p.response}>
              {p.response
                ? <>Trả lời {p.response.replyRatePercent}% tin nhắn{replyTime(p.response.medianReplyHours) && <>, thường trong {replyTime(p.response.medianReplyHours)}</>} ({p.response.conversations} cuộc trao đổi, 12 tháng qua)</>
                : 'Chưa đủ cuộc trao đổi để tính tỷ lệ phản hồi'}
            </TrustRow>
            <TrustRow icon={Award} ok={p.certificatesIssued > 0}>
              {formatNumber(p.certificatesIssued)} chứng chỉ đã cấp cho học viên hoàn thành khóa học
            </TrustRow>
          </ul>
          <MessageInstructorButton instructor={p} className="btn-primary w-full" label="Đặt câu hỏi cho giảng viên" />
          <p className="text-sm text-ink-muted">
            Số liệu được tính tự động từ dữ liệu thật trên EduViet. Đánh giá chỉ đến từ học viên đã mua khóa học.
          </p>
        </aside>
      </div>
    </>
  )
}

function Block({ id, title, children }: { id: string; title: string; children: ReactNode }) {
  return (
    <section aria-labelledby={`ins-${id}`}>
      <h2 id={`ins-${id}`} className="mb-6 text-2xl font-semibold">{title}</h2>
      {children}
    </section>
  )
}

function TrustStat({ icon: Icon, label, value }: { icon: LucideIcon; label: string; value: string }) {
  return (
    <div className="card flex flex-col gap-2 p-5">
      <Icon className="size-5 text-brand-600" aria-hidden="true" />
      <dd className="order-2 font-display text-2xl font-semibold tabular-nums">{value}</dd>
      <dt className="order-3 text-sm text-ink-soft">{label}</dt>
    </div>
  )
}

function TrustRow({ icon: Icon, ok, children }: { icon: LucideIcon; ok: boolean; children: ReactNode }) {
  return (
    <li className="flex gap-3">
      <span className={`flex size-8 shrink-0 items-center justify-center rounded-full ${ok ? 'bg-mint-50 text-mint-700' : 'bg-surface-3 text-ink-muted'}`}>
        <Icon className="size-4" aria-hidden="true" />
      </span>
      <span className="pt-1 text-sm leading-relaxed">{children}</span>
    </li>
  )
}

function Timeline({ icon: Icon, heading, items }: { icon: LucideIcon; heading: string; items: InstructorExperience[] }) {
  return (
    <div>
      <h3 className="mb-4 flex items-center gap-2 font-semibold"><Icon className="size-5 text-brand-600" aria-hidden="true" />{heading}</h3>
      <ol className="relative flex flex-col gap-6 border-l-2 border-brand-100 pl-6">
        {items.map((e) => (
          <li key={e.id} className="relative">
            <span className={`absolute top-1.5 -left-[31px] size-3.5 rounded-full border-2 border-white ${e.endYear === null && e.type === 'Work' ? 'bg-mint-500' : 'bg-brand-600'}`} aria-hidden="true" />
            <p className="font-semibold">{e.title}</p>
            {e.organization && <p className="text-ink-soft">{e.organization}</p>}
            {period(e) && <p className="text-sm text-ink-muted">{period(e)}</p>}
            {e.description && <p className="mt-1.5 text-sm leading-relaxed text-ink-soft">{e.description}</p>}
          </li>
        ))}
      </ol>
    </div>
  )
}

function Reviews({ p }: { p: InstructorDetail }) {
  const total = Object.values(p.ratingDistribution).reduce((a, b) => a + b, 0)
  if (total === 0) return null
  return (
    <section aria-labelledby="ins-reviews">
      <h2 id="ins-reviews" className="mb-6 text-2xl font-semibold">Học viên đánh giá</h2>
      <div className="grid gap-8 md:grid-cols-[240px_1fr]">
        <div className="card flex flex-col gap-3 p-6">
          <p className="font-display text-5xl font-semibold">{p.averageRating.toFixed(1)}</p>
          <RatingStars value={p.averageRating} showValue={false} />
          <p className="text-sm text-ink-soft">{formatNumber(total)} đánh giá trên mọi khóa học</p>
          <ul className="mt-2 flex flex-col gap-1.5">
            {[5, 4, 3, 2, 1].map((star) => {
              const n = p.ratingDistribution[String(star)] ?? 0
              return (
                <li key={star} className="flex items-center gap-2 text-sm">
                  <span className="w-6 tabular-nums">{star}★</span>
                  <span className="h-2 flex-1 overflow-hidden rounded-full bg-surface-3">
                    <span className="block h-full rounded-full bg-accent-500" style={{ width: `${(n / total) * 100}%` }} />
                  </span>
                  <span className="w-6 text-right text-ink-muted tabular-nums">{n}</span>
                </li>
              )
            })}
          </ul>
        </div>
        <ul className="flex flex-col gap-4">
          {p.recentReviews.map((r) => (
            <li key={r.id} className="card p-5">
              <div className="flex items-center gap-3">
                <Avatar name={r.userFullName} src={r.userAvatarUrl} size={40} />
                <div className="min-w-0">
                  <p className="font-semibold">{r.userFullName}</p>
                  <p className="text-sm text-ink-muted">{formatDate(r.createdAt)} · <Link to={`/khoa-hoc/${r.courseSlug}`} className="hover:text-brand-700">{r.courseTitle}</Link></p>
                </div>
                <span className="ml-auto"><RatingStars value={r.rating} size={14} showValue={false} /></span>
              </div>
              {r.comment && <p className="mt-3 leading-relaxed">{r.comment}</p>}
              {r.instructorReply && (
                <p className="mt-3 rounded-lg bg-surface-2 p-3 text-sm text-ink-soft"><b className="text-ink">Giảng viên phản hồi:</b> {r.instructorReply}</p>
              )}
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}

function ProfileSkeleton() {
  return (
    <div className="container-page flex flex-col gap-6 py-14" aria-hidden="true">
      <div className="flex items-center gap-6"><Skeleton className="size-32 rounded-full" /><Skeleton className="h-24 w-full max-w-lg" /></div>
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">{Array.from({ length: 5 }, (_, i) => <Skeleton key={i} className="h-28" />)}</div>
      <Skeleton className="h-64" />
    </div>
  )
}
