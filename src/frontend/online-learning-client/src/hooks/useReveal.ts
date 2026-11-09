import { useEffect, useRef } from 'react'

/**
 * Hiệu ứng xuất hiện khi cuộn tới: các phần tử con có [data-reveal] lần lượt trồi lên (stagger 70ms).
 * Nội dung luôn hiển thị nếu JS lỗi hoặc người dùng bật giảm chuyển động (xem .reveal trong index.css).
 */
export function useReveal<T extends HTMLElement>() {
  const ref = useRef<T>(null)

  useEffect(() => {
    const root = ref.current
    if (!root) return
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return

    const targets = Array.from(root.querySelectorAll<HTMLElement>('[data-reveal]'))
    targets.forEach((el, i) => {
      el.classList.add('reveal')
      el.style.setProperty('--reveal-delay', `${(i % 6) * 70}ms`)
    })

    const io = new IntersectionObserver(
      (entries) => {
        for (const e of entries) {
          if (e.isIntersecting) {
            e.target.classList.add('is-visible')
            io.unobserve(e.target)
          }
        }
      },
      { rootMargin: '0px 0px -8% 0px', threshold: 0.1 },
    )
    targets.forEach((t) => io.observe(t))
    return () => io.disconnect()
  })

  return ref
}
