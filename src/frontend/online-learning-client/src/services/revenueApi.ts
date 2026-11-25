import { apiClient } from './apiClient'
import type { OrderStatus, PagedResult } from '../types/common'

// ---------- Kiểu dữ liệu ----------
export type EarningType = 'Sale' | 'Refund'
export type PayoutStatus = 'Requested' | 'Paid' | 'Rejected' | 'Cancelled'
export type RefundRequestStatus = 'Pending' | 'Approved' | 'Rejected'

export interface PayoutBank { bankName: string; bankAccountNumber: string; bankAccountName: string }

export interface EarningsSummary {
  sharePercent: number; holdDays: number; minPayoutAmount: number
  totalEarned: number; pendingAmount: number; availableAmount: number; paidOutAmount: number; inProcessAmount: number
  withdrawableAmount: number; salesCount: number; refundCount: number; hasOpenPayout: boolean; lastBank: PayoutBank | null
}

export interface Earning {
  id: string; type: EarningType; orderCode: string; courseId: string; courseTitle: string
  grossAmount: number; sharePercent: number; amount: number; createdAt: string; availableAt: string; isAvailable: boolean
}

export interface Payout extends PayoutBank {
  id: string; amount: number; status: PayoutStatus; note: string | null; transferReference: string | null; adminNote: string | null
  createdAt: string; processedAt: string | null
}

export interface AdminPayout extends Payout { instructorId: string; instructorName: string; instructorEmail: string; instructorWithdrawable: number }

export interface InstructorBalance {
  instructorId: string; instructorName: string; instructorEmail: string
  grossSales: number; totalEarned: number; pendingAmount: number; paidOutAmount: number; inProcessAmount: number; withdrawableAmount: number; salesCount: number
}

export interface RevenueOverview {
  grossSales: number; instructorShare: number; platformShare: number; paidOut: number; inProcess: number
  owedToInstructors: number; openPayoutCount: number; sharePercent: number; instructors: InstructorBalance[]
}

export interface RefundRequest {
  id: string; orderId: string; reason: string; status: RefundRequestStatus; adminNote: string | null; createdAt: string; processedAt: string | null
}

export interface OrderRefundStatus {
  canRequest: boolean; blockedReason: string | null; deadline: string | null
  maxProgressPercent: number; currentProgressPercent: number; requests: RefundRequest[]
}

export interface AdminRefundRequest extends RefundRequest {
  orderCode: string; orderStatus: OrderStatus; orderTotal: number; paidAt: string | null
  userFullName: string; userEmail: string; maxProgressPercent: number; hasCertificate: boolean
}

export interface RequestPayoutInput extends PayoutBank { amount: number; note: string | null }

// ---------- Tải tệp (cần token) ----------
/** Gọi API trả về tệp (PDF, CSV) rồi để trình duyệt lưu với tên cho trước. */
export async function downloadFile(url: string, fileName: string, params?: Record<string, unknown>) {
  const res = await apiClient.get<Blob>(url, { responseType: 'blob', params })
  const href = URL.createObjectURL(res.data)
  const a = document.createElement('a')
  a.href = href
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(href), 1000)
}

const clean = (o: Record<string, unknown>) => Object.fromEntries(Object.entries(o).filter(([, v]) => v !== '' && v !== undefined && v !== null))
const today = () => new Date().toISOString().slice(0, 10)

// ---------- Giảng viên ----------
export const earningsService = {
  summary: () => apiClient.get<EarningsSummary>('/api/instructor/earnings/summary').then((r) => r.data),
  list: (page = 1, type?: EarningType) =>
    apiClient.get<PagedResult<Earning>>('/api/instructor/earnings', { params: clean({ page, pageSize: 10, type }) }).then((r) => r.data),
  payouts: (page = 1) => apiClient.get<PagedResult<Payout>>('/api/instructor/payouts', { params: { page, pageSize: 10 } }).then((r) => r.data),
  requestPayout: (input: RequestPayoutInput) => apiClient.post<Payout>('/api/instructor/payouts', input).then((r) => r.data),
  cancelPayout: (id: string) => apiClient.post(`/api/instructor/payouts/${id}/cancel`),
}

// ---------- Học viên ----------
export const refundService = {
  status: (orderId: string) => apiClient.get<OrderRefundStatus>(`/api/orders/${orderId}/refund-request`).then((r) => r.data),
  request: (orderId: string, reason: string) => apiClient.post<RefundRequest>(`/api/orders/${orderId}/refund-request`, { reason }).then((r) => r.data),
  downloadInvoice: (orderId: string, orderCode: string) => downloadFile(`/api/orders/${orderId}/invoice`, `bien-nhan-${orderCode}.pdf`),
}

// ---------- Quản trị ----------
export const adminRevenueService = {
  overview: () => apiClient.get<RevenueOverview>('/api/admin/revenue').then((r) => r.data),
  payouts: (params: { status?: PayoutStatus | ''; keyword?: string; page?: number }) =>
    apiClient.get<PagedResult<AdminPayout>>('/api/admin/payouts', { params: clean({ ...params, pageSize: 15 }) }).then((r) => r.data),
  markPaid: (id: string, transferReference: string, adminNote: string) =>
    apiClient.post(`/api/admin/payouts/${id}/paid`, { transferReference, adminNote: adminNote || null }),
  rejectPayout: (id: string, adminNote: string) => apiClient.post(`/api/admin/payouts/${id}/reject`, { adminNote }),
  exportPayouts: (params: { status?: PayoutStatus | ''; keyword?: string }) =>
    downloadFile('/api/admin/payouts/export', `chi-tra-giang-vien-${today()}.csv`, clean(params)),

  refundRequests: (params: { status?: RefundRequestStatus | ''; keyword?: string; page?: number }) =>
    apiClient.get<PagedResult<AdminRefundRequest>>('/api/admin/refund-requests', { params: clean({ ...params, pageSize: 15 }) }).then((r) => r.data),
  approveRefund: (id: string, adminNote: string) => apiClient.post(`/api/admin/refund-requests/${id}/approve`, { adminNote: adminNote || null }),
  rejectRefund: (id: string, adminNote: string) => apiClient.post(`/api/admin/refund-requests/${id}/reject`, { adminNote }),

  exportOrders: (params: { status?: OrderStatus | ''; keyword?: string; from?: string; to?: string }) =>
    downloadFile('/api/admin/orders/export', `don-hang-${today()}.csv`, clean(params)),
}

export const payoutStatusLabel: Record<PayoutStatus, string> = { Requested: 'Chờ xử lý', Paid: 'Đã chi', Rejected: 'Từ chối', Cancelled: 'Đã hủy' }
export const payoutStatusStyle: Record<PayoutStatus, string> = {
  Requested: 'bg-warning-50 text-warning', Paid: 'bg-success-50 text-success', Rejected: 'bg-danger-50 text-danger', Cancelled: 'bg-surface-3 text-ink-soft',
}
export const refundStatusLabel: Record<RefundRequestStatus, string> = { Pending: 'Chờ xử lý', Approved: 'Đã hoàn tiền', Rejected: 'Bị từ chối' }
export const refundStatusStyle: Record<RefundRequestStatus, string> = {
  Pending: 'bg-warning-50 text-warning', Approved: 'bg-success-50 text-success', Rejected: 'bg-danger-50 text-danger',
}
