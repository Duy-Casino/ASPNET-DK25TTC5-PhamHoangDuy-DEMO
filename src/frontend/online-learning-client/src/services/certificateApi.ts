import { apiClient } from './apiClient'
import type { PagedResult } from '../types/common'

export interface CertificateVerify {
  certificateCode: string
  studentName: string
  courseTitle: string
  courseSlug: string
  instructorName: string
  issuedAt: string
  finalScore: number | null
  totalLessons: number
  totalDurationSeconds: number
  isRevoked: boolean
  revokedAt: string | null
  revokedReason: string | null
}

export interface AdminCertificate {
  id: string; certificateCode: string; studentName: string; studentEmail: string; courseTitle: string; issuedAt: string
  finalScore: number | null; isRevoked: boolean; revokedAt: string | null; revokedReason: string | null
}

/** Tải file nhị phân (cần token) rồi cho trình duyệt lưu. */
async function downloadBlob(url: string, fileName: string) {
  const res = await apiClient.get<Blob>(url, { responseType: 'blob' })
  const href = URL.createObjectURL(res.data)
  const a = document.createElement('a')
  a.href = href
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(href), 1000)
}

export const certificateService = {
  verify: (code: string) => apiClient.get<CertificateVerify>(`/api/certificates/verify/${encodeURIComponent(code.trim())}`).then((r) => r.data),
  publicPdfUrl: (code: string) => `/api/certificates/verify/${encodeURIComponent(code)}/pdf`,
  downloadMine: (id: string, code: string) => downloadBlob(`/api/certificates/${id}/pdf`, `chung-chi-${code}.pdf`),
  verifyPageUrl: (code: string) => `${window.location.origin}/xac-minh/${code}`,
  adminList: (params: { keyword?: string; isRevoked?: boolean; page?: number }) =>
    apiClient.get<PagedResult<AdminCertificate>>('/api/admin/certificates', { params: { ...params, pageSize: 20 } }).then((r) => r.data),
  revoke: (id: string, reason: string) => apiClient.post(`/api/admin/certificates/${id}/revoke`, { reason }),
  restore: (id: string) => apiClient.post(`/api/admin/certificates/${id}/restore`),
  backfill: () => apiClient.post<{ issued: number }>('/api/admin/certificates/backfill').then((r) => r.data),
  downloadAdmin: (id: string, code: string) => downloadBlob(`/api/certificates/${id}/pdf`, `chung-chi-${code}.pdf`),
}
