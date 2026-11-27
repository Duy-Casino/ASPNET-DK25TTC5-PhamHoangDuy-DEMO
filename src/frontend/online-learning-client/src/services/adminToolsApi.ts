import { apiClient } from './apiClient'
import type { PagedResult } from '../types/common'
import { downloadFile } from './revenueApi'

// ---------- Kiểu dữ liệu ----------
export type ApplicationStatus = 'Pending' | 'Approved' | 'Rejected'
export type AnnouncementAudience = 'Everyone' | 'Students' | 'Instructors' | 'CourseStudents'
export type SettingType = 'Integer' | 'Decimal' | 'Boolean' | 'Text'

export interface AdminReview {
  id: string; courseId: string; courseTitle: string; courseSlug: string; userName: string; userEmail: string
  rating: number; comment: string | null; isHidden: boolean; instructorReply: string | null; createdAt: string
}

export interface InstructorApplication {
  id: string; userId: string; userName: string; userEmail: string; headline: string; bio: string; expertise: string
  proposedTopic: string; portfolioUrl: string | null; yearsOfExperience: number | null; status: ApplicationStatus; adminNote: string | null
  createdAt: string; processedAt: string | null
}
export interface MyApplicationStatus { isInstructor: boolean; latest: InstructorApplication | null }
export interface ApplicationInput {
  headline: string; bio: string; expertise: string; proposedTopic: string; portfolioUrl: string | null; yearsOfExperience: number | null
}

export interface AuditLog {
  id: string; actorId: string; actorName: string; action: string; summary: string; httpMethod: string; path: string
  payload: string | null; statusCode: number; succeeded: boolean; ipAddress: string | null; createdAt: string
}
export interface AuditQuery { keyword?: string; succeeded?: boolean; from?: string; to?: string; page?: number }

export interface Announcement {
  id: string; title: string; message: string; link: string | null; audience: AnnouncementAudience; courseId: string | null; courseTitle: string | null
  sendEmail: boolean; recipientCount: number; emailCount: number; createdByName: string; createdAt: string
}
export interface AnnouncementInput { audience: AnnouncementAudience; courseId: string | null; title: string; message: string; link: string | null; sendEmail: boolean }

export interface Setting {
  key: string; group: string; label: string; hint: string | null; type: SettingType; min: number | null; max: number | null
  value: string | null; isOverridden: boolean; updatedAt: string | null
}
export interface PublicSettings {
  refundRequestDays: number; refundMaxProgressPercent: number; instructorSharePercent: number
  sellerName: string; sellerEmail: string | null; sellerPhone: string | null; sellerAddress: string | null
}

export interface ReportCourse {
  courseId: string; title: string; instructorName: string; sales: number; revenue: number; enrollments: number
  completionRate: number; averageRating: number; refunds: number
}
export interface AnalyticsReport {
  from: string; to: string
  totals: { revenue: number; paidOrders: number; refundedAmount: number; refundedOrders: number; newUsers: number; enrollments: number; completions: number }
  monthly: { month: string; revenue: number; orders: number; enrollments: number }[]
  topCourses: ReportCourse[]
  topInstructors: { instructorId: string; name: string; sales: number; revenue: number; students: number; courses: number }[]
}
export interface CourseLessonStats {
  courseId: string; title: string; activeLearners: number
  lessons: { lessonId: string; sectionTitle: string; title: string; isRequired: boolean; completedCount: number; completedPercent: number }[]
}

export interface SystemStatus {
  serverTimeUtc: string; environment: string; uptime: string
  components: { name: string; status: 'ok' | 'warning' | 'error' | 'off'; detail: string }[]
  jobs: { key: string; name: string; interval: string; lastRunAt: string | null; lastSucceeded: boolean | null; lastMessage: string | null; enabled: boolean }[]
  recentEmails: { at: string; to: string; subject: string; succeeded: boolean; error: string | null; delivered: boolean }[]
  emailFailures24h: number
  counts: Record<string, number>
}

// ---------- Nhãn ----------
export const applicationStatusLabel: Record<ApplicationStatus, string> = { Pending: 'Chờ duyệt', Approved: 'Đã duyệt', Rejected: 'Từ chối' }
export const applicationStatusStyle: Record<ApplicationStatus, string> = {
  Pending: 'bg-warning-50 text-warning', Approved: 'bg-success-50 text-success', Rejected: 'bg-danger-50 text-danger',
}
export const audienceLabel: Record<AnnouncementAudience, string> = {
  Everyone: 'Tất cả người dùng', Students: 'Học viên', Instructors: 'Giảng viên', CourseStudents: 'Học viên của một khóa học',
}

