// Kiểu dữ liệu khớp DTO của OnlineLearning.Application (JSON camelCase, enum dạng chuỗi)
import type { CourseLevel, CourseStatus, OrderStatus, UserRole } from './common'

export interface User {
  id: string
  email: string
  fullName: string
  avatarUrl: string | null
  phoneNumber: string | null
  headline: string | null
  bio: string | null
  roles: UserRole[]
  createdAt: string
}

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: string
  user: User
}

export interface InstructorBrief {
  id: string
  fullName: string
  avatarUrl: string | null
  headline: string | null
}

export interface CategoryBrief {
  id: string
  name: string
  slug: string
}

export interface CourseSummary {
  id: string
  title: string
  slug: string
  shortDescription: string | null
  thumbnailUrl: string | null
  level: CourseLevel
  price: number
  discountPrice: number | null
  effectivePrice: number
  averageRating: number
  reviewCount: number
  enrollmentCount: number
  totalLessons: number
  totalDurationSeconds: number
  isFeatured: boolean
  publishedAt: string | null
  instructor: InstructorBrief
  category: CategoryBrief
}

export interface Category {
  id: string
  name: string
  slug: string
  description: string | null
  iconUrl: string | null
  parentId: string | null
  displayOrder: number
  isActive: boolean
  courseCount: number
  children: Category[]
}

export interface InstructorProfile {
  id: string
  fullName: string
  avatarUrl: string | null
  headline: string | null
  bio: string | null
  totalCourses: number
  totalStudents: number
  totalReviews: number
  averageRating: number
  isVerified: boolean
}

export type ExperienceType = 'Work' | 'Education' | 'Certification'

export interface InstructorExperience {
  id: string
  type: ExperienceType
  title: string
  organization: string | null
  startYear: number | null
  endYear: number | null
  description: string | null
  credentialUrl: string | null
}

export interface InstructorLinks {
  website: string | null
  linkedIn: string | null
  youtube: string | null
  facebook: string | null
}

export interface InstructorReview {
  id: string
  userFullName: string
  userAvatarUrl: string | null
  rating: number
  comment: string | null
  createdAt: string
  courseTitle: string
  courseSlug: string
  instructorReply: string | null
}

/** Hồ sơ công khai đầy đủ của giảng viên (GET /api/instructors/{id}). */
export interface InstructorDetail extends InstructorProfile {
  verifiedAt: string | null
  memberSince: string
  yearsOfExperience: number | null
  expertise: string[]
  links: InstructorLinks
  experiences: InstructorExperience[]
  certificatesIssued: number
  ratingDistribution: Record<string, number>
  recentReviews: InstructorReview[]
  /** null khi chưa đủ số cuộc trao đổi để công bố */
  response: { conversations: number; replyRatePercent: number; medianReplyHours: number | null } | null
}

export interface InstructorProfileEdit {
  fullName: string
  avatarUrl: string | null
  headline: string | null
  bio: string | null
  yearsOfExperience: number | null
  expertise: string[]
  links: InstructorLinks
  experiences: InstructorExperience[]
  isVerified: boolean
  verifiedAt: string | null
}

export interface SaveInstructorProfileInput {
  avatarUrl: string | null
  headline: string | null
  bio: string | null
  yearsOfExperience: number | null
  expertise: string[]
  websiteUrl: string | null
  linkedInUrl: string | null
  youtubeUrl: string | null
  facebookUrl: string | null
  experiences: Omit<InstructorExperience, 'id'>[]
}

export interface Participant { id: string; fullName: string; avatarUrl: string | null; headline: string | null; isVerified: boolean }
export interface ConversationCourse { id: string; title: string; slug: string }
export interface ConversationListItem {
  id: string
  other: Participant
  iAmInstructor: boolean
  course: ConversationCourse | null
  lastMessagePreview: string | null
  lastMessageIsMine: boolean
  lastMessageAt: string
  hasUnread: boolean
}
export interface ChatMessage { id: string; senderId: string; body: string; createdAt: string }
export interface ConversationDetail {
  id: string
  other: Participant
  iAmInstructor: boolean
  course: ConversationCourse | null
  messages: ChatMessage[]
  hasMore: boolean
  otherLastReadAt: string | null
}

