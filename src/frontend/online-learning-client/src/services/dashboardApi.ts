import { apiClient } from './apiClient'
import type { Category, InstructorProfileEdit, SaveInstructorProfileInput } from '../types/api'
import type { CourseStatus, OrderStatus, PagedResult } from '../types/common'
import type {
  AdminCourse, AdminOrder, AdminOverview, AdminUser, AttemptResult, AttemptTake, CertificateItem, Coupon, CourseEdit,
  InstructorCourseListItem, InstructorOverview, InstructorReview, InstructorStudent, LearnCourse, LessonContent, LessonEdit,
  MyCourse, MyQuizResult, Progress, QuizEdit, QuizIntro, QuizListItem, SaveCouponInput, SaveCourseInput, SaveLessonInput,
  SaveQuizInput, SectionEdit, StudentSummary,
} from '../types/dashboard'

const get = <T,>(url: string, params?: object) => apiClient.get<T>(url, { params }).then((r) => r.data)
const post = <T,>(url: string, body?: unknown) => apiClient.post<T>(url, body).then((r) => r.data)
const put = <T,>(url: string, body?: unknown) => apiClient.put<T>(url, body).then((r) => r.data)
const del = <T,>(url: string) => apiClient.delete<T>(url).then((r) => r.data)

export const learningService = {
  summary: () => get<StudentSummary>('/api/learning/summary'),
  myCourses: (filter = 'all', page = 1, pageSize = 12) => get<PagedResult<MyCourse>>('/api/learning/courses', { filter, page, pageSize }),
  course: (slug: string) => get<LearnCourse>(`/api/learning/courses/${encodeURIComponent(slug)}`),
  lesson: (id: string) => get<LessonContent>(`/api/learning/lessons/${id}`),
  complete: (id: string) => post<Progress>(`/api/learning/lessons/${id}/complete`),
  uncomplete: (id: string) => del<Progress>(`/api/learning/lessons/${id}/complete`),
  savePosition: (id: string, positionSeconds: number) => put<void>(`/api/learning/lessons/${id}/position`, { positionSeconds }),
  quizIntro: (id: string) => get<QuizIntro>(`/api/learning/quizzes/${id}`),
  startAttempt: (quizId: string) => post<AttemptTake>(`/api/learning/quizzes/${quizId}/attempts`),
  submitAttempt: (attemptId: string, answers: { questionId: string; optionIds: string[] }[]) =>
    post<AttemptResult>(`/api/learning/attempts/${attemptId}/submit`, { answers }),
  attemptResult: (attemptId: string) => get<AttemptResult>(`/api/learning/attempts/${attemptId}`),
  quizResults: (page = 1) => get<PagedResult<MyQuizResult>>('/api/learning/quiz-results', { page, pageSize: 15 }),
  certificates: () => get<CertificateItem[]>('/api/learning/certificates'),
}