const clean = (o: Record<string, unknown>) => Object.fromEntries(Object.entries(o).filter(([, v]) => v !== '' && v !== undefined && v !== null))
const stamp = () => new Date().toISOString().slice(0, 10)

// ---------- Admin ----------
export const adminToolsService = {
  reviews: (q: { rating?: number; isHidden?: boolean; keyword?: string; page?: number }) =>
    apiClient.get<PagedResult<AdminReview>>('/api/admin/reviews', { params: clean({ ...q, pageSize: 15 }) }).then((r) => r.data),
  setReviewHidden: (id: string, isHidden: boolean) => apiClient.put(`/api/admin/reviews/${id}/hidden`, { isHidden }),

  applications: (status: ApplicationStatus | '', page = 1) =>
    apiClient.get<PagedResult<InstructorApplication>>('/api/admin/instructor-applications', { params: clean({ status, page, pageSize: 10 }) }).then((r) => r.data),
  approve: (id: string, adminNote: string | null) => apiClient.post(`/api/admin/instructor-applications/${id}/approve`, { adminNote }),
  reject: (id: string, adminNote: string) => apiClient.post(`/api/admin/instructor-applications/${id}/reject`, { adminNote }),

  audit: (q: AuditQuery) => apiClient.get<PagedResult<AuditLog>>('/api/admin/audit-logs', { params: clean({ ...q, pageSize: 20 }) }).then((r) => r.data),
  exportAudit: (q: AuditQuery) => downloadFile('/api/admin/audit-logs/export', `nhat-ky-admin-${stamp()}.csv`, clean({ ...q, page: undefined })),

  announcements: (page = 1) => apiClient.get<PagedResult<Announcement>>('/api/admin/announcements', { params: { page, pageSize: 10 } }).then((r) => r.data),
  previewAudience: (audience: AnnouncementAudience, courseId?: string) =>
    apiClient.get<{ recipients: number; emailRecipients: number }>('/api/admin/announcements/preview', { params: clean({ audience, courseId }) }).then((r) => r.data),
  sendAnnouncement: (input: AnnouncementInput) => apiClient.post<Announcement>('/api/admin/announcements', input).then((r) => r.data),

  settings: () => apiClient.get<Setting[]>('/api/admin/settings').then((r) => r.data),
  saveSettings: (values: Record<string, string | null>) => apiClient.put('/api/admin/settings', { values }),

  report: (from?: string, to?: string) => apiClient.get<AnalyticsReport>('/api/admin/reports', { params: clean({ from, to }) }).then((r) => r.data),
  exportReport: (from?: string, to?: string) => downloadFile('/api/admin/reports/export', `bao-cao-khoa-hoc-${stamp()}.csv`, clean({ from, to })),
  lessonStats: (courseId: string) => apiClient.get<CourseLessonStats>(`/api/admin/reports/courses/${courseId}/lessons`).then((r) => r.data),

  system: () => apiClient.get<SystemStatus>('/api/admin/system').then((r) => r.data),
  runJob: (key: string) => apiClient.post<{ message: string }>(`/api/admin/system/jobs/${key}/run`).then((r) => r.data),

  exportUsers: (q: { keyword?: string; role?: string }) => downloadFile('/api/admin/users/export', `nguoi-dung-${stamp()}.csv`, clean(q)),
  exportEnrollments: (q: { state?: string; keyword?: string }) => downloadFile('/api/admin/learning/enrollments/export', `tien-do-hoc-tap-${stamp()}.csv`, clean(q)),
}

// ---------- Người dùng / công khai ----------
export const instructorApplicationService = {
  mine: () => apiClient.get<MyApplicationStatus>('/api/instructor-applications/me').then((r) => r.data),
  submit: (input: ApplicationInput) => apiClient.post<InstructorApplication>('/api/instructor-applications', input).then((r) => r.data),
}

export const publicSettingsService = {
  get: () => apiClient.get<PublicSettings>('/api/settings/public').then((r) => r.data),
}

export const lessonStatsService = {
  get: (courseId: string) => apiClient.get<CourseLessonStats>(`/api/instructor/courses/${courseId}/lesson-stats`).then((r) => r.data),
}
