import { useMemo } from 'react'
import { Modal, ProgressBar } from './dashboard'
import { EmptyState, ErrorState, Skeleton } from './ui'
import { useAsync } from '../hooks/useAsync'
import type { CourseLessonStats } from '../services/adminToolsApi'
import { formatNumber } from '../utils/format'

/** Tỷ lệ học viên hoàn thành từng bài: cho thấy học viên bỏ dở ở bài nào. Dùng chung cho Admin và giảng viên. */
export function LessonStatsModal({ courseId, title, load, onClose }: {
  courseId: string; title: string; onClose: () => void
  load: (id: string) => Promise<CourseLessonStats>
}) {
  const { data, loading, error, reload } = useAsync(() => load(courseId), [courseId])
  const drop = useMemo(() => {
    if (!data) return null
    let worst: { title: string; delta: number } | null = null
    data.lessons.forEach((l, i) => {
      if (i === 0) return
      const delta = data.lessons[i - 1].completedPercent - l.completedPercent
      if (delta > 0 && (!worst || delta > worst.delta)) worst = { title: l.title, delta }
    })
    return worst as { title: string; delta: number } | null
  }, [data])
  return (
    <Modal title={`Tỷ lệ hoàn thành từng bài: ${title}`} onClose={onClose} wide>
      {error ? <ErrorState message={error} onRetry={reload} /> : loading || !data ? <Skeleton className="h-64" /> : (
        <div className="flex flex-col gap-4">
          <p className="text-sm text-ink-soft">{formatNumber(data.activeLearners)} học viên đang có quyền học.
            {drop && <> Học viên rời bỏ nhiều nhất ở bài <b className="text-ink">“{drop.title}”</b> (giảm {Math.round(drop.delta)} điểm %).</>}</p>
          {data.lessons.length === 0 ? <EmptyState title="Khóa học chưa có bài học" /> : (
            <ol className="flex max-h-[60vh] flex-col gap-3 overflow-auto pr-1">
              {data.lessons.map((l, i) => (
                <li key={l.lessonId} className="grid gap-1 sm:grid-cols-[1fr_200px] sm:items-center sm:gap-4">
                  <div className="min-w-0">
                    <p className="truncate font-medium">{i + 1}. {l.title}{!l.isRequired && <span className="ml-2 text-xs font-normal text-ink-muted">(không bắt buộc)</span>}</p>
                    <p className="truncate text-xs text-ink-muted">{l.sectionTitle} · {formatNumber(l.completedCount)} học viên đã xong</p>
                  </div>
                  <ProgressBar value={l.completedPercent} label={`Hoàn thành bài ${l.title}`} />
                </li>
              ))}
            </ol>
          )}
        </div>
      )}
    </Modal>
  )
}

