import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { LessonStatsModal } from '../../components/LessonStatsModal'
import { Link } from 'react-router-dom'
import { CheckCircle2, Download, ExternalLink, Megaphone, Play, RefreshCw, RotateCcw, Save, XCircle } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, ProgressBar, RevenueChart, SelectField, StatCard, Toggle } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Pagination, RatingStars, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { catalogService } from '../../services/api'
import { getErrorMessage } from '../../services/apiClient'
import {
  adminToolsService, applicationStatusLabel, applicationStatusStyle, audienceLabel,
  type AnnouncementAudience, type ApplicationStatus, type AuditQuery, type InstructorApplication, type ReportCourse, type Setting,
} from '../../services/adminToolsApi'
import { formatDate, formatDateTime, formatNumber, formatPrice, formatRelative } from '../../utils/format'

const money = (v: number) => formatPrice(v, '0đ')

function Pill({ className, children }: { className: string; children: React.ReactNode }) {
  return <span className={`inline-flex rounded-(--radius-control) px-2.5 py-0.5 text-sm font-semibold whitespace-nowrap ${className}`}>{children}</span>
}

// =================== Kiểm duyệt đánh giá ===================
export function AdminReviewsPage() {
  useDocumentTitle('Kiểm duyệt đánh giá')
  const [keyword, setKeyword] = useState('')
  const [rating, setRating] = useState('')
  const [visibility, setVisibility] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload, setData } = useAsync(() => adminToolsService.reviews({
    keyword: q || undefined, rating: rating ? Number(rating) : undefined, isHidden: visibility === '' ? undefined : visibility === 'hidden', page,
  }), [q, rating, visibility, page])

  const toggle = async (id: string, isHidden: boolean) => {
    try {
      await adminToolsService.setReviewHidden(id, isHidden)
      if (data) setData({ ...data, items: data.items.map((r) => (r.id === id ? { ...r, isHidden } : r)) })
      toast.success(isHidden ? 'Đã ẩn đánh giá' : 'Đã hiện lại đánh giá')
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Kiểm duyệt đánh giá" description="Ẩn đánh giá spam, xúc phạm hoặc sai sự thật. Đánh giá bị ẩn không còn tính vào điểm trung bình của khóa học." />
      <Panel>
        <div className="mb-6 grid gap-4 sm:grid-cols-[1fr_180px_180px]">
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Nội dung, tên học viên hoặc khóa học" />
          <SelectField label="Số sao" value={rating} onChange={(v) => { setRating(v); setPage(1) }}>
            <option value="">Tất cả</option>
            {[5, 4, 3, 2, 1].map((s) => <option key={s} value={s}>{s} sao</option>)}
          </SelectField>
          <SelectField label="Hiển thị" value={visibility} onChange={(v) => { setVisibility(v); setPage(1) }}>
            <option value="">Tất cả</option>
            <option value="visible">Đang hiện</option>
            <option value="hidden">Đã ẩn</option>
          </SelectField>
        </div>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? <EmptyState title="Không có đánh giá phù hợp" /> : (
          <DataTable minWidth={980} head={<><th>Khóa học</th><th>Học viên</th><th>Đánh giá</th><th>Ngày</th><th>Hiển thị</th></>}>
            {data?.items.map((r) => (
              <tr key={r.id} className={r.isHidden ? 'opacity-60' : ''}>
                <td className="max-w-56"><Link to={`/khoa-hoc/${r.courseSlug}`} className="font-medium hover:text-brand-700">{r.courseTitle}</Link></td>
                <td><p className="font-medium">{r.userName}</p><p className="text-ink-muted">{r.userEmail}</p></td>
                <td className="max-w-md">
                  <RatingStars value={r.rating} showValue={false} />
                  {r.comment ? <p className="mt-1 text-sm">{r.comment}</p> : <p className="mt-1 text-sm text-ink-muted">Không có nhận xét</p>}
                  {r.instructorReply && <p className="mt-2 rounded-lg bg-surface-2 p-2 text-xs text-ink-soft"><b>Giảng viên trả lời:</b> {r.instructorReply}</p>}
                </td>
                <td className="text-ink-soft">{formatDate(r.createdAt)}</td>
                <td><Toggle checked={!r.isHidden} label={r.isHidden ? 'Đã ẩn' : 'Đang hiện'} onChange={(v) => toggle(r.id, !v)} /></td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
    </div>
  )
}

// =================== Hồ sơ đăng ký giảng viên ===================
export function AdminInstructorApplicationsPage() {
  useDocumentTitle('Đăng ký giảng viên')
  const [status, setStatus] = useState<ApplicationStatus | ''>('Pending')
  const [page, setPage] = useState(1)
  const { data, loading, error, reload } = useAsync(() => adminToolsService.applications(status, page), [status, page])
  const [acting, setActing] = useState<{ app: InstructorApplication; approve: boolean } | null>(null)
  const tabs: { s: ApplicationStatus | ''; label: string }[] = [{ s: 'Pending', label: 'Chờ duyệt' }, { s: 'Approved', label: 'Đã duyệt' }, { s: 'Rejected', label: 'Từ chối' }, { s: '', label: 'Tất cả' }]

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Đăng ký giảng viên" description="Người dùng gửi hồ sơ tại trang “Trở thành giảng viên”. Duyệt hồ sơ sẽ cấp vai trò Giảng viên để họ tạo khóa học." />
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Lọc theo trạng thái">
        {tabs.map((t) => (
          <button key={t.label} type="button" role="tab" aria-selected={status === t.s} onClick={() => { setStatus(t.s); setPage(1) }}
            className={`rounded-full px-4 py-2 text-sm font-semibold transition-colors ${status === t.s ? 'bg-brand-600 text-white' : 'bg-white text-ink-soft hover:bg-brand-50'}`}>{t.label}</button>
        ))}
      </div>
      {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? <Panel><EmptyState title="Không có hồ sơ nào" /></Panel> : (
        <div className="flex flex-col gap-4">
          {data?.items.map((a) => (
            <Panel key={a.id}>
              <article className="flex flex-col gap-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h2 className="text-lg font-semibold">{a.userName}</h2>
                    <p className="text-sm text-ink-muted">{a.userEmail} · gửi {formatRelative(a.createdAt)}</p>
                  </div>
                  <Pill className={applicationStatusStyle[a.status]}>{applicationStatusLabel[a.status]}</Pill>
                </div>
                <dl className="grid gap-3 text-sm sm:grid-cols-2">
                  <div><dt className="text-ink-muted">Chức danh</dt><dd className="font-medium">{a.headline}</dd></div>
                  <div><dt className="text-ink-muted">Lĩnh vực chuyên môn</dt><dd className="font-medium">{a.expertise}</dd></div>
                  <div><dt className="text-ink-muted">Khóa học dự kiến</dt><dd className="font-medium">{a.proposedTopic}</dd></div>
                  <div><dt className="text-ink-muted">Kinh nghiệm</dt><dd className="font-medium">{a.yearsOfExperience != null ? `${a.yearsOfExperience} năm` : '—'}
                    {a.portfolioUrl && <> · <a href={a.portfolioUrl} target="_blank" rel="noreferrer noopener" className="inline-flex items-center gap-1 text-brand-700 hover:underline">Hồ sơ năng lực<ExternalLink className="size-3.5" aria-hidden="true" /></a></>}
                  </dd></div>
                  <div className="sm:col-span-2"><dt className="text-ink-muted">Giới thiệu</dt><dd className="whitespace-pre-line">{a.bio}</dd></div>
                  {a.adminNote && <div className="sm:col-span-2"><dt className="text-ink-muted">Ghi chú của Admin</dt><dd>{a.adminNote}</dd></div>}
                </dl>
                {a.status === 'Pending' && (
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="btn-primary" onClick={() => setActing({ app: a, approve: true })}><CheckCircle2 className="size-4" aria-hidden="true" />Duyệt</button>
                    <button type="button" className="btn-soft text-danger" onClick={() => setActing({ app: a, approve: false })}><XCircle className="size-4" aria-hidden="true" />Từ chối</button>
                  </div>
                )}
              </article>
            </Panel>
          ))}
        </div>
      )}
      <Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} />
      {acting && <ProcessApplicationModal {...acting} onClose={() => setActing(null)} onDone={() => { setActing(null); reload() }} />}
    </div>
  )
}

function ProcessApplicationModal({ app, approve, onClose, onDone }: { app: InstructorApplication; approve: boolean; onClose: () => void; onDone: () => void }) {
  const [note, setNote] = useState('')
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      if (approve) await adminToolsService.approve(app.id, note.trim() || null)
      else await adminToolsService.reject(app.id, note.trim())
      toast.success(approve ? `Đã cấp vai trò Giảng viên cho ${app.userName}` : 'Đã từ chối hồ sơ')
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title={approve ? `Duyệt hồ sơ: ${app.userName}` : `Từ chối hồ sơ: ${app.userName}`} onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <p className="text-sm text-ink-soft">{approve
          ? 'Người dùng được cấp vai trò Giảng viên, nhận thông báo và có thể tạo khóa học ngay. Chức danh, giới thiệu và chuyên môn trong hồ sơ được chép sang hồ sơ giảng viên nếu còn trống.'
          : 'Người dùng nhận thông báo kèm lý do và có thể bổ sung, gửi lại hồ sơ.'}</p>
        <TextArea label={approve ? 'Lời nhắn (không bắt buộc)' : 'Lý do từ chối'} rows={3} maxLength={1000} value={note} onChange={(e) => setNote(e.target.value)} />
        <button type="submit" className={approve ? 'btn-primary' : 'btn-primary bg-danger hover:bg-danger'} disabled={saving || (!approve && note.trim().length < 5)}>
          {saving && <Spinner className="size-4" />}{approve ? 'Duyệt và cấp vai trò' : 'Từ chối hồ sơ'}
        </button>
      </form>
    </Modal>
  )
}

// =================== Nhật ký thao tác Admin ===================
export function AdminAuditLogPage() {
  useDocumentTitle('Nhật ký quản trị')
  const [keyword, setKeyword] = useState('')
  const [result, setResult] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const k = useDebounce(keyword)
  const query: AuditQuery = { keyword: k || undefined, succeeded: result === '' ? undefined : result === 'ok', from: from || undefined, to: to || undefined, page }
  const { data, loading, error, reload } = useAsync(() => adminToolsService.audit(query), [k, result, from, to, page])
  const [exporting, setExporting] = useState(false)

  const exportCsv = async () => {
    setExporting(true)
    try { await adminToolsService.exportAudit(query) } catch (err) { toast.error(getErrorMessage(err)) } finally { setExporting(false) }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Nhật ký quản trị" description="Mọi thao tác thay đổi dữ liệu trong khu vực Quản trị đều được ghi lại: ai làm, lúc nào, dữ liệu gửi lên và kết quả."
        actions={<button type="button" className="btn-soft" onClick={exportCsv} disabled={exporting}>{exporting ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />}Xuất CSV</button>} />
      <Panel>
        <div className="mb-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-[1fr_160px_160px_160px]">
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Thao tác, người thực hiện, đường dẫn" />
          <SelectField label="Kết quả" value={result} onChange={(v) => { setResult(v); setPage(1) }}>
            <option value="">Tất cả</option><option value="ok">Thành công</option><option value="fail">Thất bại</option>
          </SelectField>
          <Field label="Từ ngày" type="date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1) }} />
          <Field label="Đến ngày" type="date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1) }} />
        </div>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? <EmptyState title="Chưa có thao tác nào" /> : (
          <DataTable minWidth={960} head={<><th>Thời gian</th><th>Người thực hiện</th><th>Thao tác</th><th>Kết quả</th><th>Chi tiết</th></>}>
            {data?.items.map((l) => (
              <tr key={l.id}>
                <td className="whitespace-nowrap text-ink-soft">{formatDateTime(l.createdAt)}</td>
                <td className="font-medium">{l.actorName}</td>
                <td><p className="font-medium">{l.summary}</p><p className="font-mono text-xs text-ink-muted">{l.httpMethod} {l.path}</p></td>
                <td>{l.succeeded ? <Pill className="bg-success-50 text-success">Thành công</Pill> : <Pill className="bg-danger-50 text-danger">Lỗi {l.statusCode}</Pill>}</td>
                <td className="max-w-sm">
                  {l.payload ? (
                    <details>
                      <summary className="cursor-pointer text-sm font-semibold text-brand-700">Dữ liệu gửi lên</summary>
                      <pre className="mt-2 max-h-48 overflow-auto rounded-lg bg-surface-2 p-2 text-xs whitespace-pre-wrap break-all">{l.payload}</pre>
                    </details>
                  ) : <span className="text-ink-muted">—</span>}
                  {l.ipAddress && <p className="mt-1 text-xs text-ink-muted">IP {l.ipAddress}</p>}
                </td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
    </div>
  )
}

// =================== Cài đặt hệ thống ===================
export function AdminSettingsPage() {
  useDocumentTitle('Cài đặt hệ thống')
  const { data, loading, error, reload } = useAsync(() => adminToolsService.settings(), [])
  const [draft, setDraft] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    if (data) setDraft(Object.fromEntries(data.map((s) => [s.key, s.value ?? ''])))
  }, [data])

  const groups = useMemo(() => {
    const m = new Map<string, Setting[]>()
    data?.forEach((s) => m.set(s.group, [...(m.get(s.group) ?? []), s]))
    return [...m.entries()]
  }, [data])

  const changed = data?.filter((s) => (draft[s.key] ?? '') !== (s.value ?? '')) ?? []

  const save = async (values: Record<string, string | null>, message: string) => {
    setSaving(true)
    try {
      await adminToolsService.saveSettings(values)
      toast.success(message)
      reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Cài đặt hệ thống" description="Thay đổi có hiệu lực ngay, không cần khởi động lại máy chủ. Giá trị chưa chỉnh lấy từ tệp cấu hình appsettings.json."
        actions={<button type="button" className="btn-primary" disabled={saving || changed.length === 0}
          onClick={() => save(Object.fromEntries(changed.map((s) => [s.key, draft[s.key]])), `Đã lưu ${changed.length} cài đặt`)}>
          {saving ? <Spinner className="size-4" /> : <Save className="size-4" aria-hidden="true" />}Lưu thay đổi{changed.length ? ` (${changed.length})` : ''}
        </button>} />
      {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-96" /> : groups.map(([group, items]) => (
        <Panel key={group} title={group}>
          <div className="grid gap-5 md:grid-cols-2">
            {items.map((s) => (
              <div key={s.key} className="flex flex-col gap-1.5">
                {s.type === 'Boolean' ? (
                  <div className="flex min-h-11 items-center justify-between gap-3 rounded-(--radius-control) border border-line px-3">
                    <span className="text-sm font-semibold">{s.label}</span>
                    <Toggle checked={draft[s.key] === 'true' || draft[s.key] === 'True'} label={draft[s.key]?.toLowerCase() === 'true' ? 'Bật' : 'Tắt'}
                      onChange={(v) => setDraft({ ...draft, [s.key]: v ? 'true' : 'false' })} />
                  </div>
                ) : (
                  <Field label={s.label} type={s.type === 'Text' ? 'text' : 'number'} inputMode={s.type === 'Text' ? undefined : 'decimal'}
                    min={s.min ?? undefined} max={s.max ?? undefined} value={draft[s.key] ?? ''} onChange={(e) => setDraft({ ...draft, [s.key]: e.target.value })}
                    hint={[s.hint, s.min != null && s.max != null ? `Từ ${formatNumber(s.min)} đến ${formatNumber(s.max)}` : null].filter(Boolean).join(' · ') || undefined} />
                )}
                {s.isOverridden && (
                  <p className="flex items-center gap-2 text-xs text-ink-muted">
                    Đã chỉnh {s.updatedAt ? formatRelative(s.updatedAt) : ''}
                    <button type="button" className="inline-flex items-center gap-1 font-semibold text-brand-700 hover:underline" disabled={saving}
                      onClick={() => save({ [s.key]: null }, `Đã khôi phục mặc định: ${s.label}`)}><RotateCcw className="size-3" aria-hidden="true" />Về mặc định</button>
                  </p>
                )}
              </div>
            ))}
          </div>
        </Panel>
      ))}
    </div>
  )
}

// =================== Thông báo hàng loạt ===================
export function AdminAnnouncementsPage() {
  useDocumentTitle('Gửi thông báo')
  const courses = useAsync(() => catalogService.search({ pageSize: 100, sort: 'popular' }), [])
  const [f, setF] = useState({ audience: 'Everyone' as AnnouncementAudience, courseId: '', title: '', message: '', link: '', sendEmail: false })
  const preview = useAsync(() => (f.audience === 'CourseStudents' && !f.courseId ? Promise.resolve(null) : adminToolsService.previewAudience(f.audience, f.courseId || undefined)), [f.audience, f.courseId])
  const [page, setPage] = useState(1)
  const history = useAsync(() => adminToolsService.announcements(page), [page])
  const [confirming, setConfirming] = useState(false)
  const [sending, setSending] = useState(false)
  const linkInvalid = f.link.trim() !== '' && (!f.link.trim().startsWith('/') || f.link.trim().startsWith('//'))
  const valid = f.title.trim().length >= 3 && f.message.trim().length >= 10 && !linkInvalid && (f.audience !== 'CourseStudents' || !!f.courseId) && (preview.data?.recipients ?? 0) > 0

  const send = async () => {
    setSending(true)
    try {
      const a = await adminToolsService.sendAnnouncement({ ...f, courseId: f.courseId || null, link: f.link.trim() || null, title: f.title.trim(), message: f.message.trim() })
      toast.success(`Đã gửi tới ${formatNumber(a.recipientCount)} người${a.emailCount ? `, ${formatNumber(a.emailCount)} email` : ''}`)
      setF({ ...f, title: '', message: '', link: '' })
      setConfirming(false)
      setPage(1)
      history.reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSending(false)
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Gửi thông báo" description="Gửi thông báo trong hệ thống (biểu tượng chuông) và tùy chọn gửi kèm email tới nhóm người dùng." />
      <Panel title="Soạn thông báo">
        <form className="grid gap-4 md:grid-cols-2" onSubmit={(e) => { e.preventDefault(); if (valid) setConfirming(true) }} noValidate>
          <SelectField label="Người nhận" value={f.audience} onChange={(v) => setF({ ...f, audience: v as AnnouncementAudience })}>
            {Object.entries(audienceLabel).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
          </SelectField>
          {f.audience === 'CourseStudents' ? (
            <SelectField label="Khóa học" value={f.courseId} onChange={(v) => setF({ ...f, courseId: v })}>
              <option value="">— Chọn khóa học —</option>
              {courses.data?.items.map((c) => <option key={c.id} value={c.id}>{c.title}</option>)}
            </SelectField>
          ) : <div className="hidden md:block" />}
          <Field label="Tiêu đề" value={f.title} maxLength={150} onChange={(e) => setF({ ...f, title: e.target.value })} className="md:col-span-2" />
          <div className="md:col-span-2"><TextArea label="Nội dung" rows={4} maxLength={1000} value={f.message} onChange={(e) => setF({ ...f, message: e.target.value })} /></div>
          <Field label="Đường dẫn khi bấm vào (không bắt buộc)" value={f.link} placeholder="/khoa-hoc" onChange={(e) => setF({ ...f, link: e.target.value })}
            error={linkInvalid ? 'Đường dẫn trong website, bắt đầu bằng “/”' : undefined} />
          <div className="flex min-h-11 items-center justify-between gap-3 self-end rounded-(--radius-control) border border-line px-3">
            <span className="text-sm font-semibold">Gửi kèm email</span>
            <Toggle checked={f.sendEmail} label={f.sendEmail ? 'Có' : 'Không'} onChange={(v) => setF({ ...f, sendEmail: v })} />
          </div>
          <div className="flex flex-wrap items-center justify-between gap-3 md:col-span-2">
            <p className="text-sm text-ink-soft" aria-live="polite">
              {preview.loading ? 'Đang đếm người nhận…' : preview.data ? <>Sẽ gửi tới <b className="text-ink">{formatNumber(preview.data.recipients)}</b> người
                {f.sendEmail && <>, <b className="text-ink">{formatNumber(preview.data.emailRecipients)}</b> email (bỏ qua người đã tắt email nhắc nhở)</>}</> : 'Chọn khóa học để xem số người nhận'}
            </p>
            <button type="submit" className="btn-primary" disabled={!valid}><Megaphone className="size-4" aria-hidden="true" />Gửi thông báo</button>
          </div>
        </form>
      </Panel>

      <Panel title="Đã gửi">
        {history.error ? <ErrorState message={history.error} onRetry={history.reload} /> : history.loading ? <Skeleton className="h-32" /> : history.data?.items.length === 0 ? <EmptyState title="Chưa gửi thông báo nào" /> : (
          <DataTable minWidth={860} head={<><th>Thông báo</th><th>Người nhận</th><th className="text-right">Số người</th><th className="text-right">Email</th><th>Người gửi</th><th>Thời gian</th></>}>
            {history.data?.items.map((a) => (
              <tr key={a.id}>
                <td className="max-w-sm"><p className="font-medium">{a.title}</p><p className="line-clamp-2 text-sm text-ink-muted">{a.message}</p></td>
                <td>{audienceLabel[a.audience]}{a.courseTitle && <p className="text-xs text-ink-muted">{a.courseTitle}</p>}</td>
                <td className="text-right tabular-nums">{formatNumber(a.recipientCount)}</td>
                <td className="text-right tabular-nums">{a.sendEmail ? formatNumber(a.emailCount) : '—'}</td>
                <td>{a.createdByName}</td>
                <td className="whitespace-nowrap text-ink-soft">{formatDateTime(a.createdAt)}</td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={history.data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>

      {confirming && (
        <Modal title="Xác nhận gửi thông báo" onClose={() => setConfirming(false)}>
          <div className="flex flex-col gap-4">
            <p>Gửi “<b>{f.title.trim()}</b>” tới <b>{formatNumber(preview.data?.recipients ?? 0)}</b> người ({audienceLabel[f.audience].toLowerCase()})
              {f.sendEmail && <> kèm <b>{formatNumber(preview.data?.emailRecipients ?? 0)}</b> email</>}? Thông báo đã gửi không thu hồi được.</p>
            <div className="flex justify-end gap-2">
              <button type="button" className="btn-soft" onClick={() => setConfirming(false)}>Hủy</button>
              <button type="button" className="btn-primary" onClick={send} disabled={sending}>{sending && <Spinner className="size-4" />}Gửi ngay</button>
            </div>
          </div>
        </Modal>
      )}
    </div>
  )
}

// =================== Báo cáo phân tích ===================
const isoDay = (d: Date) => d.toISOString().slice(0, 10)

export function AdminReportsPage() {
  useDocumentTitle('Báo cáo')
  const [range, setRange] = useState({ from: '', to: '' })
  const { data, loading, error, reload } = useAsync(() => adminToolsService.report(range.from || undefined, range.to || undefined), [range.from, range.to])
  const [lessonsOf, setLessonsOf] = useState<ReportCourse | null>(null)
  const [exporting, setExporting] = useState(false)
  const preset = (months: number) => {
    const to = new Date()
    const from = new Date(to.getFullYear(), to.getMonth() - months + 1, 1)
    setRange({ from: isoDay(from), to: isoDay(to) })
  }
  const t = data?.totals

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Báo cáo" description={data ? `Từ ${formatDate(data.from)} đến ${formatDate(data.to)}` : 'Doanh thu, khóa học và giảng viên theo khoảng thời gian.'}
        actions={<button type="button" className="btn-soft" disabled={exporting} onClick={async () => {
          setExporting(true)
          try { await adminToolsService.exportReport(range.from || undefined, range.to || undefined) } catch (err) { toast.error(getErrorMessage(err)) } finally { setExporting(false) }
        }}>{exporting ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />}Xuất CSV khóa học</button>} />

      <Panel>
        <div className="flex flex-wrap items-end gap-3">
          <Field label="Từ ngày" type="date" value={range.from} onChange={(e) => setRange({ ...range, from: e.target.value })} />
          <Field label="Đến ngày" type="date" value={range.to} onChange={(e) => setRange({ ...range, to: e.target.value })} />
          {[{ m: 1, l: 'Tháng này' }, { m: 3, l: '3 tháng' }, { m: 12, l: '12 tháng' }].map((p) => (
            <button key={p.m} type="button" className="btn-soft min-h-11" onClick={() => preset(p.m)}>{p.l}</button>
          ))}
        </div>
      </Panel>

      {error ? <ErrorState message={error} onRetry={reload} /> : loading || !data || !t ? <Skeleton className="h-96" /> : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatCard label="Doanh thu thực" value={money(t.revenue)} sub={`${formatNumber(t.paidOrders)} đơn đã thanh toán`} tone="brand" />
            <StatCard label="Đã hoàn tiền" value={money(t.refundedAmount)} sub={`${formatNumber(t.refundedOrders)} đơn`} />
            <StatCard label="Lượt ghi danh mới" value={formatNumber(t.enrollments)} sub={`${formatNumber(t.completions)} lượt hoàn thành`} />
            <StatCard label="Người dùng mới" value={formatNumber(t.newUsers)} tone="accent" />
          </div>
          <Panel title="Doanh thu theo tháng"><RevenueChart data={data.monthly} /></Panel>
          <Panel title="Khóa học bán chạy">
            {data.topCourses.length === 0 ? <EmptyState title="Chưa có dữ liệu trong khoảng này" /> : (
              <DataTable minWidth={980} head={<><th>Khóa học</th><th className="text-right">Đơn bán</th><th className="text-right">Doanh thu</th><th className="text-right">Ghi danh</th><th className="w-40">Hoàn thành</th><th className="text-right">Đánh giá</th><th className="text-right">Hoàn tiền</th><th /></>}>
                {data.topCourses.map((c) => (
                  <tr key={c.courseId}>
                    <td className="max-w-xs"><p className="font-medium">{c.title}</p><p className="text-xs text-ink-muted">{c.instructorName}</p></td>
                    <td className="text-right tabular-nums">{formatNumber(c.sales)}</td>
                    <td className="text-right font-semibold tabular-nums">{money(c.revenue)}</td>
                    <td className="text-right tabular-nums">{formatNumber(c.enrollments)}</td>
                    <td><ProgressBar value={c.completionRate} label={`Tỷ lệ hoàn thành ${c.title}`} /></td>
                    <td className="text-right tabular-nums">{c.averageRating ? c.averageRating.toFixed(1) : '—'}</td>
                    <td className={`text-right tabular-nums ${c.refunds ? 'text-danger' : ''}`}>{c.refunds}</td>
                    <td><button type="button" className="btn-soft min-h-9 px-3" onClick={() => setLessonsOf(c)}>Bài học</button></td>
                  </tr>
                ))}
              </DataTable>
            )}
          </Panel>
          <Panel title="Giảng viên nổi bật">
            {data.topInstructors.length === 0 ? <EmptyState title="Chưa có dữ liệu trong khoảng này" /> : (
              <DataTable minWidth={640} head={<><th>Giảng viên</th><th className="text-right">Khóa học</th><th className="text-right">Đơn bán</th><th className="text-right">Học viên</th><th className="text-right">Doanh thu</th></>}>
                {data.topInstructors.map((i) => (
                  <tr key={i.instructorId}>
                    <td className="font-medium"><Link to={`/giang-vien/${i.instructorId}`} className="hover:text-brand-700">{i.name}</Link></td>
                    <td className="text-right tabular-nums">{i.courses}</td>
                    <td className="text-right tabular-nums">{formatNumber(i.sales)}</td>
                    <td className="text-right tabular-nums">{formatNumber(i.students)}</td>
                    <td className="text-right font-semibold tabular-nums">{money(i.revenue)}</td>
                  </tr>
                ))}
              </DataTable>
            )}
          </Panel>
        </>
      )}
      {lessonsOf && <LessonStatsModal courseId={lessonsOf.courseId} title={lessonsOf.title} load={adminToolsService.lessonStats} onClose={() => setLessonsOf(null)} />}
    </div>
  )
}

// =================== Trạng thái hệ thống ===================
const statusStyle: Record<string, { cls: string; label: string }> = {
  ok: { cls: 'bg-success-50 text-success', label: 'Hoạt động' },
  warning: { cls: 'bg-warning-50 text-warning', label: 'Cần chú ý' },
  error: { cls: 'bg-danger-50 text-danger', label: 'Lỗi' },
  off: { cls: 'bg-surface-2 text-ink-muted', label: 'Đã tắt' },
}
const countLabels: Record<string, string> = {
  users: 'Người dùng', courses: 'Khóa học', enrollments: 'Lượt ghi danh', orders: 'Đơn hàng', notifications: 'Thông báo', auditLogs: 'Nhật ký Admin',
}
const formatUptime = (s: string) => {
  const [dayPart, time] = s.includes('.') && s.indexOf('.') < s.indexOf(':') ? s.split('.') : ['0', s]
  const [h, m] = time.split(':')
  return `${Number(dayPart) ? `${Number(dayPart)} ngày ` : ''}${Number(h)} giờ ${Number(m)} phút`
}

export function AdminSystemPage() {
  useDocumentTitle('Trạng thái hệ thống')
  const { data, loading, error, reload } = useAsync(() => adminToolsService.system(), [])
  const [running, setRunning] = useState<string | null>(null)

  const run = async (key: string) => {
    setRunning(key)
    try {
      const r = await adminToolsService.runJob(key)
      toast.success(r.message)
      reload()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setRunning(null)
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Trạng thái hệ thống" description={data ? `Môi trường ${data.environment} · đã chạy ${formatUptime(data.uptime)}` : undefined}
        actions={<button type="button" className="btn-soft" onClick={reload} disabled={loading}><RefreshCw className="size-4" aria-hidden="true" />Làm mới</button>} />
      {error ? <ErrorState message={error} onRetry={reload} /> : loading || !data ? <Skeleton className="h-96" /> : (
        <>
          <Panel title="Thành phần">
            <ul className="grid gap-3 md:grid-cols-2">
              {data.components.map((c) => (
                <li key={c.name} className="flex items-start justify-between gap-3 rounded-(--radius-control) border border-line p-4">
                  <div className="min-w-0"><p className="font-semibold">{c.name}</p><p className="text-sm break-words text-ink-muted">{c.detail}</p></div>
                  <Pill className={statusStyle[c.status]?.cls ?? statusStyle.off.cls}>{statusStyle[c.status]?.label ?? c.status}</Pill>
                </li>
              ))}
            </ul>
          </Panel>

          <Panel title="Công việc chạy nền">
            <DataTable minWidth={760} head={<><th>Công việc</th><th>Chu kỳ</th><th>Lần chạy gần nhất</th><th>Kết quả</th><th /></>}>
              {data.jobs.map((j) => (
                <tr key={j.key}>
                  <td><p className="font-medium">{j.name}</p>{!j.enabled && <p className="text-xs text-warning">Đang tắt trong Cài đặt</p>}</td>
                  <td className="text-ink-soft">{j.interval}</td>
                  <td className="text-ink-soft">{j.lastRunAt ? formatRelative(j.lastRunAt) : 'Chưa chạy từ khi khởi động'}</td>
                  <td className="max-w-xs text-sm">{j.lastSucceeded == null ? '—' : <><span className={j.lastSucceeded ? 'font-semibold text-success' : 'font-semibold text-danger'}>{j.lastSucceeded ? 'Thành công' : 'Lỗi'}</span>{j.lastMessage && <span className="block text-xs text-ink-muted">{j.lastMessage}</span>}</>}</td>
                  <td><button type="button" className="btn-soft min-h-9 px-3" onClick={() => run(j.key)} disabled={running !== null}>
                    {running === j.key ? <Spinner className="size-4" /> : <Play className="size-4" aria-hidden="true" />}Chạy ngay</button></td>
                </tr>
              ))}
            </DataTable>
          </Panel>

          <div className="grid gap-4 sm:grid-cols-3 lg:grid-cols-6">
            {Object.entries(data.counts).map(([k, v]) => <StatCard key={k} label={countLabels[k] ?? k} value={formatNumber(v)} />)}
          </div>

          <Panel title="Email gần đây" actions={<span className={`text-sm font-semibold ${data.emailFailures24h ? 'text-danger' : 'text-ink-muted'}`}>{data.emailFailures24h} lỗi trong 24 giờ</span>}>
            {data.recentEmails.length === 0 ? <EmptyState title="Chưa gửi email nào từ khi khởi động" /> : (
              <DataTable minWidth={720} head={<><th>Thời gian</th><th>Người nhận</th><th>Tiêu đề</th><th>Kết quả</th></>}>
                {data.recentEmails.map((e, i) => (
                  <tr key={`${e.at}-${i}`}>
                    <td className="whitespace-nowrap text-ink-soft">{formatDateTime(e.at)}</td>
                    <td className="font-mono text-sm">{e.to}</td>
                    <td className="max-w-sm">{e.subject}</td>
                    <td>{!e.succeeded ? <><Pill className="bg-danger-50 text-danger">Lỗi</Pill>{e.error && <p className="mt-1 text-xs text-danger">{e.error}</p>}</>
                      : e.delivered ? <Pill className="bg-success-50 text-success">Đã gửi</Pill> : <Pill className="bg-surface-2 text-ink-muted">Chỉ ghi log</Pill>}</td>
                  </tr>
                ))}
              </DataTable>
            )}
          </Panel>
        </>
      )}
    </div>
  )
}
