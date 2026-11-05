// Kiểu dữ liệu cho học tập, bài kiểm tra và các dashboard
import type { CourseLevel, CourseStatus, OrderStatus } from './common'

export type EnrollmentStatus = 'Active' | 'Completed' | 'Revoked'
export type LessonType = 'Video' | 'Article'
export type QuestionType = 'SingleChoice' | 'MultipleChoice' | 'TrueFalse'

// ---------- Học viên ----------
export interface MyCourse {
  courseId: string
  title: string
  slug: string
  thumbnailUrl: string | null
  instructorName: string
  progressPercent: number
  completedLessons: number
  totalLessons: number
  status: EnrollmentStatus
  lastLessonId: string | null
  lastLessonTitle: string | null
  enrolledAt: string
  lastAccessedAt: string | null
  completedAt: string | null
  hasCertificate: boolean
}

export interface StudentSummary {
  totalCourses: number
  inProgress: number
  completed: number
  notStarted: number
  certificates: number
  quizzesPassed: number
  totalSpent: number
  completedLessons: number
  learningSecondsCompleted: number
}

export interface LearnLesson { id: string; title: string; type: LessonType; durationSeconds: number; isPreview: boolean; isRequired: boolean; isCompleted: boolean }
export interface LearnQuizBrief { id: string; title: string; isRequired: boolean; isFinalExam: boolean; passingScore: number; bestScore: number | null; isPassed: boolean; attemptsUsed: number; maxAttempts: number | null }
export interface LearnSection { id: string; title: string; lessons: LearnLesson[]; quizzes: LearnQuizBrief[] }
export interface LearnCourse {
  courseId: string
  title: string
  slug: string
  instructorName: string
  progressPercent: number
  completedLessons: number
  requiredLessons: number
  status: EnrollmentStatus
  resumeLessonId: string | null
  isPreviewMode: boolean
  canGetCertificate: boolean
  hasCertificate: boolean
  sections: LearnSection[]
  courseQuizzes: LearnQuizBrief[]
}

export interface LessonContent {
  id: string
  sectionId: string
  sectionTitle: string
  title: string
  type: LessonType
  content: string | null
  videoUrl: string | null
  durationSeconds: number
  isCompleted: boolean
  lastPositionSeconds: number
  resources: { id: string; fileName: string; fileUrl: string; contentType: string | null; fileSize: number }[]
  previousLessonId: string | null
  nextLessonId: string | null
}

export interface Progress { progressPercent: number; completedLessons: number; requiredLessons: number; status: EnrollmentStatus; courseCompleted: boolean; certificateCode: string | null }

export interface AttemptSummary { id: string; attemptNumber: number; startedAt: string; submittedAt: string | null; scorePercent: number; isPassed: boolean }
export interface QuizIntro {
  id: string
  courseId: string
  courseSlug: string
  title: string
  description: string | null
  passingScore: number
  maxAttempts: number | null
  timeLimitMinutes: number | null
  questionCount: number
  totalPoints: number
  isRequired: boolean
  attemptsUsed: number
  bestScore: number | null
  isPassed: boolean
  canAttempt: boolean
  inProgressAttemptId: string | null
  attempts: AttemptSummary[]
}
export interface AttemptTake {
  attemptId: string
  quizId: string
  quizTitle: string
  attemptNumber: number
  startedAt: string
  deadline: string | null
  questions: { id: string; content: string; type: QuestionType; points: number; options: { id: string; content: string }[] }[]
}
export interface AttemptResult {
  attemptId: string
  quizId: string
  quizTitle: string
  courseId: string
  courseSlug: string
  attemptNumber: number
  startedAt: string
  submittedAt: string
  score: number
  maxScore: number
  scorePercent: number
  passingScore: number
  isPassed: boolean
  courseCompleted: boolean
  certificateCode: string | null
  questions: {
    questionId: string; content: string; type: QuestionType; points: number; pointsAwarded: number; isCorrect: boolean
    explanation: string | null; options: { id: string; content: string; isCorrect: boolean; selected: boolean }[]
  }[]
}
export interface MyQuizResult { attemptId: string; quizId: string; quizTitle: string; courseTitle: string; courseSlug: string; attemptNumber: number; submittedAt: string; scorePercent: number; passingScore: number; isPassed: boolean }
export interface CertificateItem { id: string; certificateCode: string; courseTitle: string; courseSlug: string; issuedAt: string; finalScore: number | null; pdfUrl: string | null }

