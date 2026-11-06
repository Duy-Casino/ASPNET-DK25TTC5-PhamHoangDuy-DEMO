import { useEffect } from 'react'
import { site } from '../config/site'

interface SeoOptions {
  title?: string
  description?: string | null
  /** Ảnh chia sẻ (URL tuyệt đối hoặc bắt đầu bằng /) */
  image?: string | null
  /** Đường dẫn chuẩn, mặc định là đường dẫn hiện tại không kèm query */
  path?: string
  /** Dữ liệu có cấu trúc schema.org (JSON-LD) */
  jsonLd?: Record<string, unknown> | null
  noindex?: boolean
}

function absolute(url: string): string {
  return /^https?:\/\//i.test(url) ? url : `${window.location.origin}${url.startsWith('/') ? '' : '/'}${url}`
}

function setMeta(attr: 'name' | 'property', key: string, value: string | null) {
  let el = document.head.querySelector<HTMLMetaElement>(`meta[${attr}="${key}"]`)
  if (value === null) { el?.remove(); return }
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.content = value
}

function setLink(rel: string, href: string) {
  let el = document.head.querySelector<HTMLLinkElement>(`link[rel="${rel}"]`)
  if (!el) {
    el = document.createElement('link')
    el.rel = rel
    document.head.appendChild(el)
  }
  el.href = href
}

/**
 * Cập nhật title, description, canonical, Open Graph/Twitter và JSON-LD cho trang hiện tại.
 * Thẻ mặc định trong index.html phục vụ các trình thu thập không chạy JavaScript (Facebook, Zalo).
 */
export function useSeo({ title, description, image, path, jsonLd, noindex }: SeoOptions) {
  const ld = jsonLd ? JSON.stringify(jsonLd) : null
  useEffect(() => {
    const fullTitle = title ? `${title} · ${site.name}` : `${site.name} — ${site.tagline}`
    const desc = (description ?? '').trim() || site.description
    const url = absolute(path ?? window.location.pathname)
    const img = absolute(image || '/og-image.png')

    document.title = fullTitle
    setMeta('name', 'description', desc)
    setMeta('name', 'robots', noindex ? 'noindex, nofollow' : null)
    setLink('canonical', url)
    setMeta('property', 'og:title', fullTitle)
    setMeta('property', 'og:description', desc)
    setMeta('property', 'og:url', url)
    setMeta('property', 'og:image', img)
    setMeta('name', 'twitter:card', 'summary_large_image')

    let script = document.getElementById('seo-jsonld') as HTMLScriptElement | null
    if (ld) {
      if (!script) {
        script = document.createElement('script')
        script.id = 'seo-jsonld'
        script.type = 'application/ld+json'
        document.head.appendChild(script)
      }
      script.textContent = ld
    } else {
      script?.remove()
    }
  }, [title, description, image, path, ld, noindex])
}
