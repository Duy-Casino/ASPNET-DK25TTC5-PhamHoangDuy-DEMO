import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { toast } from 'sonner'
import { cartService } from '../services/api'
import { getErrorMessage } from '../services/apiClient'
import type { Cart } from '../types/api'
import { useAuth } from '../hooks/useAuth'

const emptyCart: Cart = { items: [], count: 0, subTotal: 0, originalTotal: 0 }

export interface CartContextValue {
  cart: Cart
  loading: boolean
  /** courseId đang được thêm/xóa, để nút hiển thị trạng thái chờ */
  pendingId: string | null
  has: (courseId: string) => boolean
  add: (courseId: string, title?: string) => Promise<boolean>
  remove: (courseId: string) => Promise<void>
  reload: () => Promise<void>
}

export const CartContext = createContext<CartContextValue | null>(null)

export function CartProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const [cart, setCart] = useState<Cart>(emptyCart)
  const [loading, setLoading] = useState(false)
  const [pendingId, setPendingId] = useState<string | null>(null)

  const reload = useCallback(async () => {
    if (!isAuthenticated) {
      setCart(emptyCart)
      return
    }
    setLoading(true)
    try {
      setCart(await cartService.get())
    } catch {
      // Giữ giỏ cũ nếu lỗi mạng
    } finally {
      setLoading(false)
    }
  }, [isAuthenticated])

  useEffect(() => {
    void reload()
  }, [reload])

  const add = useCallback(async (courseId: string, title?: string) => {
    setPendingId(courseId)
    try {
      setCart(await cartService.add(courseId))
      toast.success(title ? `Đã thêm "${title}" vào giỏ hàng` : 'Đã thêm vào giỏ hàng')
      return true
    } catch (e) {
      toast.error(getErrorMessage(e))
      return false
    } finally {
      setPendingId(null)
    }
  }, [])

  const remove = useCallback(async (courseId: string) => {
    setPendingId(courseId)
    try {
      setCart(await cartService.remove(courseId))
    } catch (e) {
      toast.error(getErrorMessage(e))
    } finally {
      setPendingId(null)
    }
  }, [])

  const value = useMemo<CartContextValue>(() => ({
    cart, loading, pendingId, add, remove, reload,
    has: (id) => cart.items.some((i) => i.courseId === id),
  }), [cart, loading, pendingId, add, remove, reload])

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>
}
