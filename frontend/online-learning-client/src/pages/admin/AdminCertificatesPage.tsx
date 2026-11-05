import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, SelectField } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Pagination, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { getErrorMessage } from '../../services/apiClient'
import { certificateService, type AdminCertificate } from '../../services/certificateApi'
import { formatDate } from '../../utils/format'

/** Quản lý chứng chỉ: tra cứu, tải PDF, thu hồi có lý do, khôi phục, cấp bù cho lượt đã hoàn thành. */
export default function AdminCertificatesPage() {
  useDocumentTitle('Chứng chỉ')
  const [keyword, setKeyword] = useState('')
  const [revoked, setRevoked] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload } = useAsync(
    () => certificateService.adminList({ keyword: q || undefined, isRevoked: revoked === '' ? undefined : revoked === 'true', page }),
    [q, revoked, page],
  )
  const [revoking, setRevoking] = useState<AdminCertificate | null>(null)
  const [busy, setBusy] = useState(false)

  const backfill = async () => {
    setBusy(true)
    try {
      const r = await certificateService.backfill()
      toast.success(r.issued ? `Đã cấp bù ${r.issued} chứng chỉ` : 'Không có lượt hoàn thành nào thiếu chứng chỉ')
      reload()
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const restore = async (c: AdminCertificate) => {
    try {
      await certificateService.restore(c.id)
      toast.success('Đã khôi phục chứng chỉ')
      reload()
    } catch (e) {
      toast.error(getErrorMessage(e))
    }
  }

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Chứng chỉ" description="Chứng chỉ được cấp tự động khi học viên hoàn thành khóa học."
        actions={<button type="button" className="btn-soft" onClick={backfill} disabled={busy}>{busy && <Spinner className="size-4" />}Cấp bù chứng chỉ còn thiếu</button>} />
      <Panel>
        <div className="mb-6 grid gap-4 sm:grid-cols-[1fr_220px]">
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Mã chứng chỉ, tên, email, khóa học" />
          <SelectField label="Tình trạng" value={revoked} onChange={(v) => { setRevoked(v); setPage(1) }}>
            <option value="">Tất cả</option>
            <option value="false">Còn hiệu lực</option>
            <option value="true">Đã thu hồi</option>
          </SelectField>
        </div>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? (
          <EmptyState title="Chưa có chứng chỉ">Chứng chỉ xuất hiện khi học viên hoàn thành bài học bắt buộc và đạt bài kiểm tra.</EmptyState>
        ) : (
          <DataTable minWidth={900} head={<><th>Mã</th><th>Học viên</th><th>Khóa học</th><th>Ngày cấp</th><th className="text-right">Điểm</th><th>Tình trạng</th><th /></>}>
            {data?.items.map((c) => (
              <tr key={c.id}>
                <td><Link to={`/xac-minh/${c.certificateCode}`} className="font-bold tabular-nums text-brand-700 hover:underline">{c.certificateCode}</Link></td>
                <td><p className="font-medium">{c.studentName}</p><p className="text-ink-muted">{c.studentEmail}</p></td>
                <td>{c.courseTitle}</td>
                <td className="text-ink-soft">{formatDate(c.issuedAt)}</td>
                <td className="text-right tabular-nums">{c.finalScore !== null ? `${Math.round(c.finalScore)}%` : '—'}</td>
                <td>{c.isRevoked
                  ? <span className="font-semibold text-danger" title={c.revokedReason ?? ''}>Đã thu hồi</span>
                  : <span className="font-semibold text-success">Còn hiệu lực</span>}</td>
                <td>
                  <div className="flex flex-wrap gap-2">
                    <button type="button" className="btn-soft min-h-9 px-3" onClick={() => certificateService.downloadAdmin(c.id, c.certificateCode).catch((e) => toast.error(getErrorMessage(e)))}>PDF</button>
                    {c.isRevoked
                      ? <button type="button" className="btn-ghost min-h-9 px-3" onClick={() => restore(c)}>Khôi phục</button>
                      : <button type="button" className="btn-ghost min-h-9 px-3 text-danger" onClick={() => setRevoking(c)}>Thu hồi</button>}
                  </div>
                </td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
      {revoking && <RevokeModal cert={revoking} onClose={() => setRevoking(null)} onDone={() => { setRevoking(null); reload() }} />}
    </div>
  )
}

function RevokeModal({ cert, onClose, onDone }: { cert: AdminCertificate; onClose: () => void; onDone: () => void }) {
  const [reason, setReason] = useState('')
  const [saving, setSaving] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setSaving(true)
    try {
      await certificateService.revoke(cert.id, reason.trim())
      toast.success('Đã thu hồi chứng chỉ')
      onDone()
    } catch (err) {
      toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal title={`Thu hồi ${cert.certificateCode}`} onClose={onClose}>
      <form onSubmit={submit} className="flex flex-col gap-5">
        <p className="text-ink-soft">Trang xác minh công khai sẽ hiển thị "Đã thu hồi" kèm lý do. Học viên nhận được thông báo.</p>
        <TextArea label="Lý do thu hồi" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} rows={3} autoFocus />
        <button type="submit" className="btn bg-danger text-white hover:bg-danger/90" disabled={saving || !reason.trim()}>{saving && <Spinner className="size-4" />}Thu hồi chứng chỉ</button>
      </form>
    </Modal>
  )
}
