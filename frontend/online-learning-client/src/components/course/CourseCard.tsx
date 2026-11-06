import { Link } from 'react-router-dom'
import { BookOpen, Clock, Users } from 'lucide-react'
import type { CourseSummary } from '../../types/api'
import { courseLevelLabel } from '../../types/common'
import { discountPercent, formatDuration, formatNumber, formatPrice } from '../../utils/format'
import { RatingStars, Skeleton } from '../ui'

/** Thẻ khóa học: ảnh trong khung bo góc, lĩnh vực, tên, giảng viên, đánh giá, giá. Cả thẻ là một liên kết. */
export function CourseCard({ course, eager = false }: { course: CourseSummary; eager?: boolean }) {
  const off = discountPercent(course.price, course.discountPrice)
  return (
    <Link
      to={`/khoa-hoc/${course.slug}`}
      className="group card card-hover flex flex-col p-3 outline-offset-4"
      data-reveal
    >
      <div className="relative aspect-video overflow-hidden rounded-xl bg-surface-3">
        {course.thumbnailUrl && (
          <img
            src={course.thumbnailUrl}
            alt=""
            width={800}
            height={450}
            loading={eager ? 'eager' : 'lazy'}
            decoding="async"
            className="size-full object-cover transition-transform duration-500 ease-(--ease-out-strong) group-hover:scale-[1.04]"
          />
        )}
        <div className="absolute top-3 left-3 flex flex-wrap gap-1.5">
          {course.enrollmentCount >= 5 && (
            <span className="rounded-full bg-sun-300 px-2.5 py-1 text-xs font-bold text-ink">Bán chạy</span>
          )}
          {off !== null && (
            <span className="rounded-full bg-white px-2.5 py-1 text-xs font-bold text-accent-700">-{off}%</span>
          )}
        </div>
      </div>
      <div className="flex flex-1 flex-col gap-2 px-2 pt-4 pb-1">
        <span className="chip self-start">{course.category.name}</span>
        <h3 className="line-clamp-2 text-[17px] leading-snug font-semibold group-hover:text-brand-700">{course.title}</h3>
        <p className="text-sm text-ink-soft">{course.instructor.fullName} · {courseLevelLabel[course.level]}</p>
        {course.reviewCount > 0 ? (
          <RatingStars value={course.averageRating} size={14} count={course.reviewCount} />
        ) : (
          <span className="text-sm text-ink-muted">Chưa có đánh giá</span>
        )}
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-muted">
          <span className="inline-flex items-center gap-1"><BookOpen className="size-4" aria-hidden="true" />{course.totalLessons} bài</span>
          <span className="inline-flex items-center gap-1"><Clock className="size-4" aria-hidden="true" />{formatDuration(course.totalDurationSeconds)}</span>
          <span className="inline-flex items-center gap-1"><Users className="size-4" aria-hidden="true" />{formatNumber(course.enrollmentCount)}</span>
        </p>
        <div className="mt-auto flex items-baseline gap-2 border-t border-line/70 pt-3">
          <span className="text-xl font-bold text-ink">{formatPrice(course.effectivePrice)}</span>
          {off !== null && <span className="text-sm text-ink-muted line-through">{formatPrice(course.price)}</span>}
        </div>
      </div>
    </Link>
  )
}

export function CourseCardSkeleton() {
  return (
    <div className="card flex flex-col gap-3 p-3" aria-hidden="true">
      <Skeleton className="aspect-video w-full rounded-xl" />
      <Skeleton className="h-5 w-4/5" />
      <Skeleton className="h-4 w-1/2" />
      <Skeleton className="h-6 w-1/3" />
    </div>
  )
}

export function CourseGrid({ courses, loading, skeletons = 8, columns = 4 }: { courses: CourseSummary[]; loading?: boolean; skeletons?: number; columns?: 3 | 4 }) {
  return (
    <div className={`grid grid-cols-1 gap-6 sm:grid-cols-2 ${columns === 4 ? 'lg:grid-cols-3 xl:grid-cols-4' : 'lg:grid-cols-2 xl:grid-cols-3'}`}>
      {loading
        ? Array.from({ length: skeletons }, (_, i) => <CourseCardSkeleton key={i} />)
        : courses.map((c, i) => <CourseCard key={c.id} course={c} eager={i < 4} />)}
    </div>
  )
}
