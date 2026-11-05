import { Link } from 'react-router-dom'
import type { CourseSummary } from '../../types/api'
import { courseLevelLabel } from '../../types/common'
import { discountPercent, formatDuration, formatNumber, formatPrice } from '../../utils/format'
import { RatingStars, Skeleton } from '../ui'

/** Thẻ khóa học: ảnh, tên, giảng viên, đánh giá, giá. Cả thẻ là một liên kết. */
export function CourseCard({ course, eager = false }: { course: CourseSummary; eager?: boolean }) {
  const off = discountPercent(course.price, course.discountPrice)
  return (
    <Link
      to={`/khoa-hoc/${course.slug}`}
      className="group flex flex-col gap-3 rounded-(--radius-card) outline-offset-4"
      data-reveal
    >
      <div className="relative aspect-video overflow-hidden rounded-(--radius-card) bg-surface-3">
        {course.thumbnailUrl && (
          <img
            src={course.thumbnailUrl}
            alt=""
            width={800}
            height={450}
            loading={eager ? 'eager' : 'lazy'}
            className="size-full object-cover transition-transform duration-500 ease-(--ease-out-strong) group-hover:scale-[1.04]"
          />
        )}
        {course.enrollmentCount >= 5 && (
          <span className="absolute top-3 left-3 rounded-(--radius-control) bg-sun-300 px-3 py-1 text-sm font-semibold text-ink">Bán chạy</span>
        )}
      </div>
      <div className="flex flex-1 flex-col gap-1.5">
        <h3 className="line-clamp-2 text-lg leading-snug font-bold group-hover:text-brand-700">{course.title}</h3>
        <p className="text-sm text-ink-soft">{course.instructor.fullName}</p>
        {course.reviewCount > 0 ? (
          <RatingStars value={course.averageRating} size={14} count={course.reviewCount} />
        ) : (
          <span className="text-sm text-ink-muted">Chưa có đánh giá</span>
        )}
        <p className="text-sm text-ink-muted">
          {courseLevelLabel[course.level]} · {course.totalLessons} bài · {formatDuration(course.totalDurationSeconds)} · {formatNumber(course.enrollmentCount)} học viên
        </p>
        <div className="mt-auto flex items-baseline gap-2 pt-1">
          <span className="text-xl font-extrabold text-ink">{formatPrice(course.effectivePrice)}</span>
          {off !== null && (
            <>
              <span className="text-sm text-ink-muted line-through">{formatPrice(course.price)}</span>
              <span className="text-sm font-bold text-accent-700">-{off}%</span>
            </>
          )}
        </div>
      </div>
    </Link>
  )
}

export function CourseCardSkeleton() {
  return (
    <div className="flex flex-col gap-3" aria-hidden="true">
      <Skeleton className="aspect-video w-full" />
      <Skeleton className="h-5 w-4/5" />
      <Skeleton className="h-4 w-1/2" />
      <Skeleton className="h-6 w-1/3" />
    </div>
  )
}

export function CourseGrid({ courses, loading, skeletons = 8, columns = 4 }: { courses: CourseSummary[]; loading?: boolean; skeletons?: number; columns?: 3 | 4 }) {
  return (
    <div className={`grid grid-cols-1 gap-x-6 gap-y-10 sm:grid-cols-2 ${columns === 4 ? 'lg:grid-cols-4' : 'lg:grid-cols-2 xl:grid-cols-3'}`}>
      {loading
        ? Array.from({ length: skeletons }, (_, i) => <CourseCardSkeleton key={i} />)
        : courses.map((c, i) => <CourseCard key={c.id} course={c} eager={i < 4} />)}
    </div>
  )
}
