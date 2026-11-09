import { useState } from 'react'
import { Link } from 'react-router-dom'
import { DataTable, PageHeader, Panel, SelectField } from '../../components/dashboard'
import { EmptyState, ErrorState, Field, Pagination, Skeleton } from '../../components/ui'
import { useAsync, useDebounce, useDocumentTitle } from '../../hooks/useAsync'
import { paymentService, providerLabel } from '../../services/paymentApi'
import { formatDateTime, formatPrice } from '../../utils/format'

const statusStyle = { Success: 'text-success', Failed: 'text-danger', Pending: 'text-warning' } as const
const statusLabel = { Success: 'Thành công', Failed: 'Thất bại', Pending: 'Đang chờ' } as const

/** Quản lý giao dịch: mỗi lần người dùng bấm thanh toán tạo một giao dịch với mã riêng gửi sang cổng. */
export default function AdminPaymentsPage() {
  useDocumentTitle('Giao dịch')
  const [status, setStatus] = useState('')
  const [provider, setProvider] = useState('')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const q = useDebounce(keyword)
  const { data, loading, error, reload } = useAsync(
    () => paymentService.adminList({ status: status || undefined, provider: provider || undefined, keyword: q || undefined, page }),
    [status, provider, q, page],
  )

  return (
    <div className="flex flex-col gap-8">
      <PageHeader title="Giao dịch thanh toán" description="Đối soát giao dịch qua VNPay, MoMo và kênh thử nghiệm (không thu tiền thật)." />
      <Panel>
        <div className="mb-6 grid gap-4 sm:grid-cols-[1fr_200px_200px]">
          <Field label="Tìm kiếm" type="search" value={keyword} onChange={(e) => { setKeyword(e.target.value); setPage(1) }} placeholder="Mã giao dịch, mã đơn, email" />
          <SelectField label="Cổng" value={provider} onChange={(v) => { setProvider(v); setPage(1) }}>
            <option value="">Tất cả</option>
            <option value="VNPay">VNPay</option>
            <option value="MoMo">MoMo</option>
            <option value="Test">Kênh thử nghiệm</option>
            <option value="Free">Miễn phí</option>
          </SelectField>
          <SelectField label="Trạng thái" value={status} onChange={(v) => { setStatus(v); setPage(1) }}>
            <option value="">Tất cả</option>
            <option value="Success">Thành công</option>
            <option value="Pending">Đang chờ</option>
            <option value="Failed">Thất bại</option>
          </SelectField>
        </div>
        {error ? <ErrorState message={error} onRetry={reload} /> : loading ? <Skeleton className="h-48" /> : data?.items.length === 0 ? <EmptyState title="Không có giao dịch" /> : (
          <DataTable minWidth={980} head={<><th>Mã giao dịch</th><th>Đơn hàng</th><th>Cổng</th><th className="text-right">Số tiền</th><th>Trạng thái</th><th>Mã cổng</th><th>Thời gian</th></>}>
            {data?.items.map((p) => (
              <tr key={p.id}>
                <td className="text-xs tabular-nums">{p.transactionRef}</td>
                <td><Link to={`/don-hang/${p.orderId}`} className="font-semibold text-brand-700 hover:underline">{p.orderCode}</Link><p className="text-ink-muted">{p.userEmail}</p></td>
                <td>{providerLabel[p.provider]}{p.provider === 'Test' && <span className="block text-xs text-ink-muted">không thu tiền</span>}</td>
                <td className="text-right font-semibold tabular-nums">{formatPrice(p.amount, '0đ')}</td>
                <td className={`font-semibold ${statusStyle[p.status]}`}>{statusLabel[p.status]}</td>
                <td className="text-ink-soft">{p.providerTransactionId ?? '—'}{p.responseCode && <span className="block text-xs">mã {p.responseCode}</span>}</td>
                <td className="text-ink-soft">{formatDateTime(p.createdAt)}</td>
              </tr>
            ))}
          </DataTable>
        )}
        <div className="mt-6"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
      </Panel>
    </div>
  )
}
