import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import type { UserRole } from '../types/common'
import { Spinner } from './ui'

/** Chặn trang cần đăng nhập (và vai trò, nếu có); chuyển tới /dang-nhap kèm đường quay lại. */
export function ProtectedRoute({ children, roles }: { children: ReactNode; roles?: UserRole[] }) {
  const { initializing, isAuthenticated, hasRole } = useAuth()
  const location = useLocation()

  if (initializing) {
    return (
      <div className="container-page flex min-h-[50vh] items-center justify-center" role="status">
        <Spinner className="size-8 text-brand-600" />
        <span className="sr-only">Đang tải…</span>
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to={`/dang-nhap?next=${encodeURIComponent(location.pathname + location.search)}`} replace />
  }

  if (roles && !hasRole(...roles)) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}
