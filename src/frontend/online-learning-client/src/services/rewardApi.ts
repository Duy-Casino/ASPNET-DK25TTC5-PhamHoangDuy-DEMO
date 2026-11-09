import { apiClient } from './apiClient'
import type { PagedResult } from '../types/common'

// ---------------------------------------------------------------- Kiểu dữ liệu

export type PointTransactionType = 'Earn' | 'Spend' | 'Revoke' | 'Adjust'
export type ShareChannel = 'Facebook' | 'Zalo' | 'CopyLink' | 'Other'

export interface CheckInDay { date: string; checked: boolean; isToday: boolean }
export interface CheckInStatus {
  enabled: boolean; checkedInToday: boolean; currentStreak: number; streakTarget: number; streakBonus: number
  dailyPoints: number; streakBonusEnabled: boolean; recentDays: CheckInDay[]
}
export interface RewardTask { key: string; name: string; description: string | null; points: number; condition: string; enabled: boolean; doneToday: boolean }
export interface ReferralInfo { code: string; referredCount: number; rewardedCount: number; pointsPerReferral: number; enabled: boolean }
export interface RedeemConfig { enabled: boolean; pointsPerUnit: number; valuePerUnit: number; minPoints: number; options: number[] }
export interface PointSummary {
  balance: number; totalEarned: number; totalSpent: number; checkIn: CheckInStatus; tasks: RewardTask[]; referral: ReferralInfo; redeem: RedeemConfig
}
export interface PointTransaction { id: string; points: number; balanceAfter: number; type: PointTransactionType; ruleKey: string | null; description: string; createdAt: string }
export interface CheckInResult { pointsAwarded: number; streakBonus: number; streak: number; balance: number }
export interface ShareLink { token: string; courseId: string; courseSlug: string; courseTitle: string; channel: ShareChannel; referralCode: string }
export interface Redemption { id: string; points: number; voucherValue: number; couponCode: string; expiresAt: string; used: boolean; createdAt: string }

export interface RuleUsage { ruleKey: string; ruleName: string; count: number; points: number }
export interface RewardOverview {
  totalIssued: number; totalSpent: number; outstandingBalance: number; holders: number; transactionCount: number; issuedLast30Days: number; topRules: RuleUsage[]
}
export interface UserBalance { userId: string; fullName: string; email: string; avatarUrl: string | null; balance: number; totalEarned: number; totalSpent: number; updatedAt: string }
export interface AdminPointTransaction extends PointTransaction { userId: string; userFullName: string; userEmail: string; createdByName: string | null }
export interface PointRule {
  id: string; key: string; name: string; description: string | null; points: number; amountUnit: number | null; threshold: number | null
  isActive: boolean; isSystem: boolean; sortOrder: number; condition: string; updatedAt: string | null
}
export interface SavePointRuleInput {
  key?: string | null; name: string; description: string | null; points: number; amountUnit: number | null; threshold: number | null; isActive: boolean; sortOrder: number
}
export interface TransactionFilter { userId?: string; keyword?: string; ruleKey?: string; type?: PointTransactionType | ''; from?: string; to?: string; page?: number }

// ---------------------------------------------------------------- API

const get = <T,>(url: string, params?: object) => apiClient.get<T>(url, { params }).then((r) => r.data)
const post = <T,>(url: string, body?: unknown) => apiClient.post<T>(url, body).then((r) => r.data)
const put = <T,>(url: string, body?: unknown) => apiClient.put<T>(url, body).then((r) => r.data)
const del = <T,>(url: string) => apiClient.delete<T>(url).then((r) => r.data)

/** Bỏ tham số rỗng để URL gọn và không lọc nhầm. */
const clean = (f: object) => Object.fromEntries(Object.entries(f).filter(([, v]) => v !== '' && v !== undefined && v !== null))

export const rewardService = {
  summary: () => get<PointSummary>('/api/rewards/summary'),
  transactions: (page = 1) => get<PagedResult<PointTransaction>>('/api/rewards/transactions', { page, pageSize: 10 }),
  checkIn: () => post<CheckInResult>('/api/rewards/check-in'),
  createShare: (courseId: string, channel: ShareChannel) => post<ShareLink>('/api/rewards/shares', { courseId, channel }),
  openShare: (token: string) => post<void>(`/api/rewards/shares/${encodeURIComponent(token)}/open`),
  redeem: (points: number) => post<Redemption>('/api/rewards/redeem', { points }),
  redemptions: () => get<Redemption[]>('/api/rewards/redemptions'),
}

export const adminRewardService = {
  overview: () => get<RewardOverview>('/api/admin/rewards/overview'),
  balances: (keyword = '', page = 1) => get<PagedResult<UserBalance>>('/api/admin/rewards/balances', clean({ keyword, page, pageSize: 15 })),
  transactions: (f: TransactionFilter) => get<PagedResult<AdminPointTransaction>>('/api/admin/rewards/transactions', clean({ ...f, pageSize: 15 })),
  exportCsv: (f: TransactionFilter) =>
    apiClient.get<Blob>('/api/admin/rewards/transactions/export', { params: clean({ ...f, page: undefined }), responseType: 'blob' }).then((r) => r.data),
  rules: () => get<PointRule[]>('/api/admin/rewards/rules'),
  createRule: (data: SavePointRuleInput) => post<PointRule>('/api/admin/rewards/rules', data),
  updateRule: (id: string, data: SavePointRuleInput) => put<PointRule>(`/api/admin/rewards/rules/${id}`, data),
  deleteRule: (id: string) => del<void>(`/api/admin/rewards/rules/${id}`),
  adjust: (userId: string, points: number, reason: string, ruleKey?: string | null) =>
    post<AdminPointTransaction>('/api/admin/rewards/adjustments', { userId, points, reason, ruleKey: ruleKey || null }),
  refundOrder: (orderId: string, reason: string) => post<void>(`/api/admin/orders/${orderId}/refund`, { reason }),
}

// ---------------------------------------------------------------- Liên kết chia sẻ / giới thiệu

const REF_KEY = 'eduviet_ref'

export function shareUrl(link: Pick<ShareLink, 'courseSlug' | 'token' | 'referralCode'>): string {
  return `${window.location.origin}/khoa-hoc/${link.courseSlug}?s=${encodeURIComponent(link.token)}&ref=${encodeURIComponent(link.referralCode)}`
}
export const referralUrl = (code: string) => `${window.location.origin}/dang-ky?ref=${encodeURIComponent(code)}`

/** Mã giới thiệu ghi nhớ khi khách mở liên kết (dùng khi đăng ký, giữ 30 ngày). */
export function rememberReferral(code: string) {
  try { localStorage.setItem(REF_KEY, JSON.stringify({ code: code.slice(0, 16).toUpperCase(), at: Date.now() })) } catch { /* trình duyệt chặn lưu trữ */ }
}
export function storedReferral(): string | null {
  try {
    const raw = localStorage.getItem(REF_KEY)
    if (!raw) return null
    const v = JSON.parse(raw) as { code: string; at: number }
    return Date.now() - v.at < 30 * 86_400_000 ? v.code : null
  } catch {
    return null
  }
}
export function clearReferral() {
  try { localStorage.removeItem(REF_KEY) } catch { /* bỏ qua */ }
}
