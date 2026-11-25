import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Download } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, StatCard } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, OrderStatusBadge, Pagination, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { getErrorMessage } from '../../services/apiClient'
import {
  adminRevenueService, payoutStatusLabel, payoutStatusStyle, refundStatusLabel, refundStatusStyle,
  type AdminPayout, type AdminRefundRequest, type PayoutStatus, type RefundRequestStatus,
} from '../../services/revenueApi'
import { formatDateTime, formatNumber, formatPrice } from '../../utils/format'

const money = (v: number) => formatPrice(v, '0đ')

function Pill({ className, children }: { className: string; children: string }) {
  return <span className={`inline-flex rounded-(--radius-control) px-2.5 py-0.5 text-sm font-semibold whitespace-nowrap ${className}`}>{children}</span>
}

// =================== Doanh thu và chi trả giảng viên ===================
export function AdminPayoutsPage() {
  useDocumentTitle('Chi trả giảng viên')
  const overview = useAsync(() => adminRevenueService.overview(), [])
  const [status, setStatus] = useState<PayoutStatus | ''>('Requested')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const list = useAsync(() => adminRevenueService.payouts({ status, keyword: q || undefined, page }), [status, q, page])
  const [action, setAction] = useState<{ payout: AdminPayout; kind: 'paid' | 'reject' } | null>(null)
  const [exporting, setExporting] = useState(false)

  const exportCsv = async () => {
    setExporting(true)
    try { await adminRevenueService.exportPayouts({ status, keyword: q || undefined }) } catch (err) { toast.error(getErrorMessage(err)) } finally { setExporting(false) }
  }

  const o = overview.data
  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Doanh thu và chi trả giảng viên"
        description={o ? `Giảng viên nhận ${formatNumber(o.sharePercent)}% số tiền học viên thực trả; phần còn lại là doanh thu nền tảng. Tiền hoàn cho học viên được trừ lại.` : undefined} />

      {overview.error ? <ErrorState message={overview.error} onRetry={overview.reload} /> : !o ? (
        <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">{Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-28" />)}</div>
      ) : (
        <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
          <StatCard label="Tổng thực thu" value={money(o.grossSales)} sub="Sau giảm giá, đã trừ hoàn tiền" tone="brand" />
          <StatCard label="Doanh thu nền tảng" value={money(o.platformShare)} sub={`${formatNumber(100 - o.sharePercent)}% thực thu`} />
          <StatCard label="Phải trả giảng viên" value={money(o.owedToInstructors)} sub={`Đã chi ${money(o.paidOut)}`} />
          <StatCard label="Yêu cầu chờ chi" value={String(o.openPayoutCount)} sub={o.inProcess > 0 ? `Tổng ${money(o.inProcess)}` : 'Không có yêu cầu mới'} tone="accent" />
        </div>
      )}

      <Panel title="Yêu cầu rút tiền"
        actions={<button type="button" className="btn-outline" onClick={exportCsv} disabled={exporting}>{exporting ? <Spinner className="size-4" /> : <Download className="size-4" aria-hidden="true" />} Xuất CSV</button>}>
        <div className="mb-6 flex flex-wrap items-end gap-3">
          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Lọc trạng thái">
            {([['Requested', 'Chờ xử lý'], ['Paid', 'Đã chi'], ['Rejected', 'Từ chối'], ['', 'Tất cả']] as const).map(([v, l]) => (
              <button key={v} type="button" role="tab" aria-selected={status === v} className={status === v ? 'btn-primary' : 'btn-soft'}
                onClick={() => { setStatus(v); setPage(1) }}>{l}</button>
            ))}
          </div>
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Tên, email, số tài khoản" className="ml-auto min-w-64" />
        </div>
        {list.error ? <ErrorState message={list.error} onRetry={list.reload} /> : list.loading ? <Skeleton className="h-40" /> :
          list.data?.items.length === 0 ? <EmptyState title="Không có yêu cầu" /> : (
            <DataTable minWidth={980} head={<><th>Giảng viên</th><th className="text-right">Số tiền</th><th>Chuyển vào</th><th>Trạng thái</th><th>Ngày gửi</th><th /></>}>
              {list.data?.items.map((p) => (
                <tr key={p.id}>
                  <td><p className="font-medium">{p.instructorName}</p><p className="text-ink-muted">{p.instructorEmail}</p></td>
                  <td className="text-right tabular-nums">
                    <p className="font-semibold">{money(p.amount)}</p>
                    {p.status === 'Requested' && (
                      <p className={`text-xs ${p.instructorWithdrawable < p.amount ? 'font-semibold text-danger' : 'text-ink-muted'}`}>Số dư hiện có {money(p.instructorWithdrawable)}</p>
                    )}
                  </td>
                  <td><p className="font-medium">{p.bankName} · <span className="tabular-nums">{p.bankAccountNumber}</span></p><p className="text-ink-muted">{p.bankAccountName}</p>
                    {p.note && <p className="text-xs text-ink-muted">“{p.note}”</p>}</td>
                  <td>
                    <Pill className={payoutStatusStyle[p.status]}>{payoutStatusLabel[p.status]}</Pill>
                    {p.transferReference && <p className="mt-1 text-xs text-ink-muted">Mã CK {p.transferReference}</p>}
                    {p.status === 'Rejected' && p.adminNote && <p className="mt-1 max-w-56 text-xs text-ink-muted">{p.adminNote}</p>}
                  </td>
                  <td className="text-ink-soft">{formatDateTime(p.createdAt)}</td>
                  <td>{p.status === 'Requested' && (
                    <div className="flex gap-1">
                      <button type="button" className="btn-soft min-h-9 px-3" onClick={() => setAction({ payout: p, kind: 'paid' })}>Đã chuyển</button>
                      <button type="button" className="btn-ghost min-h-9 px-3 text-danger" onClick={() => setAction({ payout: p, kind: 'reject' })}>Từ chối</button>
                    </div>
                  )}</td>
                </tr>
              ))}
            </DataTable>
          )}
        <div className="mt-6"><Pagination page={page} totalPages={list.data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>

      <Panel title="Số dư theo giảng viên">
        {!o ? <Skeleton className="h-32" /> : o.instructors.length === 0 ? <p className="text-ink-soft">Chưa có thu nhập.</p> : (
          <DataTable minWidth={900} head={<><th>Giảng viên</th><th className="text-right">Lượt bán</th><th className="text-right">Thực thu</th><th className="text-right">Thu nhập</th><th className="text-right">Chờ đối soát</th><th className="text-right">Đã chi</th><th className="text-right">Có thể rút</th></>}>
            {o.instructors.map((i) => (
              <tr key={i.instructorId}>
                <td><p className="font-medium">{i.instructorName}</p><p className="text-ink-muted">{i.instructorEmail}</p></td>
                <td className="text-right tabular-nums">{formatNumber(i.salesCount)}</td>
                <td className="text-right tabular-nums">{money(i.grossSales)}</td>
                <td className="text-right font-semibold tabular-nums">{money(i.totalEarned)}</td>
                <td className="text-right tabular-nums text-ink-soft">{money(i.pendingAmount)}</td>
                <td className="text-right tabular-nums">{money(i.paidOutAmount)}{i.inProcessAmount > 0 && <span className="block text-xs text-warning">+{money(i.inProcessAmount)} chờ chi</span>}</td>
                <td className="text-right font-semibold tabular-nums text-brand-700">{money(i.withdrawableAmount)}</td>
              </tr>
            ))}
          </DataTable>
        )}
      </Panel>

      {action && <PayoutActionModal {...action} onClose={() => setAction(null)} onDone={() => { setAction(null); list.reload(); overview.reload() }} />}
    </div>
  )
}

