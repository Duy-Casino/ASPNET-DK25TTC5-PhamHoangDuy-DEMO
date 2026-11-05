const vnd = new Intl.NumberFormat('vi-VN')

/** 499000 → "499.000đ"; 0 → "Miễn phí" */
export function formatPrice(value: number, freeLabel = 'Miễn phí'): string {
  return value === 0 ? freeLabel : `${vnd.format(value)}đ`
}

export function formatNumber(value: number): string {
  return vnd.format(value)
}

/** 4530 giây → "1 giờ 15 phút" */
export function formatDuration(totalSeconds: number): string {
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.round((totalSeconds % 3600) / 60)
  if (hours === 0) return `${Math.max(minutes, 1)} phút`
  return minutes === 0 ? `${hours} giờ` : `${hours} giờ ${minutes} phút`
}

/** 425 giây → "7:05" */
export function formatClock(totalSeconds: number): string {
  const m = Math.floor(totalSeconds / 60)
  const s = totalSeconds % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}

const dateFmt = new Intl.DateTimeFormat('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'Asia/Ho_Chi_Minh' })
const dateTimeFmt = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Ho_Chi_Minh',
})

export const formatDate = (iso: string) => dateFmt.format(new Date(iso))
export const formatDateTime = (iso: string) => dateTimeFmt.format(new Date(iso))

export function initials(name: string): string {
  const parts = name.trim().split(/\s+/)
  const letters = parts.length === 1 ? parts[0].slice(0, 2) : parts[0][0] + parts[parts.length - 1][0]
  return letters.toUpperCase()
}

export function discountPercent(price: number, discountPrice: number | null): number | null {
  if (discountPrice === null || discountPrice >= price || price === 0) return null
  return Math.round(((price - discountPrice) / price) * 100)
}
