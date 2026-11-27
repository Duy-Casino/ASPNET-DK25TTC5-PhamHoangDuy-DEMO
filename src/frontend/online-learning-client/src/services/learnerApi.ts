import { apiClient } from './apiClient'
import type { PagedResult } from '../types/common'

export interface QaAuthor { id: string; fullName: string; avatarUrl: string | null }
export interface LessonAnswer { id: string; content: string; author: QaAuthor; isInstructor: boolean; isMine: boolean; createdAt: string }
export interface LessonQuestion {
  id: string; content: string; author: QaAuthor; isMine: boolean; isResolved: boolean; createdAt: string
  answers: LessonAnswer[]; canModerate: boolean
}
export interface InstructorQuestion {
  id: string; courseId: string; courseTitle: string; courseSlug: string; lessonId: string; lessonTitle: string
  content: string; authorName: string; answerCount: number; hasInstructorAnswer: boolean; isResolved: boolean; createdAt: string; lastActivityAt: string
}
export interface LessonNote { id: string; lessonId: string; positionSeconds: number | null; content: string; createdAt: string; updatedAt: string | null }

export const lessonQaService = {
  list: (lessonId: string, page = 1) =>
    apiClient.get<PagedResult<LessonQuestion>>(`/api/learning/lessons/${lessonId}/questions`, { params: { page, pageSize: 10 } }).then((r) => r.data),
  ask: (lessonId: string, content: string) => apiClient.post<LessonQuestion>(`/api/learning/lessons/${lessonId}/questions`, { content }).then((r) => r.data),
  answer: (questionId: string, content: string) => apiClient.post<LessonAnswer>(`/api/learning/questions/${questionId}/answers`, { content }).then((r) => r.data),
  setResolved: (questionId: string, isResolved: boolean) => apiClient.put(`/api/learning/questions/${questionId}/resolved`, { isResolved }),
  removeQuestion: (questionId: string) => apiClient.delete(`/api/learning/questions/${questionId}`),
  removeAnswer: (answerId: string) => apiClient.delete(`/api/learning/answers/${answerId}`),
  instructorInbox: (unanswered: boolean, page = 1) =>
    apiClient.get<PagedResult<InstructorQuestion>>('/api/instructor/questions', { params: { unanswered: unanswered || undefined, page, pageSize: 15 } }).then((r) => r.data),
}

export const noteService = {
  list: (lessonId: string) => apiClient.get<LessonNote[]>(`/api/learning/lessons/${lessonId}/notes`).then((r) => r.data),
  add: (lessonId: string, content: string, positionSeconds: number | null) =>
    apiClient.post<LessonNote>(`/api/learning/lessons/${lessonId}/notes`, { content, positionSeconds }).then((r) => r.data),
  update: (noteId: string, content: string, positionSeconds: number | null) =>
    apiClient.put<LessonNote>(`/api/learning/notes/${noteId}`, { content, positionSeconds }).then((r) => r.data),
  remove: (noteId: string) => apiClient.delete(`/api/learning/notes/${noteId}`),
}

export const reminderSettingsService = {
  get: () => apiClient.get<{ reminderEmailsEnabled: boolean }>('/api/account/reminder-settings').then((r) => r.data),
  set: (enabled: boolean) => apiClient.put<{ reminderEmailsEnabled: boolean }>('/api/account/reminder-settings', { reminderEmailsEnabled: enabled }).then((r) => r.data),
}

// ---------- Hỗ trợ học tập ----------
export type SupportRequestType = 'QuizRetake' | 'VideoIssue' | 'ProgressIssue' | 'CertificateIssue' | 'Other'
export type SupportRequestStatus = 'Open' | 'Resolved' | 'Rejected'
export interface SupportRequest {
  id: string; userId: string; userName: string; userEmail: string; courseId: string; courseTitle: string; quizId: string | null; lessonId: string | null
  type: SupportRequestType; message: string; status: SupportRequestStatus; response: string | null; createdAt: string; resolvedAt: string | null
  enrollmentId: string | null
}
export const supportTypeLabel: Record<SupportRequestType, string> = {
  QuizRetake: 'Xin thêm lượt làm bài kiểm tra', VideoIssue: 'Video không phát được', ProgressIssue: 'Tiến độ không được ghi nhận',
  CertificateIssue: 'Chưa nhận được chứng chỉ', Other: 'Vấn đề khác',
}
export const supportStatusLabel: Record<SupportRequestStatus, string> = { Open: 'Đang chờ xử lý', Resolved: 'Đã xử lý', Rejected: 'Từ chối' }
export const supportStatusStyle: Record<SupportRequestStatus, string> = {
  Open: 'bg-warning-50 text-warning', Resolved: 'bg-success-50 text-success', Rejected: 'bg-danger-50 text-danger',
}

export const supportService = {
  mine: (courseId?: string) => apiClient.get<SupportRequest[]>('/api/learning/support-requests', { params: courseId ? { courseId } : undefined }).then((r) => r.data),
  create: (input: { courseId: string; type: SupportRequestType; message: string; quizId?: string | null; lessonId?: string | null }) =>
    apiClient.post<SupportRequest>('/api/learning/support-requests', { quizId: null, lessonId: null, ...input }).then((r) => r.data),
}

