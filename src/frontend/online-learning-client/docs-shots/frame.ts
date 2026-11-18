import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Page } from '@playwright/test'

/** Thư mục ảnh của báo cáo đồ án. */
export const OUT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../thesis/doc/hinh')
fs.mkdirSync(OUT, { recursive: true })

/** Chụp màn hình kèm thanh địa chỉ (URL thật) và thời điểm chụp theo giờ Việt Nam. */
export async function frame(page: Page, file: string, width = 1366) {
  const png = await page.screenshot()
  const url = page.url().replace(/^https?:\/\//, '')
  const title = (await page.title()).replace(/</g, '&lt;')
  const now = new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
  const f = await page.context().newPage()
  await f.setViewportSize({ width, height: 900 })
  await f.setContent(`<div id="w" style="width:${width}px;font-family:'Segoe UI',Arial,sans-serif">
    <div style="background:#dee1e6;padding:6px 10px 0"><div style="display:inline-block;background:#fff;border-radius:8px 8px 0 0;padding:6px 16px;font-size:13px;max-width:60%;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${title}</div></div>
    <div style="background:#fff;border-bottom:1px solid #c8ccd2;padding:6px 12px;display:flex;gap:12px;align-items:center;font-size:14px;color:#333">
      <span style="letter-spacing:6px">&#8592;&#8594;&#10227;</span>
      <div style="flex:1;background:#f1f3f4;border-radius:16px;padding:5px 14px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${url}</div>
      <span style="white-space:nowrap">${now}</span></div>
    <img style="display:block;width:${width}px" src="data:image/png;base64,${png.toString('base64')}"></div>`)
  await f.locator('#w').screenshot({ path: path.join(OUT, `anh-${file}.png`) })
  await f.close()
}

export async function settle(page: Page) {
  await page.waitForLoadState('networkidle').catch(() => {})
  await page.waitForTimeout(600)
}