export interface Testimonial {
  id: string
  userFullName: string
  userAvatarUrl: string | null
  rating: number
  comment: string
  courseTitle: string
  courseSlug: string
}

export interface HomeData {
  featured: CourseSummary[]
  newest: CourseSummary[]
  bestSelling: CourseSummary[]
  categories: { id: string; name: string; slug: string; iconUrl: string | null; courseCount: number }[]
  featuredInstructors: InstructorProfile[]
  testimonials: Testimonial[]
  stats: { totalCourses: number; totalStudents: number; totalInstructors: number; totalCertificates: number }
}

export interface CurriculumLesson {
  id: string
  title: string
  type: 'Video' | 'Article'
  durationSeconds: number
  isPreview: boolean
  previewVideoUrl: string | null
}

export interface CurriculumSection {
  id: string
  title: string
  description: string | null
  sortOrder: number
  lessonCount: number
  durationSeconds: number
  lessons: CurriculumLesson[]
}

export interface CourseDetail {
  id: string
  title: string
  slug: string
  shortDescription: string | null
  description: string | null
  requirements: string[]
  outcomes: string[]
  thumbnailUrl: string | null
  previewVideoUrl: string | null
  language: string
  level: CourseLevel
  price: number
  discountPrice: number | null
  effectivePrice: number
  status: CourseStatus
  averageRating: number
  reviewCount: number
  enrollmentCount: number
  totalLessons: number
  totalDurationSeconds: number
  quizCount: number
  publishedAt: string | null
  updatedAt: string | null
  category: CategoryBrief
  instructor: InstructorProfile
  sections: CurriculumSection[]
  ratingDistribution: Record<string, number>
  isEnrolled: boolean
  isInCart: boolean
  isOwner: boolean
}

export interface Review {
  id: string
  userId: string
  userFullName: string
  userAvatarUrl: string | null
  rating: number
  comment: string | null
  instructorReply: string | null
  repliedAt: string | null
  createdAt: string
  updatedAt: string | null
}

export interface CartItem {
  courseId: string
  course: CourseSummary
  addedAt: string
}

export interface Cart {
  items: CartItem[]
  count: number
  subTotal: number
  originalTotal: number
}

export interface PriceLine {
  courseId: string
  title: string
  originalPrice: number
  price: number
  discountAllocated: number
  finalPrice: number
}

export interface PriceQuote {
  lines: PriceLine[]
  subTotal: number
  discountAmount: number
  totalAmount: number
  couponCode: string | null
  couponMessage: string | null
}

export interface OrderItem {
  courseId: string
  courseTitle: string
  courseSlug: string | null
  thumbnailUrl: string | null
  originalPrice: number
  price: number
  discountAllocated: number
  finalPrice: number
}

export interface Payment {
  id: string
  provider: 'VNPay' | 'MoMo' | 'Free' | 'Test'
  amount: number
  status: 'Pending' | 'Success' | 'Failed'
  transactionRef: string
  createdAt: string
  paidAt: string | null
}

export interface Order {
  id: string
  orderCode: string
  status: OrderStatus
  subTotal: number
  discountAmount: number
  totalAmount: number
  couponCode: string | null
  note: string | null
  createdAt: string
  expiresAt: string
  paidAt: string | null
  cancelledAt: string | null
  items: OrderItem[]
  payments: Payment[]
}

export interface OrderListItem {
  id: string
  orderCode: string
  status: OrderStatus
  totalAmount: number
  itemCount: number
  firstCourseTitle: string
  createdAt: string
  paidAt: string | null
}

export interface Notification {
  id: string
  title: string
  message: string
  type: string
  link: string | null
  isRead: boolean
  createdAt: string
}

export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  code?: string
  errors?: Record<string, string[]>
}