// ---------- Giảng viên ----------
export interface InstructorOverview {
  totalCourses: number
  publishedCourses: number
  pendingCourses: number
  totalStudents: number
  enrollmentsLast30Days: number
  totalRevenue: number
  revenueLast30Days: number
  averageRating: number
  totalReviews: number
  monthly: { month: string; enrollments: number; revenue: number }[]
  courses: { courseId: string; title: string; status: CourseStatus; enrollments: number; revenue: number; averageRating: number; reviewCount: number; averageProgress: number }[]
}
export interface InstructorStudent { userId: string; fullName: string; email: string; avatarUrl: string | null; courseId: string; courseTitle: string; progressPercent: number; status: EnrollmentStatus; enrolledAt: string; lastAccessedAt: string | null }
export interface InstructorCourseListItem {
  id: string; title: string; slug: string; thumbnailUrl: string | null; status: CourseStatus; rejectionReason: string | null
  price: number; discountPrice: number | null; totalLessons: number; enrollmentCount: number; averageRating: number; reviewCount: number
  createdAt: string; updatedAt: string | null; submittedAt: string | null; publishedAt: string | null
}
export interface LessonEdit {
  id: string; sectionId: string; title: string; type: LessonType; content: string | null; videoUrl: string | null; videoPublicId: string | null
  durationSeconds: number; sortOrder: number; isPreview: boolean; isRequired: boolean
  resources: { id: string; fileName: string; fileUrl: string; contentType: string | null; fileSize: number }[]
}
export interface SectionEdit { id: string; title: string; description: string | null; sortOrder: number; lessons: LessonEdit[] }
export interface CourseEdit {
  id: string; title: string; slug: string; categoryId: string; shortDescription: string | null; description: string | null
  requirements: string[]; outcomes: string[]; thumbnailUrl: string | null; previewVideoUrl: string | null; language: string
  level: CourseLevel; price: number; discountPrice: number | null; status: CourseStatus; rejectionReason: string | null
  isEditable: boolean; instructorId: string; sections: SectionEdit[]
}
export interface SaveCourseInput {
  title: string; categoryId: string; shortDescription: string | null; description: string | null; requirements: string[]; outcomes: string[]
  thumbnailUrl: string | null; previewVideoUrl: string | null; language: string; level: CourseLevel; price: number; discountPrice: number | null
}
export interface SaveLessonInput { title: string; type: LessonType; content: string | null; videoUrl: string | null; videoPublicId: string | null; durationSeconds: number; isPreview: boolean; isRequired: boolean }
export interface QuizListItem { id: string; title: string; sectionId: string | null; sectionTitle: string | null; questionCount: number; passingScore: number; maxAttempts: number | null; isRequired: boolean; isFinalExam: boolean; attemptCount: number }
export interface QuizEdit {
  id: string; courseId: string; sectionId: string | null; title: string; description: string | null; passingScore: number; maxAttempts: number | null
  timeLimitMinutes: number | null; isRequired: boolean; isFinalExam: boolean; shuffleQuestions: boolean; attemptCount: number
  questions: { id: string; content: string; type: QuestionType; points: number; explanation: string | null; sortOrder: number; options: { id: string; content: string; isCorrect: boolean; sortOrder: number }[] }[]
}
export interface SaveQuizInput {
  title: string; description: string | null; sectionId: string | null; passingScore: number; maxAttempts: number | null; timeLimitMinutes: number | null
  isRequired: boolean; isFinalExam: boolean; shuffleQuestions: boolean
  questions: { content: string; type: QuestionType; points: number; explanation: string | null; options: { content: string; isCorrect: boolean }[] }[]
}
export interface InstructorReview { id: string; courseId: string; courseTitle: string; userFullName: string; userAvatarUrl: string | null; rating: number; comment: string | null; instructorReply: string | null; repliedAt: string | null; isHidden: boolean; createdAt: string }

// ---------- Admin ----------
export interface AdminOverview {
  totalUsers: number; totalStudents: number; totalInstructors: number; newUsersLast30Days: number
  publishedCourses: number; pendingCourses: number; draftCourses: number; rejectedCourses: number
  paidOrders: number; pendingOrders: number; totalRevenue: number; revenueLast30Days: number
  totalEnrollments: number; totalCertificates: number
  monthly: { month: string; revenue: number; orders: number; newUsers: number }[]
}
export interface AdminUser { id: string; email: string; fullName: string; avatarUrl: string | null; phoneNumber: string | null; isActive: boolean; emailConfirmed: boolean; roles: string[]; enrollmentCount: number; courseCount: number; createdAt: string }
export interface AdminCourse { id: string; title: string; slug: string; thumbnailUrl: string | null; status: CourseStatus; instructorName: string; categoryName: string; price: number; discountPrice: number | null; isFeatured: boolean; isDeleted: boolean; enrollmentCount: number; totalLessons: number; createdAt: string; submittedAt: string | null; publishedAt: string | null }
export interface AdminOrder { id: string; orderCode: string; status: OrderStatus; userFullName: string; userEmail: string; subTotal: number; discountAmount: number; totalAmount: number; couponCode: string | null; itemCount: number; createdAt: string; paidAt: string | null }
export interface Coupon {
  id: string; code: string; description: string | null; discountType: 'Percentage' | 'FixedAmount'; discountValue: number; maxDiscountAmount: number | null
  minOrderAmount: number; usageLimit: number | null; usedCount: number; perUserLimit: number; courseId: string | null; courseTitle: string | null
  startsAt: string; expiresAt: string; isActive: boolean; createdAt: string
}
export type SaveCouponInput = Omit<Coupon, 'id' | 'usedCount' | 'courseTitle' | 'createdAt'>
