import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authService } from '../services/api'
import { refreshSession, tokenStore } from '../services/apiClient'
import type { AuthResponse, User } from '../types/api'
import type { UserRole } from '../types/common'

export interface AuthContextValue {
  user: User | null
  /** true khi đang khôi phục phiên lúc tải trang */
  initializing: boolean
  isAuthenticated: boolean
  hasRole: (...roles: UserRole[]) => boolean
  login: (email: string, password: string) => Promise<User>
  register: (fullName: string, email: string, password: string, referralCode?: string | null) => Promise<User>
  logout: () => Promise<void>
  setUser: (user: User) => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [initializing, setInitializing] = useState(true)

  const apply = useCallback((auth: AuthResponse | null) => {
    tokenStore.set(auth?.accessToken ?? null)
    setUser(auth?.user ?? null)
  }, [])

  // Khôi phục phiên từ cookie refresh token khi tải trang
  useEffect(() => {
    const unsubscribe = tokenStore.subscribe(apply)
    refreshSession().finally(() => setInitializing(false))
    return () => {
      unsubscribe()
    }
  }, [apply])

  // Làm mới access token trước khi hết hạn (15 phút)
  useEffect(() => {
    if (!user) return
    const id = window.setInterval(() => void refreshSession(), 12 * 60 * 1000)
    return () => window.clearInterval(id)
  }, [user])

  const login = useCallback(async (email: string, password: string) => {
    const auth = await authService.login(email, password)
    apply(auth)
    return auth.user
  }, [apply])

  const register = useCallback(async (fullName: string, email: string, password: string, referralCode?: string | null) => {
    const auth = await authService.register(fullName, email, password, referralCode)
    apply(auth)
    return auth.user
  }, [apply])

  const logout = useCallback(async () => {
    try {
      await authService.logout()
    } finally {
      apply(null)
    }
  }, [apply])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    initializing,
    isAuthenticated: user !== null,
    hasRole: (...roles) => !!user && roles.some((r) => user.roles.includes(r)),
    login,
    register,
    logout,
    setUser,
  }), [user, initializing, login, register, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
