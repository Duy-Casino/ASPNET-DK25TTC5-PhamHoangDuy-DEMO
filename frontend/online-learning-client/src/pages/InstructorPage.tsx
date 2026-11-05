import { useParams } from 'react-router-dom'
import { CourseGrid } from '../components/course/CourseCard'
import { Avatar, ErrorState, Skeleton } from '../components/ui'
import { useAsync, useDocumentTitle } from '../hooks/useAsync'
import { catalogService } from '../services/api'
import { formatNumber } from '../utils/format'
import NotFoundPage from './NotFoundPage'

export default function InstructorPage() {
  const { id = '' } = useParams()
  const profile = useAsync(() => catalogService.instructor(id), [id])
  const courses = useAsync(() => catalogService.search({ instructorId: id, pageSize: 24, sort: 'popular' }), [id])
  useDocumentTitle(profile.data?.fullName)

  if (profile.status === 404) return <NotFoundPage />
  if (profile.error) return <div className="container-page py-24"><ErrorState message={profile.error} onRetry={profile.reload} /></div>

  const p = profile.data
  return (
    <>
      <section className="bg-brand-50">
        <div className="container-page grid gap-10 py-16 md:grid-cols-[1fr_auto] md:items-end">
          {p ? (
            <div className="flex flex-col gap-5">
              <Avatar name={p.fullName} src={p.avatarUrl} size={96} />
              <h1 className="text-title font-extrabold">{p.fullName}</h1>
              <p className="text-xl text-ink-soft">{p.headline}</p>
              {p.bio && <p className="max-w-[65ch] text-lg">{p.bio}</p>}
            </div>
          ) : (
            <Skeleton className="h-48 w-full max-w-xl" />
          )}
          {p && (
            <dl className="grid grid-cols-3 gap-8 md:gap-12">
              <Stat label="Học viên" value={formatNumber(p.totalStudents)} />
              <Stat label="Khóa học" value={String(p.totalCourses)} />
              <Stat label="Điểm đánh giá" value={p.totalReviews ? p.averageRating.toFixed(1) : '—'} />
            </dl>
          )}
        </div>
      </section>
      <section className="container-page py-16" aria-labelledby="ins-courses">
        <h2 id="ins-courses" className="text-2xl font-extrabold">Khóa học của {p?.fullName ?? 'giảng viên'}</h2>
        <div className="mt-8">
          <CourseGrid courses={courses.data?.items ?? []} loading={courses.loading} skeletons={4} />
        </div>
      </section>
    </>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-sm text-ink-soft">{label}</dt>
      <dd className="text-4xl font-extrabold tabular-nums">{value}</dd>
    </div>
  )
}
