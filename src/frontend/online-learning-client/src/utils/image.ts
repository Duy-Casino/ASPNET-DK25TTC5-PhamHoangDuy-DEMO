/**
 * Ảnh đáp ứng: sinh src/srcSet theo chiều rộng cho các nguồn hỗ trợ đổi kích thước qua URL.
 *  - Cloudinary: chèn biến đổi w_{n},c_fill,f_auto,q_auto (tự chọn WebP/AVIF, nén hợp lý).
 *  - picsum.photos (ảnh minh họa dữ liệu mẫu): /seed/{id}/{w}/{h} → đổi w/h theo tỉ lệ.
 * Nguồn khác (ảnh tải lên lưu cục bộ…) giữ nguyên URL, không có srcSet.
 */
const WIDTHS = [320, 480, 640, 960, 1280]

function cloudinary(url: string, w: number, ratio?: number): string | null {
  const m = url.match(/^(https?:\/\/res\.cloudinary\.com\/[^/]+\/image\/upload\/)(.*)$/)
  if (!m) return null
  const h = ratio ? `,h_${Math.round(w / ratio)}` : ''
  return `${m[1]}w_${w}${h},c_fill,f_auto,q_auto/${m[2]}`
}

function picsum(url: string, w: number): string | null {
  const m = url.match(/^(https?:\/\/picsum\.photos\/(?:seed\/[^/]+|id\/\d+))\/(\d+)\/(\d+)(.*)$/)
  if (!m) return null
  const ratio = Number(m[2]) / Number(m[3])
  return `${m[1]}/${w}/${Math.round(w / ratio)}${m[4]}`
}

export interface ResponsiveImage {
  src: string
  srcSet?: string
}

/** `maxWidth`: chiều rộng lớn nhất cần (px CSS × mật độ điểm ảnh); `ratio`: tỉ lệ khung (rộng/cao) cho Cloudinary. */
export function responsiveImage(url: string | null | undefined, maxWidth = 960, ratio = 16 / 9): ResponsiveImage | null {
  if (!url) return null
  const build = (w: number) => cloudinary(url, w, ratio) ?? picsum(url, w)
  const widths = WIDTHS.filter((w) => w <= maxWidth)
  if (widths.length === 0 || build(widths[0]) === null) return { src: url }
  return {
    src: build(widths[Math.min(1, widths.length - 1)])!,
    srcSet: widths.map((w) => `${build(w)} ${w}w`).join(', '),
  }
}