export const instructorService = {
  profile: () => get<InstructorProfileEdit>('/api/instructor/profile'),
  updateProfile: (data: SaveInstructorProfileInput) => put<InstructorProfileEdit>('/api/instructor/profile', data),
  overview: () => get<InstructorOverview>('/api/instructor/overview'),
  students: (params: { courseId?: string; keyword?: string; page?: number }) =>
    get<PagedResult<InstructorStudent>>('/api/instructor/students', { ...params, pageSize: 15 }),
  courses: (status?: CourseStatus, page = 1) => get<PagedResult<InstructorCourseListItem>>('/api/instructor/courses', { status, page, pageSize: 20 }),
  course: (id: string) => get<CourseEdit>(`/api/instructor/courses/${id}`),
  createCourse: (data: SaveCourseInput) => post<CourseEdit>('/api/instructor/courses', data),
  updateCourse: (id: string, data: SaveCourseInput) => put<CourseEdit>(`/api/instructor/courses/${id}`, data),
  deleteCourse: (id: string) => del<void>(`/api/instructor/courses/${id}`),
  submit: (id: string) => post<void>(`/api/instructor/courses/${id}/submit`),
  unpublish: (id: string) => post<void>(`/api/instructor/courses/${id}/unpublish`),
  addSection: (courseId: string, title: string, description?: string) => post<SectionEdit>(`/api/instructor/courses/${courseId}/sections`, { title, description: description || null }),
  updateSection: (id: string, title: string, description?: string | null) => put<SectionEdit>(`/api/instructor/sections/${id}`, { title, description: description || null }),
  deleteSection: (id: string) => del<void>(`/api/instructor/sections/${id}`),
  reorderSections: (courseId: string, orderedIds: string[]) => put<void>(`/api/instructor/courses/${courseId}/sections/order`, { orderedIds }),
  addLesson: (sectionId: string, data: SaveLessonInput) => post<LessonEdit>(`/api/instructor/sections/${sectionId}/lessons`, data),
  updateLesson: (id: string, data: SaveLessonInput) => put<LessonEdit>(`/api/instructor/lessons/${id}`, data),
  deleteLesson: (id: string) => del<void>(`/api/instructor/lessons/${id}`),
  reorderLessons: (sectionId: string, orderedIds: string[]) => put<void>(`/api/instructor/sections/${sectionId}/lessons/order`, { orderedIds }),
  addResource: (lessonId: string, data: { fileName: string; fileUrl: string; publicId: string | null; contentType: string | null; fileSize: number }) =>
    post<LessonEdit['resources'][number]>(`/api/instructor/lessons/${lessonId}/resources`, data),
  deleteResource: (id: string) => del<void>(`/api/instructor/resources/${id}`),
  quizzes: (courseId: string) => get<QuizListItem[]>(`/api/instructor/courses/${courseId}/quizzes`),
  quiz: (id: string) => get<QuizEdit>(`/api/instructor/quizzes/${id}`),
  createQuiz: (courseId: string, data: SaveQuizInput) => post<QuizEdit>(`/api/instructor/courses/${courseId}/quizzes`, data),
  updateQuiz: (id: string, data: SaveQuizInput) => put<QuizEdit>(`/api/instructor/quizzes/${id}`, data),
  deleteQuiz: (id: string) => del<void>(`/api/instructor/quizzes/${id}`),
  reviews: (page = 1, courseId?: string) => get<PagedResult<InstructorReview>>('/api/instructor/reviews', { page, pageSize: 15, courseId }),
  reply: (reviewId: string, reply: string) => post<void>(`/api/instructor/reviews/${reviewId}/reply`, { reply }),
}

export const adminService = {
  overview: () => get<AdminOverview>('/api/admin/overview'),
  users: (params: { keyword?: string; role?: string; page?: number }) => get<PagedResult<AdminUser>>('/api/admin/users', { ...params, pageSize: 15 }),
  setRoles: (id: string, roles: string[]) => put<AdminUser>(`/api/admin/users/${id}/roles`, { roles }),
  setActive: (id: string, isActive: boolean) => put<AdminUser>(`/api/admin/users/${id}/active`, { isActive }),
  setInstructorVerified: (id: string, isVerified: boolean) => put<AdminUser>(`/api/admin/users/${id}/instructor-verified`, { isVerified }),
  courses: (params: { status?: CourseStatus; keyword?: string; page?: number }) => get<PagedResult<AdminCourse>>('/api/admin/courses', { ...params, pageSize: 15 }),
  approve: (id: string) => post<void>(`/api/admin/courses/${id}/approve`),
  reject: (id: string, reason: string) => post<void>(`/api/admin/courses/${id}/reject`, { reason }),
  unpublish: (id: string) => post<void>(`/api/admin/courses/${id}/unpublish`),
  setFeatured: (id: string, isFeatured: boolean) => put<void>(`/api/admin/courses/${id}/featured`, { isFeatured }),
  categories: () => get<Category[]>('/api/admin/categories'),
  saveCategory: (id: string | null, data: { name: string; slug: string | null; description: string | null; parentId: string | null; displayOrder: number; isActive: boolean }) =>
    id ? put<Category>(`/api/admin/categories/${id}`, data) : post<Category>('/api/admin/categories', data),
  deleteCategory: (id: string) => del<void>(`/api/admin/categories/${id}`),
  orders: (params: { status?: OrderStatus; keyword?: string; page?: number }) => get<PagedResult<AdminOrder>>('/api/admin/orders', { ...params, pageSize: 15 }),
  coupons: (params: { keyword?: string; page?: number }) => get<PagedResult<Coupon>>('/api/admin/coupons', { ...params, pageSize: 15 }),
  saveCoupon: (id: string | null, data: SaveCouponInput) => (id ? put<Coupon>(`/api/admin/coupons/${id}`, data) : post<Coupon>('/api/admin/coupons', data)),
  deleteCoupon: (id: string) => del<void>(`/api/admin/coupons/${id}`),
}
