import { apiClient } from './apiClient'
import type {
  AuthResponse, Cart, Category, CourseDetail, CourseSummary, HomeData, InstructorProfile, Notification, Order,
  OrderListItem, PriceQuote, Review, User,
} from '../types/api'
import type { CourseLevel, OrderStatus, PagedResult } from '../types/common'

const get = <T,>(url: string, params?: object) => apiClient.get<T>(url, { params }).then((r) => r.data)
const post = <T,>(url: string, body?: unknown) => apiClient.post<T>(url, body).then((r) => r.data)
const put = <T,>(url: string, body?: unknown) => apiClient.put<T>(url, body).then((r) => r.data)
const del = <T,>(url: string) => apiClient.delete<T>(url).then((r) => r.data)

export const authService = {
  login: (email: string, password: string) => post<AuthResponse>('/api/auth/login', { email, password }),
  register: (fullName: string, email: string, password: string) =>
    post<AuthResponse>('/api/auth/register', { fullName, email, password }),
  logout: () => post<void>('/api/auth/logout'),
  forgotPassword: (email: string) => post<void>('/api/auth/forgot-password', { email }),
  resetPassword: (email: string, token: string, newPassword: string) =>
    post<void>('/api/auth/reset-password', { email, token, newPassword }),
  confirmEmail: (userId: string, token: string) => post<void>('/api/auth/confirm-email', { userId, token }),
}

export const accountService = {
  profile: () => get<User>('/api/account/profile'),
  updateProfile: (data: { fullName: string; phoneNumber: string | null; headline: string | null; bio: string | null; avatarUrl: string | null }) =>
    put<User>('/api/account/profile', data),
  changePassword: (currentPassword: string, newPassword: string) =>
    post<void>('/api/account/change-password', { currentPassword, newPassword }),
}

export interface CourseSearchParams {
  keyword?: string
  category?: string
  level?: CourseLevel
  minPrice?: number
  maxPrice?: number
  minRating?: number
  free?: boolean
  sort?: 'newest' | 'popular' | 'rating' | 'price_asc' | 'price_desc'
  page?: number
  pageSize?: number
  instructorId?: string
}

export const catalogService = {
  home: () => get<HomeData>('/api/home'),
  categories: () => get<Category[]>('/api/categories'),
  search: (params: CourseSearchParams) => get<PagedResult<CourseSummary>>('/api/courses', params),
  detail: (slug: string) => get<CourseDetail>(`/api/courses/${encodeURIComponent(slug)}`),
  reviews: (courseId: string, page = 1, pageSize = 6) =>
    get<PagedResult<Review>>(`/api/courses/${courseId}/reviews`, { page, pageSize }),
  instructor: (id: string) => get<InstructorProfile>(`/api/instructors/${id}`),
}

export const reviewService = {
  mine: (courseId: string) => get<Review>(`/api/courses/${courseId}/reviews/me`),
  create: (courseId: string, rating: number, comment: string) =>
    post<Review>(`/api/courses/${courseId}/reviews`, { rating, comment }),
  update: (courseId: string, rating: number, comment: string) =>
    put<Review>(`/api/courses/${courseId}/reviews/me`, { rating, comment }),
}

export const cartService = {
  get: () => get<Cart>('/api/cart'),
  add: (courseId: string) => post<Cart>('/api/cart/items', { courseId }),
  remove: (courseId: string) => del<Cart>(`/api/cart/items/${courseId}`),
}

export const orderService = {
  quote: (courseIds: string[], couponCode?: string) =>
    post<PriceQuote>('/api/orders/quote', { courseIds, couponCode: couponCode || null }),
  create: (input: { courseIds?: string[]; fromCart?: boolean; couponCode?: string; note?: string }) =>
    post<Order>('/api/orders', {
      courseIds: input.courseIds ?? null,
      fromCart: input.fromCart ?? false,
      couponCode: input.couponCode || null,
      note: input.note || null,
    }),
  mine: (page = 1, status?: OrderStatus) => get<PagedResult<OrderListItem>>('/api/orders', { page, pageSize: 10, status }),
  get: (id: string) => get<Order>(`/api/orders/${id}`),
  cancel: (id: string) => post<void>(`/api/orders/${id}/cancel`),
}

export const notificationService = {
  mine: (page = 1) => get<PagedResult<Notification>>('/api/notifications', { page, pageSize: 10 }),
  unreadCount: () => get<{ count: number }>('/api/notifications/unread-count'),
  markRead: (id: string) => post<void>(`/api/notifications/${id}/read`),
  markAllRead: () => post<void>('/api/notifications/read-all'),
}