// ---------- Quản lý học tập (Admin) ----------
export type LearningState = 'not-started' | 'in-progress' | 'completed' | 'stuck' | 'inactive' | 'no-certificate' | 'revoked'
export const learningStateLabel: Record<LearningState, string> = {
  'not-started': 'Chưa bắt đầu', 'in-progress': 'Đang học', completed: 'Hoàn thành', stuck: 'Hết lượt, chưa đạt',
  inactive: 'Bỏ dở > 14 ngày', 'no-certificate': 'Đủ điều kiện, chưa có chứng chỉ', revoked: 'Đã thu hồi',
}
export const learningStateStyle: Record<LearningState, string> = {
  'not-started': 'bg-surface-3 text-ink-soft', 'in-progress': 'bg-brand-50 text-brand-800', completed: 'bg-success-50 text-success',
  stuck: 'bg-danger-50 text-danger', inactive: 'bg-warning-50 text-warning', 'no-certificate': 'bg-sun-100 text-ink', revoked: 'bg-surface-3 text-ink-muted',
}
export interface LearningOverview {
  total: number; notStarted: number; inProgress: number; completed: number; stuck: number; inactive: number; noCertificate: number
  revoked: number; completionRate: number; openSupportRequests: number; failedAttempts30Days: number
}
export interface AdminEnrollment {
  enrollmentId: string; userId: string; fullName: string; email: string; courseId: string; courseTitle: string; courseSlug: string
  progressPercent: number; completedLessons: number; requiredLessons: number; status: 'Active' | 'Completed' | 'Revoked'; state: LearningState
  finalBestScore: number | null; finalPassed: boolean; finalAttemptsUsed: number; finalMaxAttempts: number | null
  enrolledAt: string; lastAccessedAt: string | null; hasCertificate: boolean; source: string
}
export interface AdminLessonProgress { lessonId: string; sectionTitle: string; title: string; isRequired: boolean; isCompleted: boolean; completedAt: string | null; lastPositionSeconds: number }
export interface AdminAttempt { id: string; attemptNumber: number; startedAt: string; submittedAt: string | null; scorePercent: number; isPassed: boolean }
export interface AdminQuizProgress {
  quizId: string; title: string; isFinalExam: boolean; isRequired: boolean; passingScore: number; baseMaxAttempts: number | null
  extraAttempts: number; attemptsUsed: number; bestScore: number | null; isPassed: boolean; attempts: AdminAttempt[]
}
export interface LearningEvent { at: string; kind: string; title: string; detail: string | null }
export interface AdminEnrollmentDetail {
  summary: AdminEnrollment; lessons: AdminLessonProgress[]; quizzes: AdminQuizProgress[]; certificateCode: string | null
  timeline: LearningEvent[]; supportRequests: SupportRequest[]
}

const cleanParams = (o: Record<string, unknown>) => Object.fromEntries(Object.entries(o).filter(([, v]) => v !== '' && v !== undefined && v !== null))

export const adminLearningService = {
  overview: () => apiClient.get<LearningOverview>('/api/admin/learning/overview').then((r) => r.data),
  list: (params: { keyword?: string; courseId?: string; state?: LearningState | ''; page?: number }) =>
    apiClient.get<PagedResult<AdminEnrollment>>('/api/admin/learning/enrollments', { params: cleanParams({ ...params, pageSize: 15 }) }).then((r) => r.data),
  detail: (id: string) => apiClient.get<AdminEnrollmentDetail>(`/api/admin/learning/enrollments/${id}`).then((r) => r.data),
  grantAttempts: (id: string, quizId: string, extraAttempts: number, reason: string) =>
    apiClient.post(`/api/admin/learning/enrollments/${id}/grant-attempts`, { quizId, extraAttempts, reason }),
  setLesson: (id: string, lessonId: string, completed: boolean, reason: string) =>
    apiClient.post(`/api/admin/learning/enrollments/${id}/lesson`, { lessonId, completed, reason }),
  reset: (id: string, reason: string) => apiClient.post(`/api/admin/learning/enrollments/${id}/reset-progress`, { reason }),
  recalculate: (id: string, reason: string) => apiClient.post<{ progressPercent: number; certificateCode: string | null }>(`/api/admin/learning/enrollments/${id}/recalculate`, { reason }).then((r) => r.data),
  revoke: (id: string, reason: string) => apiClient.post(`/api/admin/learning/enrollments/${id}/revoke`, { reason }),
  restore: (id: string, reason: string) => apiClient.post(`/api/admin/learning/enrollments/${id}/restore`, { reason }),
  manualEnroll: (email: string, courseId: string, reason: string) =>
    apiClient.post<{ enrollmentId: string }>('/api/admin/learning/enrollments', { email, courseId, reason }).then((r) => r.data),
  requests: (params: { status?: SupportRequestStatus | ''; type?: SupportRequestType | ''; keyword?: string; page?: number }) =>
    apiClient.get<PagedResult<SupportRequest>>('/api/admin/learning/support-requests', { params: cleanParams({ ...params, pageSize: 15 }) }).then((r) => r.data),
  respond: (id: string, status: 'Resolved' | 'Rejected', response: string) =>
    apiClient.post(`/api/admin/learning/support-requests/${id}/respond`, { status, response }),
}
