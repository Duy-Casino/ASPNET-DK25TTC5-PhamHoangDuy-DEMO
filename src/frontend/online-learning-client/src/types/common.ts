// Kiểu dùng chung, khớp với OnlineLearning.Application.Common.Models

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type CourseLevel = 'Beginner' | 'Intermediate' | 'Advanced'
export type CourseStatus = 'Draft' | 'Pending' | 'Published' | 'Rejected'
export type OrderStatus = 'Pending' | 'Paid' | 'Cancelled' | 'Expired' | 'Refunded'
export type UserRole = 'Admin' | 'Instructor' | 'Student'

export const courseLevelLabel: Record<CourseLevel, string> = {
  Beginner: 'Cơ bản',
  Intermediate: 'Trung cấp',
  Advanced: 'Nâng cao',
}

export const courseStatusLabel: Record<CourseStatus, string> = {
  Draft: 'Bản nháp',
  Pending: 'Chờ duyệt',
  Published: 'Đã xuất bản',
  Rejected: 'Bị từ chối',
}
