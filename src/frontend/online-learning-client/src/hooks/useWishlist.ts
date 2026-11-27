import { useCallback, useEffect, useSyncExternalStore } from 'react'
import { toast } from 'sonner'
import { getErrorMessage } from '../services/apiClient'
import { wishlistService } from '../services/growthApi'
import { useAuth } from './useAuth'

/**
 * Danh sách yêu thích dùng chung cho mọi nút trái tim trên trang: tải một lần sau khi đăng nhập,
 * cập nhật lạc quan khi bấm và hoàn tác nếu API lỗi.
 */
let ids = new Set<string>()
let loadedFor: string | null = null
const listeners = new Set<() => void>()
const emit = () => listeners.forEach((l) => l())
const subscribe = (l: () => void) => { listeners.add(l); return () => listeners.delete(l) }
const snapshot = () => ids

export function useWishlist() {
  const { user } = useAuth()
  const current = useSyncExternalStore(subscribe, snapshot)

  useEffect(() => {
    if (!user) {
      if (loadedFor) { ids = new Set(); loadedFor = null; emit() }
      return
    }
    if (loadedFor === user.id) return
    loadedFor = user.id
    wishlistService.ids().then((list) => { ids = new Set(list); emit() }).catch(() => { loadedFor = null })
  }, [user])

  const toggle = useCallback(async (courseId: string, title?: string) => {
    const had = ids.has(courseId)
    ids = new Set(ids)
    if (had) ids.delete(courseId)
    else ids.add(courseId)
    emit()
    try {
      if (had) await wishlistService.remove(courseId)
      else await wishlistService.add(courseId)
      if (!had) toast.success(title ? `Đã thêm "${title}" vào yêu thích` : 'Đã thêm vào yêu thích')
    } catch (err) {
      ids = new Set(ids)
      if (had) ids.add(courseId)
      else ids.delete(courseId)
      emit()
      toast.error(getErrorMessage(err))
    }
  }, [])

  return { has: (id: string) => current.has(id), toggle, count: current.size, enabled: !!user }
}
