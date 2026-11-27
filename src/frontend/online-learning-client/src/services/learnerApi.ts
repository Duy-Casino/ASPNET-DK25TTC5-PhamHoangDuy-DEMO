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
