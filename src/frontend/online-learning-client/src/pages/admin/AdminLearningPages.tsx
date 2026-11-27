import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { ArrowLeft, CheckCircle2, Circle, UserPlus } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, ProgressBar } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Pagination, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { catalogService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import {
  adminLearningService, learningStateLabel, learningStateStyle, supportStatusLabel, supportStatusStyle, supportTypeLabel,
  type AdminEnrollmentDetail, type AdminQuizProgress, type LearningState, type SupportRequest, type SupportRequestStatus,
} from '../../services/learnerApi'
import { formatClock, formatDateTime, formatRelative } from '../../utils/format'

function StateBadge({ state }: { state: LearningState }) {
  return <span className={`inline-flex rounded-(--radius-control) px-2.5 py-0.5 text-sm font-semibold whitespace-nowrap ${learningStateStyle[state]}`}>{learningStateLabel[state]}</span>
}

// =================== Tiến độ học tập: tổng quan + danh sách ===================
export function AdminLearningPage() {
  useDocumentTitle('Tiến độ học tập')
  const [params, setParams] = useSearchParams()
  const state = (params.get('state') ?? '') as LearningState | ''
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const overview = useAsync(() => adminLearningService.overview(), [])
  const list = useAsync(() => adminLearningService.list({ state, keyword: q || undefined, page }), [state, q, page])
  const [enrolling, setEnrolling] = useState(false)
  const setState = (s: LearningState | '') => { setParams(s ? { state: s } : {}, { replace: true }); setPage(1) }
  const o = overview.data

  const cards: { s: LearningState | ''; label: string; value?: number; tone?: string }[] = [
    { s: '', label: 'Tất cả lượt ghi danh', value: o?.total },
    { s: 'not-started', label: 'Chưa bắt đầu', value: o?.notStarted },
    { s: 'in-progress', label: 'Đang học', value: o?.inProgress },
    { s: 'completed', label: 'Hoàn thành', value: o?.completed, tone: 'text-success' },
    { s: 'stuck', label: 'Hết lượt, chưa đạt', value: o?.stuck, tone: 'text-danger' },
    { s: 'inactive', label: 'Bỏ dở > 14 ngày', value: o?.inactive, tone: 'text-warning' },
    { s: 'no-certificate', label: 'Đủ điều kiện, chưa có chứng chỉ', value: o?.noCertificate },
    { s: 'revoked', label: 'Đã thu hồi quyền học', value: o?.revoked },
  ]

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Tiến độ học tập"
        description={o ? `Tỷ lệ hoàn thành ${o.completionRate}% · ${o.failedAttempts30Days} lượt kiểm tra chưa đạt trong 30 ngày · ${o.openSupportRequests} yêu cầu hỗ trợ đang chờ` : undefined}
        actions={<>
          <Link to="/quan-tri/ho-tro-hoc-tap" className="btn-soft">Yêu cầu hỗ trợ{o?.openSupportRequests ? ` (${o.openSupportRequests})` : ''}</Link>
          <button type="button" className="btn-primary" onClick={() => setEnrolling(true)}><UserPlus className="size-4" aria-hidden="true" />Cấp quyền học</button>
        </>} />

      {overview.error ? <ErrorState message={overview.error} onRetry={overview.reload} /> : (
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4" role="tablist" aria-label="Lọc theo tình trạng">
          {cards.map((c) => (
            <button key={c.s || 'all'} type="button" role="tab" aria-selected={state === c.s} onClick={() => setState(c.s)}
              className={`rounded-(--radius-card) p-4 text-left transition-colors ${state === c.s ? 'bg-brand-600 text-white' : 'bg-white hover:bg-brand-50'}`}>
              <span className={`block text-sm ${state === c.s ? 'text-brand-100' : 'text-ink-soft'}`}>{c.label}</span>
              <span className={`mt-1 block text-2xl font-bold tabular-nums ${state === c.s ? '' : c.tone ?? ''}`}>{c.value ?? '—'}</span>
            </button>
          ))}
        </div>
      )}

      <Panel>
        <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Tên, email học viên hoặc tên khóa học" className="mb-6 max-w-md" />
        {list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : list.loading ? <Skeleton className="h-48" /> : list.data?.items.length === 0 ? <EmptyState title="Không có học viên phù hợp" /> : (
          <DataTable minWidth={1050} head={<><th>Học viên</th><th>Khóa học</th><th className="w-44">Tiến độ</th><th>Bài kiểm tra</th><th>Tình trạng</th><th>Lần học gần nhất</th><th /></>}>
            {list.data?.items.map((e) => (
              <tr key={e.enrollmentId}>
                <td><p className="font-medium">{e.fullName}</p><p className="text-ink-muted">{e.email}</p></td>
                <td><p className="font-medium">{e.courseTitle}</p><p className="text-xs text-ink-muted">{e.source}</p></td>
                <td><ProgressBar value={e.progressPercent} label={`Tiến độ ${e.fullName}`} /><p className="text-xs text-ink-muted">{e.completedLessons}/{e.requiredLessons} bài bắt buộc</p></td>
                <td className="text-sm">{e.finalAttemptsUsed === 0 ? <span className="text-ink-muted">Chưa làm</span> : (
                  <><span className={e.finalPassed ? 'font-semibold text-success' : 'font-semibold text-danger'}>{e.finalPassed ? 'Đạt' : 'Chưa đạt'}</span>
                    {e.finalBestScore !== null && ` · ${Math.round(e.finalBestScore)}%`}
                    <span className="block text-xs text-ink-muted">{e.finalAttemptsUsed}{e.finalMaxAttempts ? `/${e.finalMaxAttempts}` : ''} lượt</span></>
                )}</td>
                <td><StateBadge state={e.state} />{e.hasCertificate && <span className="mt-1 block text-xs text-success">Có chứng chỉ</span>}</td>
                <td className="text-ink-soft">{e.lastAccessedAt ? formatRelative(e.lastAccessedAt) : 'Chưa vào học'}</td>
                <td><Link to={`/quan-tri/hoc-tap/${e.enrollmentId}`} className="btn-soft min-h-9 px-3">Chi tiết</Link></td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={list.data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
      {enrolling && <ManualEnrollModal onClose={() => setEnrolling(false)} onDone={() => { setEnrolling(false); list.reload(); overview.reload() }} />}
    </div>
  )
}

function ManualEnrollModal({ onClose, onDone }: { onClose: () => void; onDone: () => void }) {
  const courses = useAsync(() => catalogService.search({ pageSize: 100, sort: 'popular' }), [])
  const [f, setF] = useState({ email: '', courseId: '', reason: '' })
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await adminLearningService.manualEnroll(f.email.trim(), f.courseId, f.reason.trim())
      toast.success('Đã cấp quyền học')
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title="Cấp quyền học thủ công" onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <p className="text-sm text-ink-soft">Dùng khi học viên đã thanh toán ngoài hệ thống hoặc cổng thanh toán gặp sự cố. Học viên nhận thông báo và vào học ngay.</p>
        <Field label="Email học viên" type="email" value={f.email} onChange={(e) => setF({ ...f, email: e.target.value })} />
        <label className="flex flex-col gap-1.5">
          <span className="text-sm font-semibold">Khóa học</span>
          <select className="input" value={f.courseId} onChange={(e) => setF({ ...f, courseId: e.target.value })}>
            <option value="">— Chọn khóa học —</option>
            {courses.data?.items.map((c) => <option key={c.id} value={c.id}>{c.title}</option>)}
          </select>
        </label>
        <TextArea label="Lý do (ghi vào nhật ký)" rows={2} maxLength={500} value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} />
        <button type="submit" className="btn-primary" disabled={saving || !f.email || !f.courseId || f.reason.trim().length < 5}>{saving && <Spinner className="size-4" />}Cấp quyền học</button>
      </form>
    </Modal>
  )
}

// =================== Chi tiết học viên trong một khóa học ===================
type Action =
  | { kind: 'grant'; quiz: AdminQuizProgress }
  | { kind: 'lesson'; lessonId: string; title: string; completed: boolean }
  | { kind: 'reset' | 'recalculate' | 'revoke' | 'restore' }

const actionTitle = (a: Action) =>
  a.kind === 'grant' ? `Cấp thêm lượt: ${a.quiz.title}`
  : a.kind === 'lesson' ? `${a.completed ? 'Đánh dấu hoàn thành' : 'Bỏ hoàn thành'}: ${a.title}`
  : { reset: 'Đặt lại tiến độ học', recalculate: 'Tính lại tiến độ và cấp chứng chỉ', revoke: 'Thu hồi quyền học', restore: 'Khôi phục quyền học' }[a.kind]

export function AdminEnrollmentDetailPage() {
  const { id = '' } = useParams()
  const { data, loading, error, reload } = useAsync(() => adminLearningService.detail(id), [id])
  const [action, setAction] = useState<Action | null>(null)
  useDocumentTitle(data ? `Tiến độ: ${data.summary.fullName}` : 'Tiến độ học viên')

  if (error) return <ErrorState message={error} onRetry={reload} />
  if (loading || !data) return <Skeleton className="h-96" />
  const s = data.summary
  const revoked = s.status === 'Revoked'

  return (
    <div className="flex flex-col gap-6">
      <Link to="/quan-tri/hoc-tap" className="inline-flex w-fit items-center gap-1 text-sm font-semibold text-brand-700 hover:underline"><ArrowLeft className="size-4" aria-hidden="true" />Tiến độ học tập</Link>
      <PageHeader title={s.fullName} description={`${s.email} · ${s.courseTitle} · ghi danh ${formatDateTime(s.enrolledAt)} (${s.source})`}
        actions={<div className="flex flex-wrap gap-2">
          {!revoked && <button type="button" className="btn-soft" onClick={() => setAction({ kind: 'recalculate' })}>Tính lại tiến độ</button>}
          {!revoked && <button type="button" className="btn-ghost" onClick={() => setAction({ kind: 'reset' })}>Đặt lại tiến độ</button>}
          {revoked
            ? <button type="button" className="btn-primary" onClick={() => setAction({ kind: 'restore' })}>Khôi phục quyền học</button>
            : <button type="button" className="btn-ghost text-danger" onClick={() => setAction({ kind: 'revoke' })}>Thu hồi quyền học</button>}
        </div>} />

      <div className="grid gap-4 md:grid-cols-4">
        <Fact label="Tình trạng"><StateBadge state={s.state} /></Fact>
        <Fact label="Tiến độ"><ProgressBar value={s.progressPercent} /><span className="text-xs text-ink-muted">{s.completedLessons}/{s.requiredLessons} bài bắt buộc</span></Fact>
        <Fact label="Lần học gần nhất">{s.lastAccessedAt ? formatDateTime(s.lastAccessedAt) : 'Chưa vào học'}</Fact>
        <Fact label="Chứng chỉ">{data.certificateCode ? <Link to={`/xac-minh/${data.certificateCode}`} className="font-semibold text-brand-700 hover:underline">{data.certificateCode}</Link> : 'Chưa có'}</Fact>
      </div>

      <Panel title="Bài kiểm tra">
        {data.quizzes.length === 0 ? <p className="text-ink-soft">Khóa học không có bài kiểm tra.</p> : (
          <ul className="flex flex-col gap-4">
            {data.quizzes.map((q) => {
              const max = q.baseMaxAttempts === null ? null : q.baseMaxAttempts + q.extraAttempts
              const exhausted = !q.isPassed && max !== null && q.attemptsUsed >= max
              return (
                <li key={q.quizId} className={`rounded-(--radius-card) p-4 ${exhausted ? 'bg-danger-50' : 'bg-surface-2'}`}>
                  <div className="flex flex-wrap items-center gap-3">
                    <p className="font-semibold">{q.title}{q.isFinalExam && <span className="ml-2 text-xs font-bold text-accent-700">CUỐI KHÓA</span>}{q.isRequired && <span className="ml-2 text-xs text-ink-muted">bắt buộc</span>}</p>
                    <span className={`text-sm font-semibold ${q.isPassed ? 'text-success' : q.attemptsUsed ? 'text-danger' : 'text-ink-muted'}`}>
                      {q.isPassed ? 'Đạt' : q.attemptsUsed ? 'Chưa đạt' : 'Chưa làm'}{q.bestScore !== null && ` · cao nhất ${Math.round(q.bestScore)}%`} · cần {q.passingScore}%
                    </span>
                    <span className="text-sm text-ink-soft">{q.attemptsUsed}{max !== null ? `/${max}` : ''} lượt{q.extraAttempts > 0 && ` (cấp thêm ${q.extraAttempts})`}</span>
                    {!q.isPassed && q.baseMaxAttempts !== null && !revoked && (
                      <button type="button" className={exhausted ? 'btn-primary ml-auto min-h-9 px-3' : 'btn-soft ml-auto min-h-9 px-3'} onClick={() => setAction({ kind: 'grant', quiz: q })}>Cấp thêm lượt</button>
                    )}
                  </div>
                  {q.attempts.length > 0 && (
                    <ul className="mt-2 flex flex-wrap gap-2 text-xs">
                      {q.attempts.map((a) => (
                        <li key={a.id} className={`rounded px-2 py-1 ${a.isPassed ? 'bg-success-50 text-success' : a.submittedAt ? 'bg-white text-danger' : 'bg-white text-ink-muted'}`}>
                          Lượt {a.attemptNumber}: {a.submittedAt ? `${Math.round(a.scorePercent)}% · ${formatDateTime(a.submittedAt)}` : 'chưa nộp'}
                        </li>
                      ))}
                    </ul>
                  )}
                </li>
              )
            })}
          </ul>
        )}
      </Panel>

      <div className="grid gap-6 xl:grid-cols-[1.2fr_1fr]">
        <Panel title="Bài học">
          <DataTable minWidth={560} head={<><th>Bài học</th><th>Trạng thái</th><th>Vị trí xem</th><th /></>}>
            {data.lessons.map((l) => (
              <tr key={l.lessonId}>
                <td><p className="font-medium">{l.title}</p><p className="text-xs text-ink-muted">{l.sectionTitle}{!l.isRequired && ' · không bắt buộc'}</p></td>
                <td>{l.isCompleted
                  ? <span className="inline-flex items-center gap-1 text-sm font-semibold text-success"><CheckCircle2 className="size-4" aria-hidden="true" />{l.completedAt ? formatDateTime(l.completedAt) : 'Hoàn thành'}</span>
                  : <span className="inline-flex items-center gap-1 text-sm text-ink-muted"><Circle className="size-4" aria-hidden="true" />Chưa xong</span>}</td>
                <td className="text-sm tabular-nums text-ink-soft">{l.lastPositionSeconds > 0 ? formatClock(l.lastPositionSeconds) : '—'}</td>
                <td>{!revoked && (
                  <button type="button" className="btn-ghost min-h-9 px-2 text-sm" onClick={() => setAction({ kind: 'lesson', lessonId: l.lessonId, title: l.title, completed: !l.isCompleted })}>
                    {l.isCompleted ? 'Bỏ hoàn thành' : 'Đánh dấu xong'}
                  </button>
                )}</td>
              </tr>
            ))}
          </DataTable>
        </Panel>

        <Panel title="Hoạt động">
          <ol className="flex flex-col gap-3" aria-label="Dòng thời gian hoạt động">
            {data.timeline.map((t, i) => (
              <li key={i} className="flex gap-3">
                <span className={`mt-1.5 size-2.5 shrink-0 rounded-full ${t.kind === 'quiz-fail' ? 'bg-danger' : t.kind === 'quiz-pass' || t.kind === 'certificate' || t.kind === 'complete' ? 'bg-success' : t.kind === 'admin' ? 'bg-accent-500' : 'bg-brand-500'}`} aria-hidden="true" />
                <div className="min-w-0">
                  <p className="text-sm font-medium">{t.title}</p>
                  <p className="text-xs text-ink-muted">{formatDateTime(t.at)}{t.detail && ` · ${t.detail}`}</p>
                </div>
              </li>
            ))}
          </ol>
        </Panel>
      </div>

      {data.supportRequests.length > 0 && (
        <Panel title="Yêu cầu hỗ trợ của học viên">
          <SupportList items={data.supportRequests} onChanged={reload} />
        </Panel>
      )}

      {action && <ActionModal enrollmentId={s.enrollmentId} action={action} detail={data} onClose={() => setAction(null)} onDone={() => { setAction(null); reload() }} />}
    </div>
  )
}

function Fact({ label, children }: { label: string; children: ReactNode }) {
  return <div className="rounded-(--radius-card) bg-white p-4"><p className="text-sm text-ink-soft">{label}</p><div className="mt-1 font-semibold">{children}</div></div>
}

function ActionModal({ enrollmentId, action, detail, onClose, onDone }: {
  enrollmentId: string; action: Action; detail: AdminEnrollmentDetail; onClose: () => void; onDone: () => void
}) {
  const [reason, setReason] = useState('')
  const [extra, setExtra] = useState(1)
  const [saving, setSaving] = useState(false)
  const danger = action.kind === 'reset' || action.kind === 'revoke'

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (reason.trim().length < 5) { toast.error('Bắt buộc ghi lý do (ít nhất 5 ký tự).'); return }
    setSaving(true)
    try {
      const r = reason.trim()
      switch (action.kind) {
        case 'grant': await adminLearningService.grantAttempts(enrollmentId, action.quiz.quizId, extra, r); break
        case 'lesson': await adminLearningService.setLesson(enrollmentId, action.lessonId, action.completed, r); break
        case 'reset': await adminLearningService.reset(enrollmentId, r); break
        case 'revoke': await adminLearningService.revoke(enrollmentId, r); break
        case 'restore': await adminLearningService.restore(enrollmentId, r); break
        case 'recalculate': {
          const p = await adminLearningService.recalculate(enrollmentId, r)
          toast.info(p.certificateCode ? `Tiến độ ${Math.round(p.progressPercent)}% · đã cấp chứng chỉ ${p.certificateCode}` : `Tiến độ ${Math.round(p.progressPercent)}%`)
          break
        }
      }
      toast.success('Đã thực hiện và ghi nhật ký')
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  const note = {
    grant: 'Học viên nhận thông báo và làm tiếp được ngay. Yêu cầu xin thêm lượt đang mở (nếu có) được đóng tự động.',
    lesson: 'Dùng khi tiến độ không được ghi nhận đúng (ví dụ mất kết nối khi xem). Tiến độ và chứng chỉ được tính lại.',
    reset: 'Xóa trạng thái hoàn thành của mọi bài học để học viên học lại từ đầu. Lịch sử bài kiểm tra vẫn giữ. Không làm được khi đã có chứng chỉ.',
    recalculate: 'Tính lại % hoàn thành từ dữ liệu thật; nếu đủ điều kiện (100% bài bắt buộc, đạt bài kiểm tra bắt buộc) thì cấp chứng chỉ.',
    revoke: 'Học viên không vào học được nữa. Dùng khi giao dịch bị hủy, vi phạm điều khoản...',
    restore: 'Học viên vào học lại được, giữ nguyên tiến độ cũ.',
  }[action.kind]

  return (
    <Modal title={actionTitle(action)} onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <p className={`rounded-(--radius-control) p-3 text-sm ${danger ? 'bg-danger-50' : 'bg-surface-2'}`}>
          <b>{detail.summary.fullName}</b> · {detail.summary.courseTitle}<br />{note}
        </p>
        {action.kind === 'grant' && (
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold">Số lượt cấp thêm</span>
            <select className="input w-32" value={extra} onChange={(e) => setExtra(Number(e.target.value))}>
              {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{n}</option>)}
            </select>
          </label>
        )}
        <TextArea label="Lý do (ghi vào nhật ký, bắt buộc)" rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
        <button type="submit" className={danger ? 'btn-primary bg-danger hover:bg-danger' : 'btn-primary'} disabled={saving}>{saving && <Spinner className="size-4" />}Xác nhận</button>
      </form>
    </Modal>
  )
}

// =================== Yêu cầu hỗ trợ học tập ===================
function SupportList({ items, onChanged }: { items: SupportRequest[]; onChanged: () => void }) {
  const [responding, setResponding] = useState<SupportRequest | null>(null)
  return (
    <>
      <ul className="flex flex-col divide-y divide-line">
        {items.map((r) => (
          <li key={r.id} className="flex flex-wrap items-start gap-4 py-4">
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-center gap-2">
                <b>{supportTypeLabel[r.type]}</b>
                <span className={`rounded px-2 py-0.5 text-xs font-semibold ${supportStatusStyle[r.status]}`}>{supportStatusLabel[r.status]}</span>
                <span className="text-sm text-ink-muted">{formatDateTime(r.createdAt)}</span>
              </p>
              <p className="text-sm text-ink-soft">{r.userName} ({r.userEmail}) · {r.courseTitle}</p>
              <p className="mt-1 whitespace-pre-line">{r.message}</p>
              {r.response && <p className="mt-1 text-sm">Phản hồi: {r.response}</p>}
            </div>
            <div className="flex gap-2">
              {r.enrollmentId && <Link to={`/quan-tri/hoc-tap/${r.enrollmentId}`} className="btn-soft min-h-9 px-3">Xem tiến độ</Link>}
              {r.status === 'Open' && <button type="button" className="btn-primary min-h-9 px-3" onClick={() => setResponding(r)}>Phản hồi</button>}
            </div>
          </li>
        ))}
      </ul>
      {responding && <RespondModal request={responding} onClose={() => setResponding(null)} onDone={() => { setResponding(null); onChanged() }} />}
    </>
  )
}

function RespondModal({ request, onClose, onDone }: { request: SupportRequest; onClose: () => void; onDone: () => void }) {
  const [status, setStatus] = useState<'Resolved' | 'Rejected'>('Resolved')
  const [response, setResponse] = useState('')
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await adminLearningService.respond(request.id, status, response.trim())
      toast.success('Đã phản hồi học viên')
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title={`Phản hồi: ${supportTypeLabel[request.type]}`} onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <p className="rounded-(--radius-control) bg-surface-2 p-3 text-sm"><b>{request.userName}</b>: {request.message}</p>
        {request.type === 'QuizRetake' && request.enrollmentId && (
          <p className="text-sm text-ink-soft">Để cấp thêm lượt, mở <Link to={`/quan-tri/hoc-tap/${request.enrollmentId}`} className="font-semibold text-brand-700 hover:underline">trang tiến độ</Link> và bấm "Cấp thêm lượt" (yêu cầu sẽ tự đóng).</p>
        )}
        <fieldset className="flex gap-4">
          <legend className="sr-only">Kết quả</legend>
          <label className="flex items-center gap-2"><input type="radio" name="st" checked={status === 'Resolved'} onChange={() => setStatus('Resolved')} />Đã xử lý</label>
          <label className="flex items-center gap-2"><input type="radio" name="st" checked={status === 'Rejected'} onChange={() => setStatus('Rejected')} />Từ chối</label>
        </fieldset>
        <TextArea label="Phản hồi gửi học viên" rows={3} maxLength={1000} value={response} onChange={(e) => setResponse(e.target.value)} />
        <button type="submit" className="btn-primary" disabled={saving || response.trim().length < 5}>{saving && <Spinner className="size-4" />}Gửi phản hồi</button>
      </form>
    </Modal>
  )
}

export function AdminSupportRequestsPage() {
  useDocumentTitle('Hỗ trợ học tập')
  const [status, setStatus] = useState<SupportRequestStatus | ''>('Open')
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => adminLearningService.requests({ status, page }), [status, page])
  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Hỗ trợ học tập" description="Học viên báo sự cố khi học: xin thêm lượt kiểm tra, video lỗi, tiến độ không ghi nhận, chưa nhận chứng chỉ." />
      <Panel actions={
        <div className="flex flex-wrap gap-2" role="tablist" aria-label="Lọc trạng thái">
          {([['Open', 'Đang chờ'], ['Resolved', 'Đã xử lý'], ['Rejected', 'Từ chối'], ['', 'Tất cả']] as const).map(([v, l]) => (
            <button key={v} type="button" role="tab" aria-selected={status === v} className={status === v ? 'btn-primary min-h-9' : 'btn-soft min-h-9'} onClick={() => { setStatus(v); setPage(1) }}>{l}</button>
          ))}
        </div>
      }>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-40" /> : data?.items.length === 0 ? <EmptyState title="Không có yêu cầu" /> : (
          <SupportList items={data!.items} onChanged={reload} />
        )}
        <div className="mt-4"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
    </div>
  )
}
