import { apiClient } from './apiClient'
import type { OrderStatus, PagedResult } from '../types/common'

export type PaymentProvider = 'VNPay' | 'MoMo' | 'Free' | 'Test'

export interface PaymentMethod { provider: PaymentProvider; displayName: string; isTestMode: boolean }
export interface CheckoutResponse { orderId: string; provider: PaymentProvider; redirectUrl: string; isTestMode: boolean }
export interface PaymentResult {
  orderId: string | null
  orderCode: string | null
  orderStatus: OrderStatus | null
  provider: PaymentProvider | null
  paymentStatus: 'Pending' | 'Success' | 'Failed' | null
  signatureValid: boolean
  gatewayReportedSuccess: boolean
  isTestMode: boolean
  amount: number
  message: string | null
}
export interface AdminPayment {
  id: string; orderId: string; orderCode: string; userEmail: string; provider: PaymentProvider; amount: number
  status: 'Pending' | 'Success' | 'Failed'; transactionRef: string; providerTransactionId: string | null
  bankCode: string | null; responseCode: string | null; createdAt: string; paidAt: string | null
}

export interface UploadResult {
  publicId: string
  url: string | null
  fileName: string
  contentType: string
  size: number
  durationSeconds: number | null
  kind: 'image' | 'video' | 'document'
  storage: string
}

export const paymentService = {
  methods: () => apiClient.get<PaymentMethod[]>('/api/payments/methods').then((r) => r.data),
  checkout: (orderId: string, provider: PaymentProvider) =>
    apiClient.post<CheckoutResponse>(`/api/payments/orders/${orderId}/checkout`, { provider }).then((r) => r.data),
  /** Gửi nguyên query cổng trả về để Backend xác thực chữ ký (chỉ hiển thị, không cấp quyền). */
  returnResult: (provider: PaymentProvider, query: string) =>
    apiClient.get<PaymentResult>(`/api/payments/return/${provider}?${query}`).then((r) => r.data),
  adminList: (params: { status?: string; provider?: string; keyword?: string; page?: number }) =>
    apiClient.get<PagedResult<AdminPayment>>('/api/admin/payments', { params: { ...params, pageSize: 20 } }).then((r) => r.data),
}

/** Chuyển trình duyệt tới cổng thanh toán (URL ngoài) hoặc cổng test của API (đường dẫn /api/...). */
export function redirectToGateway(url: string) {
  window.location.assign(url)
}

export const uploadService = {
  upload: (kind: UploadResult['kind'], file: File, onProgress?: (percent: number) => void) => {
    const form = new FormData()
    form.append('file', file)
    return apiClient
      .post<UploadResult>(`/api/uploads/${kind}`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
        timeout: 30 * 60 * 1000,
        onUploadProgress: (e) => e.total && onProgress?.(Math.round((e.loaded / e.total) * 100)),
      })
      .then((r) => r.data)
  },
}

export const providerLabel: Record<PaymentProvider, string> = {
  VNPay: 'VNPay',
  MoMo: 'Ví MoMo',
  Free: 'Miễn phí',
  Test: 'Kênh thử nghiệm',
}