function PayoutActionModal({ payout, kind, onClose, onDone }: { payout: AdminPayout; kind: 'paid' | 'reject'; onClose: () => void; onDone: () => void }) {
  const [ref, setRef] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const paid = kind === 'paid'

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (paid && ref.trim().length < 3) { setError('Nhập mã giao dịch chuyển khoản (ít nhất 3 ký tự).'); return }
    if (!paid && note.trim().length < 5) { setError('Bắt buộc ghi lý do từ chối (ít nhất 5 ký tự).'); return }
    setSaving(true)
    try {
      if (paid) await adminRevenueService.markPaid(payout.id, ref.trim(), note.trim())
      else await adminRevenueService.rejectPayout(payout.id, note.trim())
      toast.success(paid ? `Đã ghi nhận chi ${money(payout.amount)} cho ${payout.instructorName}` : 'Đã từ chối yêu cầu')
      onDone()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={paid ? 'Xác nhận đã chuyển khoản' : 'Từ chối yêu cầu rút tiền'} onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 rounded-(--radius-control) bg-surface-2 p-4 text-sm">
          <dt className="text-ink-soft">Giảng viên</dt><dd className="font-medium">{payout.instructorName}</dd>
          <dt className="text-ink-soft">Số tiền</dt><dd className="font-semibold tabular-nums">{money(payout.amount)}</dd>
          <dt className="text-ink-soft">Ngân hàng</dt><dd>{payout.bankName}</dd>
          <dt className="text-ink-soft">Số tài khoản</dt><dd className="tabular-nums">{payout.bankAccountNumber}</dd>
          <dt className="text-ink-soft">Chủ tài khoản</dt><dd>{payout.bankAccountName}</dd>
        </dl>
        {paid ? (
          <>
            <p className="text-sm text-ink-soft">Chuyển khoản cho giảng viên qua ngân hàng trước, rồi ghi mã giao dịch để đối soát.</p>
            <Field label="Mã giao dịch chuyển khoản" value={ref} onChange={(e) => setRef(e.target.value)} maxLength={100} error={error ?? undefined} />
            <TextArea label="Ghi chú (không bắt buộc)" rows={2} value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} />
          </>
        ) : (
          <TextArea label="Lý do từ chối" rows={3} value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} error={error ?? undefined} />
        )}
        <button type="submit" className={paid ? 'btn-primary' : 'btn-primary bg-danger hover:bg-danger'} disabled={saving}>
          {saving && <Spinner className="size-4" />}{paid ? 'Xác nhận đã chuyển' : 'Từ chối'}
        </button>
      </form>
    </Modal>
  )
}

