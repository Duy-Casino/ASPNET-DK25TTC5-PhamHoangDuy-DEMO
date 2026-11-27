import { apiClient } from './apiClient'
import type { CourseSummary } from '../types/api'

// ---------- Kiểu dữ liệu ----------
export interface WishlistItem { course: CourseSummary; addedAt: string }
export interface RecommendedCourse { course: CourseSummary; reason: string }

export interface BundleCourseBrief { id: string; title: string; slug: string; thumbnailUrl: string | null; price: number }
export interface BundleSummary {
  id: string; title: string; slug: string; description: string | null; thumbnailUrl: string | null
  price: number; originalTotal: number; savingPercent: number; courses: BundleCourseBrief[]
}
export interface BundleDetail extends Omit<BundleSummary, 'courses'> { courses: CourseSummary[]; ownedCourseIds: string[] }
export interface AdminBundle {
  id: string; title: string; slug: string; description: string | null; thumbnailUrl: string | null; price: number
  originalTotal: number; isActive: boolean; courses: BundleCourseBrief[]; orderCount: number; createdAt: string
}
export interface SaveBundleInput { title: string; description: string | null; thumbnailUrl: string | null; price: number; isActive: boolean; courseIds: string[] }

export type GiftStatus = 'Pending' | 'Sent' | 'Redeemed' | 'Cancelled'
export interface GiftView {
  senderName: string; recipientName: string; recipientEmailMasked: string; message: string | null; status: GiftStatus
  courses: BundleCourseBrief[]; canRedeem: boolean; blockedReason: string | null; redeemedAt: string | null
}
export interface GiftInput { recipientEmail: string; recipientName: string; message: string | null }

// ---------- API ----------
export const wishlistService = {
  ids: () => apiClient.get<string[]>('/api/wishlist/ids').then((r) => r.data),
  list: () => apiClient.get<WishlistItem[]>('/api/wishlist').then((r) => r.data),
  add: (courseId: string) => apiClient.post(`/api/wishlist/${courseId}`),
  remove: (courseId: string) => apiClient.delete(`/api/wishlist/${courseId}`),
}

export const recommendationService = {
  forCourse: (slug: string, take = 4) => apiClient.get<RecommendedCourse[]>(`/api/courses/${encodeURIComponent(slug)}/related`, { params: { take } }).then((r) => r.data),
  forCart: (take = 4) => apiClient.get<RecommendedCourse[]>('/api/cart/suggestions', { params: { take } }).then((r) => r.data),
}

export const bundleService = {
  list: (courseId?: string) => apiClient.get<BundleSummary[]>('/api/bundles', { params: courseId ? { courseId } : undefined }).then((r) => r.data),
  get: (slug: string) => apiClient.get<BundleDetail>(`/api/bundles/${encodeURIComponent(slug)}`).then((r) => r.data),
  adminList: () => apiClient.get<AdminBundle[]>('/api/admin/bundles').then((r) => r.data),
  create: (input: SaveBundleInput) => apiClient.post<AdminBundle>('/api/admin/bundles', input).then((r) => r.data),
  update: (id: string, input: SaveBundleInput) => apiClient.put<AdminBundle>(`/api/admin/bundles/${id}`, input).then((r) => r.data),
  remove: (id: string) => apiClient.delete(`/api/admin/bundles/${id}`),
}

export const giftService = {
  get: (code: string) => apiClient.get<GiftView>(`/api/gifts/${encodeURIComponent(code)}`).then((r) => r.data),
  redeem: (code: string) => apiClient.post<{ enrolledCount: number; alreadyOwned: string[] }>(`/api/gifts/${encodeURIComponent(code)}/redeem`).then((r) => r.data),
}

export const promotionService = {
  set: (courseId: string, discountPrice: number | null, discountEndsAt: string | null) =>
    apiClient.put(`/api/instructor/courses/${courseId}/promotion`, { discountPrice, discountEndsAt }).then((r) => r.data),
}

export const giftStatusLabel: Record<GiftStatus, string> = { Pending: 'Chờ thanh toán', Sent: 'Đã gửi, chờ nhận', Redeemed: 'Đã nhận', Cancelled: 'Đã hủy' }
