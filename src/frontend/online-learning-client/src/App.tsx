import { lazy, Suspense } from 'react'
import { Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from './components/ProtectedRoute'
import { Spinner } from './components/ui'
import AuthLayout from './layouts/AuthLayout'
import MainLayout from './layouts/MainLayout'
import HomePage from './pages/HomePage'

// Tách bundle theo trang để trang chủ tải nhanh
const CoursesPage = lazy(() => import('./pages/CoursesPage'))
const CourseDetailPage = lazy(() => import('./pages/CourseDetailPage'))
const InstructorPage = lazy(() => import('./pages/InstructorPage'))
const LoginPage = lazy(() => import('./pages/auth/LoginPage'))
const RegisterPage = lazy(() => import('./pages/auth/RegisterPage'))
const ForgotPasswordPage = lazy(() => import('./pages/auth/ForgotPasswordPage'))
const ResetPasswordPage = lazy(() => import('./pages/auth/ResetPasswordPage'))
const ConfirmEmailPage = lazy(() => import('./pages/auth/ConfirmEmailPage'))
const CartPage = lazy(() => import('./pages/checkout/CartPage'))
const CheckoutPage = lazy(() => import('./pages/checkout/CheckoutPage'))
const OrderDetailPage = lazy(() => import('./pages/checkout/OrderDetailPage'))
const PaymentResultPage = lazy(() => import('./pages/checkout/PaymentResultPage'))
const ProfilePage = lazy(() => import('./pages/account/ProfilePage'))
const OrdersPage = lazy(() => import('./pages/account/OrdersPage'))
const VerifyCertificatePage = lazy(() => import('./pages/VerifyCertificatePage'))
const AdminCertificatesPage = lazy(() => import('./pages/admin/AdminCertificatesPage'))
const AdminRewardsPage = lazy(() => import('./pages/admin/AdminRewardsPage').then((m) => ({ default: m.AdminRewardsPage })))
const RewardsPage = lazy(() => import('./pages/student/RewardsPage').then((m) => ({ default: m.RewardsPage })))
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'))
const info = () => import('./pages/info/InfoPages')
const AboutPage = lazy(() => info().then((m) => ({ default: m.AboutPage })))
const ContactPage = lazy(() => info().then((m) => ({ default: m.ContactPage })))
const PolicyPage = lazy(() => info().then((m) => ({ default: m.PolicyPage })))

// Học tập
const LearnPage = lazy(() => import('./pages/learn/LearnPage'))
const LearnIndex = lazy(() => import('./pages/learn/LearnPage').then((m) => ({ default: m.LearnIndex })))
const LessonView = lazy(() => import('./pages/learn/LessonView'))
const QuizView = lazy(() => import('./pages/learn/QuizView'))

// Dashboard
const DashboardLayout = lazy(() => import('./layouts/DashboardLayout'))
const student = () => import('./pages/student/StudentPages')
const MyCoursesPage = lazy(() => student().then((m) => ({ default: m.MyCoursesPage })))
const QuizResultsPage = lazy(() => student().then((m) => ({ default: m.QuizResultsPage })))
const CertificatesPage = lazy(() => student().then((m) => ({ default: m.CertificatesPage })))
const instructor = () => import('./pages/instructor/InstructorPages')
const InstructorOverviewPage = lazy(() => instructor().then((m) => ({ default: m.InstructorOverviewPage })))
const InstructorCoursesPage = lazy(() => instructor().then((m) => ({ default: m.InstructorCoursesPage })))
const InstructorStudentsPage = lazy(() => instructor().then((m) => ({ default: m.InstructorStudentsPage })))
const InstructorReviewsPage = lazy(() => instructor().then((m) => ({ default: m.InstructorReviewsPage })))
const InstructorProfilePage = lazy(() => import('./pages/instructor/InstructorProfilePage').then((m) => ({ default: m.InstructorProfilePage })))
const MessagesPage = lazy(() => import('./pages/messages/MessagesPage').then((m) => ({ default: m.MessagesPage })))
const CourseEditorPage = lazy(() => import('./pages/instructor/CourseEditorPage'))
const admin = () => import('./pages/admin/AdminPages')
const AdminOverviewPage = lazy(() => admin().then((m) => ({ default: m.AdminOverviewPage })))
const AdminCoursesPage = lazy(() => admin().then((m) => ({ default: m.AdminCoursesPage })))
const AdminUsersPage = lazy(() => admin().then((m) => ({ default: m.AdminUsersPage })))
const AdminCategoriesPage = lazy(() => admin().then((m) => ({ default: m.AdminCategoriesPage })))
const AdminOrdersPage = lazy(() => admin().then((m) => ({ default: m.AdminOrdersPage })))
const AdminCouponsPage = lazy(() => admin().then((m) => ({ default: m.AdminCouponsPage })))
const AdminPaymentsPage = lazy(() => import('./pages/admin/AdminPaymentsPage'))

function PageLoader() {
  return (
    <div className="flex min-h-[50vh] items-center justify-center" role="status">
      <Spinner className="size-8 text-brand-600" />
      <span className="sr-only">Đang tải trang…</span>
    </div>
  )
}

export default function App() {
  return (
    <Suspense fallback={<PageLoader />}>
      <Routes>
        <Route element={<MainLayout />}>
          <Route index element={<HomePage />} />
          <Route path="khoa-hoc" element={<CoursesPage />} />
          <Route path="khoa-hoc/:slug" element={<CourseDetailPage />} />
          <Route path="giang-vien/:id" element={<InstructorPage />} />
          <Route path="gio-hang" element={<ProtectedRoute><CartPage /></ProtectedRoute>} />
          <Route path="thanh-toan" element={<ProtectedRoute><CheckoutPage /></ProtectedRoute>} />
          <Route path="thanh-toan/ket-qua" element={<ProtectedRoute><PaymentResultPage /></ProtectedRoute>} />
          <Route path="don-hang/:id" element={<ProtectedRoute><OrderDetailPage /></ProtectedRoute>} />
          <Route path="tai-khoan" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
          <Route path="tai-khoan/don-hang" element={<ProtectedRoute><OrdersPage /></ProtectedRoute>} />
          <Route path="xac-nhan-email" element={<ConfirmEmailPage />} />
          <Route path="xac-minh" element={<VerifyCertificatePage />} />
          <Route path="xac-minh/:code" element={<VerifyCertificatePage />} />
          <Route path="gioi-thieu" element={<AboutPage />} />
          <Route path="lien-he" element={<ContactPage />} />
          <Route path="chinh-sach/:slug" element={<PolicyPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>

        <Route element={<AuthLayout />}>
          <Route path="dang-nhap" element={<LoginPage />} />
          <Route path="dang-ky" element={<RegisterPage />} />
          <Route path="quen-mat-khau" element={<ForgotPasswordPage />} />
          <Route path="dat-lai-mat-khau" element={<ResetPasswordPage />} />
        </Route>

        {/* Trình học (toàn màn hình) */}
        <Route path="hoc/:slug" element={<ProtectedRoute><LearnPage /></ProtectedRoute>}>
          <Route index element={<LearnIndex />} />
          <Route path="bai/:lessonId" element={<LessonView />} />
          <Route path="kiem-tra/:quizId" element={<QuizView />} />
        </Route>

        <Route path="hoc-vien" element={<ProtectedRoute><DashboardLayout area="student" /></ProtectedRoute>}>
          <Route index element={<MyCoursesPage />} />
          <Route path="ket-qua" element={<QuizResultsPage />} />
          <Route path="chung-chi" element={<CertificatesPage />} />
          <Route path="diem-thuong" element={<RewardsPage />} />
          <Route path="tin-nhan" element={<MessagesPage area="student" />} />
          <Route path="tin-nhan/:id" element={<MessagesPage area="student" />} />
        </Route>

        <Route path="giang-day" element={<ProtectedRoute roles={['Instructor', 'Admin']}><DashboardLayout area="instructor" /></ProtectedRoute>}>
          <Route index element={<InstructorOverviewPage />} />
          <Route path="khoa-hoc" element={<InstructorCoursesPage />} />
          <Route path="khoa-hoc/:id" element={<CourseEditorPage />} />
          <Route path="hoc-vien" element={<InstructorStudentsPage />} />
          <Route path="danh-gia" element={<InstructorReviewsPage />} />
          <Route path="ho-so" element={<InstructorProfilePage />} />
          <Route path="tin-nhan" element={<MessagesPage area="instructor" />} />
          <Route path="tin-nhan/:id" element={<MessagesPage area="instructor" />} />
        </Route>

        <Route path="quan-tri" element={<ProtectedRoute roles={['Admin']}><DashboardLayout area="admin" /></ProtectedRoute>}>
          <Route index element={<AdminOverviewPage />} />
          <Route path="khoa-hoc" element={<AdminCoursesPage />} />
          <Route path="nguoi-dung" element={<AdminUsersPage />} />
          <Route path="danh-muc" element={<AdminCategoriesPage />} />
          <Route path="don-hang" element={<AdminOrdersPage />} />
          <Route path="ma-giam-gia" element={<AdminCouponsPage />} />
          <Route path="giao-dich" element={<AdminPaymentsPage />} />
          <Route path="chung-chi" element={<AdminCertificatesPage />} />
          <Route path="diem-thuong" element={<AdminRewardsPage />} />
        </Route>
      </Routes>
    </Suspense>
  )
}