// =================== Yêu cầu hoàn tiền ===================
export function AdminRefundRequestsPage() {
  useDocumentTitle('Yêu cầu hoàn tiền')
  const [status, setStatus] = useState<RefundRequestStatus | ''>('Pending')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload } = useAsync(() => adminRevenueService.refundRequests({ status, keyword: q || undefined, page }), [status, q, page])
  const [action, setAction] = useState<{ request: AdminRefundRequest; kind: 'approve' | 'reject' } | null>(null)

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Yêu cầu hoàn tiền" description="Học viên gửi yêu cầu theo Chính sách hoàn tiền. Duyệt sau khi đã hoàn tiền cho khách qua cổng thanh toán." />
      <Panel>
        <div className="mb-6 flex flex-wrap items-end gap-3">
          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Lọc trạng thái">
            {([['Pending', 'Chờ xử lý'], ['Approved', 'Đã hoàn tiền'], ['Rejected', 'Bị từ chối'], ['', 'Tất cả']] as const).map(([v, l]) => (
              <button key={v} type="button" role="tab" aria-selected={status === v} className={status === v ? 'btn-primary' : 'btn-soft'}
                onClick={() => { setStatus(v); setPage(1) }}>{l}</button>
            ))}
          </div>
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Mã đơn, email, tên" className="ml-auto min-w-64" />
        </div>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-40" /> : data?.items.length === 0 ? <EmptyState title="Không có yêu cầu" /> : (
          <DataTable minWidth={1000} head={<><th>Đơn hàng</th><th>Học viên</th><th>Lý do</th><th>Điều kiện</th><th>Trạng thái</th><th>Ngày gửi</th><th /></>}>
            {data?.items.map((r) => (
              <tr key={r.id}>
                <td>
                  <Link to={`/don-hang/${r.orderId}`} className="font-semibold text-brand-700 hover:underline">{r.orderCode}</Link>
                  <p className="font-semibold tabular-nums">{money(r.orderTotal)}</p>
                  <OrderStatusBadge status={r.orderStatus} />
                </td>
                <td><p className="font-medium">{r.userFullName}</p><p className="text-ink-muted">{r.userEmail}</p></td>
                <td className="max-w-72 text-ink-soft">{r.reason}{r.adminNote && <p className="mt-1 text-xs text-ink-muted">Admin: {r.adminNote}</p>}</td>
                <td className="text-sm">
                  <p>Đã học <b className="tabular-nums">{formatNumber(r.maxProgressPercent)}%</b></p>
                  {r.paidAt && <p className="text-ink-muted">Thanh toán {formatDateTime(r.paidAt)}</p>}
                  {r.hasCertificate && <p className="font-semibold text-danger">Đã cấp chứng chỉ</p>}
                </td>
                <td><Pill className={refundStatusStyle[r.status]}>{refundStatusLabel[r.status]}</Pill></td>
                <td className="text-ink-soft">{formatDateTime(r.createdAt)}</td>
                <td>{r.status === 'Pending' && (
                  <div className="flex gap-1">
                    <button type="button" className="btn-soft min-h-9 px-3" onClick={() => setAction({ request: r, kind: 'approve' })}>Hoàn tiền</button>
                    <button type="button" className="btn-ghost min-h-9 px-3 text-danger" onClick={() => setAction({ request: r, kind: 'reject' })}>Từ chối</button>
                  </div>
                )}</td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
      {action && <RefundActionModal {...action} onClose={() => setAction(null)} onDone={() => { setAction(null); reload() }} />}
    </div>
  )
}

function RefundActionModal({ request, kind, onClose, onDone }: { request: AdminRefundRequest; kind: 'approve' | 'reject'; onClose: () => void; onDone: () => void }) {
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const approve = kind === 'approve'

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    if (!approve && note.trim().length < 5) { setError('Bắt buộc ghi lý do từ chối (ít nhất 5 ký tự).'); return }
    setSaving(true)
    try {
      if (approve) await adminRevenueService.approveRefund(request.id, note.trim())
      else await adminRevenueService.rejectRefund(request.id, note.trim())
      toast.success(approve ? `Đã hoàn tiền đơn ${request.orderCode}` : 'Đã từ chối yêu cầu')
      onDone()
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={approve ? `Hoàn tiền đơn ${request.orderCode}` : `Từ chối hoàn tiền đơn ${request.orderCode}`} onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <p className="rounded-(--radius-control) bg-surface-2 p-4 text-sm"><b>Lý do của học viên:</b> {request.reason}</p>
        {approve && (
          <p className="rounded-(--radius-control) bg-warning-50 p-4 text-sm">
            Chỉ xác nhận sau khi đã hoàn {money(request.orderTotal)} cho khách qua cổng thanh toán. Hệ thống sẽ thu hồi quyền học, điểm thưởng
            và trừ lại thu nhập của giảng viên.
          </p>
        )}
        <TextArea label={approve ? 'Ghi chú (không bắt buộc)' : 'Lý do từ chối (gửi cho học viên)'} rows={3} value={note}
          onChange={(e) => setNote(e.target.value)} maxLength={500} error={error ?? undefined} />
        <button type="submit" className={approve ? 'btn-primary' : 'btn-primary bg-danger hover:bg-danger'} disabled={saving}>
          {saving && <Spinner className="size-4" />}{approve ? 'Xác nhận đã hoàn tiền' : 'Từ chối'}
        </button>
      </form>
    </Modal>
  )
}
