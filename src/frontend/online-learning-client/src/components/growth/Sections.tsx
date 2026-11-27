import { Link } from 'react-router-dom'
import { Layers } from 'lucide-react'
import { useAsync } from '../../hooks/useAsync'
import { bundleService, recommendationService, type BundleSummary, type RecommendedCourse } from '../../services/growthApi'
import { formatPrice } from '../../utils/format'
import { CourseCard } from '../course/CourseCard'

/** Lưới khóa học gợi ý, mỗi thẻ có nhãn lý do ("Học viên cũng mua" / "Cùng chủ đề"). */
export function RecommendationGrid({ items }: { items: RecommendedCourse[] }) {
  return (
    <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 xl:grid-cols-4">
      {items.map((r) => (
        <div key={r.course.id} className="flex flex-col gap-2">
          <span className="text-xs font-semibold tracking-wide text-ink-muted uppercase">{r.reason}</span>
          <CourseCard course={r.course} />
        </div>
      ))}
    </div>
  )
}

/** "Học viên cũng mua" ở cuối trang chi tiết khóa học. */
export function RelatedCourses({ slug }: { slug: string }) {
  const { data } = useAsync(() => recommendationService.forCourse(slug, 4), [slug])
  if (!data?.length) return null
  return (
    <section aria-labelledby="related-h" className="container-page pb-16">
      <h2 id="related-h" className="text-2xl font-semibold">Học viên cũng quan tâm</h2>
      <div className="mt-6"><RecommendationGrid items={data} /></div>
    </section>
  )
}

export function BundleCard({ bundle }: { bundle: BundleSummary }) {
  return (
    <Link to={`/combo/${bundle.slug}`} className="group card card-hover flex flex-col gap-3 p-5">
      <span className="inline-flex items-center gap-2 self-start rounded-full bg-accent-50 px-3 py-1 text-sm font-bold text-accent-700">
        <Layers className="size-4" aria-hidden="true" />Combo {bundle.courses.length} khóa · tiết kiệm {bundle.savingPercent}%
      </span>
      <h3 className="text-lg font-semibold group-hover:text-brand-700">{bundle.title}</h3>
      <ul className="flex flex-col gap-1 text-sm text-ink-soft">
        {bundle.courses.map((c) => <li key={c.id} className="line-clamp-1">• {c.title}</li>)}
      </ul>
      <p className="mt-auto flex items-baseline gap-2 border-t border-line/70 pt-3">
        <span className="text-xl font-bold">{formatPrice(bundle.price)}</span>
        <span className="text-sm text-ink-muted line-through">{formatPrice(bundle.originalTotal)}</span>
      </p>
    </Link>
  )
}

/** Các combo có chứa khóa học đang xem. */
export function CourseBundles({ courseId }: { courseId: string }) {
  const { data } = useAsync(() => bundleService.list(courseId), [courseId])
  if (!data?.length) return null
  return (
    <section aria-labelledby="bundles-h" data-reveal>
      <h2 id="bundles-h" className="text-2xl font-semibold">Mua theo combo, tiết kiệm hơn</h2>
      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        {data.map((b) => <BundleCard key={b.id} bundle={b} />)}
      </div>
    </section>
  )
}
