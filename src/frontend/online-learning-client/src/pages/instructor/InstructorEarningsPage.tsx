import { useState, type FormEvent } from 'react'
import { Wallet } from 'lucide-react'
import { toast } from 'sonner'
import { DataTable, Modal, PageHeader, Panel, StatCard } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Pagination, Skeleton, Spinner, TextArea } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { getErrorMessage, getFieldErrors } from '../../services/apiClient'
import {
  earningsService, payoutStatusLabel, payoutStatusStyle, type EarningsSummary, type EarningType,
} from '../../services/revenueApi'
import { formatDate, formatDateTime, formatNumber, formatPrice } from '../../utils/format'

const money = (v: number) => formatPrice(v, '0đ')

/** Thu nhập của giảng viên: số dư, sổ thu nhập theo đơn, yêu cầu rút tiền. */
export function InstructorEarningsPage() {
  useDocumentTitle('Doanh thu và rút tiền')
  const summary = useAsync(() => earningsService.summary(), [])
  const [type, setType] = useState<EarningType | ''>('')
  const [page, setPage] = useState(1)
  const earnings = useAsync(() => earningsService.list(page, type || undefined), [page, type])
  const [payoutPage, setPayoutPage] = useState(1)
  const payouts = useAsync(() => earningsService.payouts(payoutPage), [payoutPage])
  const [requesting, setRequesting] = useState(false)

  const reloadAll = () => { summary.reload(); payouts.reload() }

  const cancel = async (id: string) => {
    if (!window.confirm('Hủy yêu cầu rút tiền này?')) return
    try {
      await earningsService.cancelPayout(id)
      toast.success('Đã hủy yêu cầu rút tiền')
      reloadAll()
    } catch (err) {
      toast.error(getErrorMessage(err))
    }
  }

  if (summary.error) return <ErrorState message={summary.error} onRetry={summary.reload} />
  const s = summary.data
  const canRequest = !!s && !s.hasOpenPayout && s.withdrawableAmount >= s.minPayoutAmount

  return (
    <div className="flex flex-col gap-8">
      <PageHeader
        title="Doanh thu và rút tiền"
        description={s ? `Bạn nhận ${formatNumber(s.sharePercent)}% số tiền học viên thực trả. Tiền được rút sau ${s.holdDays} ngày kể từ khi bán (hết hạn hoàn tiền).` : undefined}
        actions={s && (
          <button type="button" className="btn-primary" disabled={!canRequest} onClick={() => setRequesting(true)}
            title={s.hasOpenPayout ? 'Bạn đang có yêu cầu chờ xử lý' : s.withdrawableAmount < s.minPayoutAmount ? `Cần tối thiểu ${money(s.minPayoutAmount)}` : undefined}>
            <Wallet className="size-4" aria-hidden="true" />Rút tiền
          </button>
        )}
      />

      {!s ? (
        <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">{Array.from({ length: 4 }, (_, i) => <Skeleton key={i} className="h-28" />)}</div>
      ) : (
        <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
          <StatCard label="Có thể rút" value={money(s.withdrawableAmount)} sub={`Tối thiểu mỗi lần ${money(s.minPayoutAmount)}`} tone="brand" />
          <StatCard label="Đang chờ đối soát" value={money(s.pendingAmount)} sub={`Mở sau ${s.holdDays} ngày kể từ khi bán`} />
          <StatCard label="Đã nhận" value={money(s.paidOutAmount)} sub={s.inProcessAmount > 0 ? `Đang xử lý ${money(s.inProcessAmount)}` : 'Đã chuyển vào tài khoản'} />
          <StatCard label="Tổng thu nhập" value={money(s.totalEarned)} sub={`${s.salesCount} lượt bán${s.refundCount ? ` · ${s.refundCount} hoàn tiền` : ''}`} tone="accent" />
        </div>
      )}

      <Panel title="Yêu cầu rút tiền">
        {payouts.error ? <ErrorState message={payouts.error} onRetry={payouts.reload} /> : payouts.loading ? <Skeleton className="h-24" /> :
          payouts.data?.items.length === 0 ? <p className="text-ink-soft">Chưa có yêu cầu rút tiền nào.</p> : (
            <DataTable minWidth={760} head={<><th>Ngày gửi</th><th className="text-right">Số tiền</th><th>Tài khoản nhận</th><th>Trạng thái</th><th>Ghi chú</th><th /></>}>
              {payouts.data?.items.map((p) => (
                <tr key={p.id}>
                  <td className="text-ink-soft">{formatDateTime(p.createdAt)}</td>
                  <td className="text-right font-semibold tabular-nums">{money(p.amount)}</td>
                  <td><p className="font-medium">{p.bankName} · {p.bankAccountNumber}</p><p className="text-ink-muted">{p.bankAccountName}</p></td>
                  <td>
                    <span className={`inline-flex rounded-(--radius-control) px-2.5 py-0.5 text-sm font-semibold ${payoutStatusStyle[p.status]}`}>{payoutStatusLabel[p.status]}</span>
                    {p.processedAt && <p className="mt-1 text-xs text-ink-muted">{formatDateTime(p.processedAt)}</p>}
                  </td>
                  <td className="max-w-64 text-ink-soft">
                    {p.transferReference && <p>Mã chuyển khoản: <b className="text-ink">{p.transferReference}</b></p>}
                    {p.adminNote && <p>{p.adminNote}</p>}
                    {!p.transferReference && !p.adminNote && (p.note ?? '—')}
                  </td>
                  <td>{p.status === 'Requested' && <button type="button" className="btn-ghost min-h-9 px-3 text-danger" onClick={() => cancel(p.id)}>Hủy</button>}</td>
                </tr>
              ))}
            </DataTable>
          )}
        <div className="mt-4"><Pagination page={payoutPage} totalPages={payouts.data?.totalPages ?? 1} onChange={setPayoutPage} /></div>
      </Panel>

      <Panel title="Sổ thu nhập"
        actions={
          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Lọc loại">
            {([['', 'Tất cả'], ['Sale', 'Bán khóa học'], ['Refund', 'Hoàn tiền']] as const).map(([v, l]) => (
              <button key={v} type="button" role="tab" aria-selected={type === v} className={type === v ? 'btn-primary min-h-9' : 'btn-soft min-h-9'}
                onClick={() => { setType(v); setPage(1) }}>{l}</button>
            ))}
          </div>
        }>
        {earnings.error ? <ErrorState message={earnings.error} onRetry={earnings.reload} /> : earnings.loading ? <Skeleton className="h-48" /> :
          earnings.data?.items.length === 0 ? <EmptyState title="Chưa có thu nhập">Thu nhập được ghi khi học viên thanh toán khóa học của bạn.</EmptyState> : (
            <DataTable minWidth={820} head={<><th>Ngày</th><th>Đơn hàng</th><th>Khóa học</th><th className="text-right">Học viên trả</th><th className="text-right">Bạn nhận</th><th>Trạng thái</th></>}>
              {earnings.data?.items.map((e) => (
                <tr key={e.id}>
                  <td className="text-ink-soft">{formatDate(e.createdAt)}</td>
                  <td className="text-xs tabular-nums">{e.orderCode}</td>
                  <td className="font-medium">{e.courseTitle}{e.type === 'Refund' && <span className="ml-2 text-sm font-semibold text-danger">Hoàn tiền</span>}</td>
                  <td className="text-right tabular-nums">{money(e.grossAmount)}</td>
                  <td className={`text-right font-semibold tabular-nums ${e.amount < 0 ? 'text-danger' : ''}`}>{money(e.amount)}<span className="block text-xs font-normal text-ink-muted">{formatNumber(e.sharePercent)}%</span></td>
                  <td>{e.isAvailable
                    ? <span className="text-sm font-semibold text-success">Đã đối soát</span>
                    : <span className="text-sm text-warning">Mở ngày {formatDate(e.availableAt)}</span>}</td>
                </tr>
              ))}
            </DataTable>
          )}
        <div className="mt-4"><Pagination page={page} totalPages={earnings.data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>

      {requesting && s && <PayoutModal summary={s} onClose={() => setRequesting(false)} onDone={() => { setRequesting(false); reloadAll() }} />}
    </div>
  )
}

function PayoutModal({ summary, onClose, onDone }: { summary: EarningsSummary; onClose: () => void; onDone: () => void }) {
  const [f, setF] = useState({
    amount: String(Math.floor(summary.withdrawableAmount)),
    bankName: summary.lastBank?.bankName ?? '',
    bankAccountNumber: summary.lastBank?.bankAccountNumber ?? '',
    bankAccountName: summary.lastBank?.bankAccountName ?? '',
    note: '',
  })
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [saving, setSaving] = useState(false)

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    const amount = Number(f.amount)
    if (!amount || amount < summary.minPayoutAmount) { setErrors({ amount: `Tối thiểu ${money(summary.minPayoutAmount)}` }); return }
    if (amount > summary.withdrawableAmount) { setErrors({ amount: `Tối đa ${money(summary.withdrawableAmount)}` }); return }
    setSaving(true)
    setErrors({})
    try {
      await earningsService.requestPayout({ ...f, amount, note: f.note.trim() || null })
      toast.success('Đã gửi yêu cầu rút tiền. EduViet sẽ chuyển khoản và báo lại cho bạn.')
      onDone()
    } catch (err) {
      const fe = getFieldErrors(err)
      setErrors(fe)
      if (!Object.keys(fe).length) toast.error(getErrorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title="Rút tiền về tài khoản ngân hàng" onClose={onClose}>
      <form className="flex flex-col gap-4" onSubmit={submit} noValidate>
        <p className="text-sm text-ink-soft">Số dư có thể rút: <b className="text-ink">{money(summary.withdrawableAmount)}</b>. Mỗi lúc chỉ có một yêu cầu chờ xử lý.</p>
        <Field label="Số tiền (đ)" type="number" inputMode="numeric" min={summary.minPayoutAmount} max={summary.withdrawableAmount} step={1000}
          value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} error={errors.amount} />
        <Field label="Ngân hàng" value={f.bankName} onChange={(e) => setF({ ...f, bankName: e.target.value })} placeholder="Ví dụ: Vietcombank" error={errors.bankName} maxLength={100} />
        <Field label="Số tài khoản" inputMode="numeric" value={f.bankAccountNumber} onChange={(e) => setF({ ...f, bankAccountNumber: e.target.value.replace(/\s/g, '') })}
          error={errors.bankAccountNumber} maxLength={20} />
        <Field label="Chủ tài khoản" value={f.bankAccountName} onChange={(e) => setF({ ...f, bankAccountName: e.target.value })} hint="Viết đúng tên trên tài khoản ngân hàng"
          error={errors.bankAccountName} maxLength={100} />
        <TextArea label="Ghi chú (không bắt buộc)" rows={2} value={f.note} onChange={(e) => setF({ ...f, note: e.target.value })} maxLength={500} />
        <button type="submit" className="btn-primary" disabled={saving}>{saving && <Spinner className="size-4" />}Gửi yêu cầu</button>
      </form>
    </Modal>
  )
}
