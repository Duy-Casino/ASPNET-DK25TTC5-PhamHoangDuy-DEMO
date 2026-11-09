import { useContext } from 'react'
import { AuthContext } from '../contexts/AuthContext'
import { CartContext } from '../contexts/CartContext'

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth phải nằm trong AuthProvider')
  return ctx
}

export function useCart() {
  const ctx = useContext(CartContext)
  if (!ctx) throw new Error('useCart phải nằm trong CartProvider')
  return ctx
}
