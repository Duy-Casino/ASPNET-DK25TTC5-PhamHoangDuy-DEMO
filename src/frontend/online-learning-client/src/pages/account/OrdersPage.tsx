import { useState } from 'react'
import { Link } from 'react-router-dom'
import { PageHeader, EmptyState, ErrorState, OrderStatusBadge, Pagination, Skeleton } from '../../components/ui'
import { useAsync, useDocumentTitle } from '../../hooks/useAsync'
import { orderService } from '../../services/api'
import type { OrderStatus } from '../../types/common'
import { formatDateTime, formatPrice } from '../../utils/format'

const tabs: { value: OrderStatus | ''; label: string }[] = [
  { value: '', label: 'Tất cả' },
  { value: 'Paid', label: 'Đã thanh toán' },
  { value: 'Pending', label: 'Chờ thanh toán' },
  { value: 'Cancelled', label: 'Đã hủy' },
]

export default function OrdersPage() {
  useDocumentTitle('Lịch sử mua hàng')
  const [page, setPage] = useState(1)
  const [status, setStatus] = useState<OrderStatus | ''>('')
  const { data, loading, error, reload } = useAsync(() => orderService.mine(page, status || undefined), [page, status])

  return (
    <>
      <PageHeader title="Lịch sử mua hàng" crumbs={[{ label: 'Tài khoản', to: '/tai-khoan' }, { label: 'Lịch sử mua hàng' }]} description="Theo dõi đơn hàng, trạng thái thanh toán và hóa đơn của bạn." />
    <div className="container-page py-10 md:py-12">
      <div className="mt-8 flex flex-wrap gap-2" role="tablist" aria-label="Lọc theo trạng thái">
        {tabs.map((t) => (
          <button key={t.value} type="button" role="tab" aria-selected={status === t.value}
            className={status === t.value ? 'btn-primary' : 'btn-soft'}
            onClick={() => { setStatus(t.value); setPage(1) }}>
            {t.label}
          </button>
        ))}
      </div>

      <div className="mt-10">
        {error ? (
          <ErrorState message={error} onRetry={reload} />
        ) : loading ? (
          <div className="flex flex-col gap-3"><Skeleton className="h-20" /><Skeleton className="h-20" /><Skeleton className="h-20" /></div>
        ) : data?.items.length === 0 ? (
          <EmptyState title="Chưa có đơn hàng" action={<Link to="/khoa-hoc" className="btn-primary">Khám phá khóa học</Link>}>
            Đơn hàng của bạn sẽ hiển thị ở đây.
          </EmptyState>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-left">
                <thead className="text-sm text-ink-soft">
                  <tr className="border-b border-line">
                    <th className="py-3 pr-4 font-semibold">Mã đơn</th>
                    <th className="py-3 pr-4 font-semibold">Khóa học</th>
                    <th className="py-3 pr-4 font-semibold">Ngày đặt</th>
                    <th className="py-3 pr-4 text-right font-semibold">Tổng tiền</th>
                    <th className="py-3 font-semibold">Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {data?.items.map((o) => (
                    <tr key={o.id} className="border-b border-line hover:bg-surface-2">
                      <td className="py-4 pr-4"><Link to={`/don-hang/${o.id}`} className="font-bold text-brand-700 hover:underline">{o.orderCode}</Link></td>
                      <td className="py-4 pr-4">{o.firstCourseTitle}{o.itemCount > 1 && <span className="text-ink-muted"> +{o.itemCount - 1} khóa khác</span>}</td>
                      <td className="py-4 pr-4 text-ink-soft">{formatDateTime(o.createdAt)}</td>
                      <td className="py-4 pr-4 text-right font-semibold tabular-nums">{formatPrice(o.totalAmount, '0đ')}</td>
                      <td className="py-4"><OrderStatusBadge status={o.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <div className="mt-8"><Pagination page={page} totalPages={data?.totalPages ?? 1} onChange={setPage} /></div>
          </>
        )}
      </div>
    </div>
    </>
  )
}
