import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CheckCircle2 } from 'lucide-react'
import { PageHeader, Panel } from '../../components/dashboard'
import { EmptyState, ErrorState, Pagination, Skeleton } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { lessonQaService } from '../../services/learnerApi'
import { formatRelative } from '../../utils/format'

/** Câu hỏi của học viên trong các bài học của giảng viên; mở bài học để trả lời ngay dưới video. */
export function InstructorQuestionsPage() {
  useDocumentTitle('Hỏi đáp của học viên')
  const [unanswered, setUnanswered] = useState(true)
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => lessonQaService.instructorInbox(unanswered, page), [unanswered, page])

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Hỏi đáp của học viên" description="Trả lời ngay trong bài học để mọi học viên của khóa cùng xem." />
      <Panel actions={
        <div className="flex gap-2" role="tablist" aria-label="Lọc câu hỏi">
          <button type="button" role="tab" aria-selected={unanswered} className={unanswered ? 'btn-primary min-h-9' : 'btn-soft min-h-9'} onClick={() => { setUnanswered(true); setPage(1) }}>Chưa trả lời</button>
          <button type="button" role="tab" aria-selected={!unanswered} className={!unanswered ? 'btn-primary min-h-9' : 'btn-soft min-h-9'} onClick={() => { setUnanswered(false); setPage(1) }}>Tất cả</button>
        </div>
      }>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-40" /> : data?.items.length === 0 ? (
          <EmptyState title={unanswered ? 'Không còn câu hỏi chờ trả lời' : 'Chưa có câu hỏi'}>Câu hỏi mới sẽ hiện ở đây và trên chuông thông báo.</EmptyState>
        ) : (
          <ul className="flex flex-col divide-y divide-line">
            {data?.items.map((q) => (
              <li key={q.id} className="flex flex-wrap items-start gap-4 py-4">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-ink-muted">{q.courseTitle} · {q.lessonTitle}</p>
                  <p className="mt-1 line-clamp-3 whitespace-pre-line">{q.content}</p>
                  <p className="mt-1 text-sm text-ink-soft">
                    {q.authorName} · {formatRelative(q.createdAt)} · {q.answerCount} trả lời
                    {q.hasInstructorAnswer && <span className="ml-2 font-semibold text-brand-700">Bạn đã trả lời</span>}
                    {q.isResolved && <span className="ml-2 inline-flex items-center gap-1 font-semibold text-success"><CheckCircle2 className="size-4" aria-hidden="true" />Đã giải đáp</span>}
                  </p>
                </div>
                <Link to={`/hoc/${q.courseSlug}/bai/${q.lessonId}?tab=hoi-dap`} className="btn-soft">Mở và trả lời</Link>
              </li>
            ))}
          </ul>
        )}
        <div className="mt-4"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
    </div>
  )
}
