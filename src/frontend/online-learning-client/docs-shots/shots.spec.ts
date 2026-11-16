import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { expect, test, type Browser, type Page } from '@playwright/test'
import { accounts, login, registerStudent } from '../e2e/helpers'

/**
 * Ảnh màn hình cho Chương 3 (Kết quả) của báo cáo đồ án.
 * Mỗi ảnh có thanh địa chỉ ghi URL thật và thời điểm chụp (giờ Việt Nam), như mẫu báo cáo yêu cầu.
 */
const OUT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../thesis/doc/hinh')
fs.mkdirSync(OUT, { recursive: true })
const failures: string[] = []

async function frame(page: Page, file: string, width = 1366) {
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

/** Một bước chụp: lỗi ở bước này được ghi lại, các bước sau vẫn chạy. */
async function shot(name: string, fn: () => Promise<void>) {
  try {
    await test.step(name, fn)
  } catch (e) {
    failures.push(`${name}: ${(e as Error).message.split('\n')[0]}`)
  }
}

async function settle(page: Page) {
  await page.waitForLoadState('networkidle').catch(() => {})
  await page.waitForTimeout(600)
}

async function go(page: Page, url: string) {
  await page.goto(url)
  await settle(page)
}

test('chụp ảnh chức năng cho báo cáo', async ({ page, browser }) => {
  // ---------------- Khách ----------------
  await shot('01 trang chủ', async () => { await go(page, '/'); await expect(page.locator('h1').first()).toBeVisible(); await frame(page, '01-trang-chu') })
  await shot('02 danh sách khóa học', async () => { await go(page, '/khoa-hoc'); await frame(page, '02-danh-sach-khoa-hoc') })
  await shot('03 tìm kiếm, lọc', async () => { await go(page, '/khoa-hoc?q=excel'); await frame(page, '03-tim-kiem') })
  await shot('04 chi tiết khóa học', async () => { await go(page, '/khoa-hoc/lap-trinh-c-tu-co-ban-den-nang-cao'); await frame(page, '04-chi-tiet-khoa-hoc') })
  await shot('05 đề cương khóa học', async () => {
    await page.getByRole('heading', { name: /Nội dung khóa học|Đề cương/ }).first().scrollIntoViewIfNeeded()
    await page.mouse.wheel(0, -120); await page.waitForTimeout(400); await frame(page, '05-de-cuong')
  })
  await shot('06 hồ sơ giảng viên', async () => {
    await page.getByRole('region', { name: 'Giảng viên' }).getByRole('link', { name: 'Xem hồ sơ đầy đủ' }).click()
    await settle(page); await frame(page, '06-ho-so-giang-vien')
  })
  await shot('07 đăng nhập', async () => { await go(page, '/dang-nhap'); await frame(page, '07-dang-nhap') })
  await shot('08 đăng ký', async () => { await go(page, '/dang-ky'); await frame(page, '08-dang-ky') })
  await shot('09 chính sách hoàn tiền', async () => { await go(page, '/chinh-sach/hoan-tien'); await frame(page, '09-chinh-sach') })

  // ---------------- Học viên ----------------
  await registerStudent(page, 'Nguyễn Minh An')
  await shot('10 điểm danh, điểm thưởng', async () => {
    await go(page, '/hoc-vien/diem-thuong')
    await page.getByRole('button', { name: 'Điểm danh +5' }).click()
    await expect(page.getByRole('button', { name: 'Đã điểm danh hôm nay' })).toBeVisible()
    await settle(page); await frame(page, '10-diem-thuong')
  })
  await shot('11 giỏ hàng', async () => {
    for (const slug of ['excel-tu-co-ban-den-nang-cao', 'sql-server-cho-lap-trinh-vien']) {
      await go(page, `/khoa-hoc/${slug}`)
      await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click()
      await page.waitForTimeout(800)
    }
    await go(page, '/gio-hang'); await frame(page, '11-gio-hang')
  })
  await shot('12 thanh toán', async () => {
    await go(page, '/thanh-toan')
    await page.getByRole('textbox', { name: 'Mã giảm giá' }).fill('CHAOBANMOI')
    await page.getByRole('button', { name: 'Áp dụng' }).first().click()
    await expect(page.getByText(/Đã áp dụng mã CHAOBANMOI/)).toBeVisible()
    await page.getByText('Dùng điểm thưởng để giảm giá').click()
    await expect(page.getByText(/Điểm thưởng \(5 điểm\)/)).toBeVisible()
    await page.getByLabel(/Kênh thanh toán thử nghiệm/).check()
    await settle(page)
    await page.getByRole('heading', { name: 'Mã giảm giá' }).scrollIntoViewIfNeeded()
    await frame(page, '12-thanh-toan')
  })
  await shot('13 kết quả thanh toán', async () => {
    await page.getByRole('button', { name: 'Đặt hàng và thanh toán' }).click()
    await page.waitForURL(/\/thanh-toan\/ket-qua/)
    await expect(page.getByRole('heading', { name: 'Thanh toán thành công' })).toBeVisible()
    await settle(page); await frame(page, '13-ket-qua-thanh-toan')
  })
  await shot('14 lịch sử mua hàng', async () => { await go(page, '/tai-khoan/don-hang'); await frame(page, '14-lich-su-don-hang') })
  await shot('15 chi tiết đơn hàng', async () => {
    await page.locator('a[href^="/don-hang/"]').first().click(); await settle(page); await frame(page, '15-chi-tiet-don-hang')
  })
  await shot('16 trình học video', async () => {
    await go(page, '/hoc/excel-tu-co-ban-den-nang-cao')
    await expect(page.locator('video')).toBeVisible(); await settle(page); await frame(page, '16-trinh-hoc')
  })

  // Khóa miễn phí: học hết bài → làm bài kiểm tra → nhận chứng chỉ
  let certCode = ''
  await shot('17-18 bài kiểm tra', async () => {
    await go(page, '/khoa-hoc/ky-nang-thuyet-trinh-chuyen-nghiep')
    await page.getByRole('button', { name: 'Đăng ký học miễn phí' }).click()
    await page.getByRole('button', { name: 'Hoàn tất đăng ký' }).click()
    await expect(page.getByRole('heading', { name: 'Đăng ký thành công' })).toBeVisible()
    await go(page, '/hoc/ky-nang-thuyet-trinh-chuyen-nghiep')
    const outline = page.getByRole('complementary', { name: 'Nội dung khóa học' })
    const lessonLinks = outline.locator('a[href*="/bai/"]')
    const count = await lessonLinks.count()
    for (let i = 0; i < count; i++) {
      await lessonLinks.nth(i).click()
      await page.getByRole('button', { name: 'Đánh dấu hoàn thành' }).click()
      await expect(page.getByRole('button', { name: 'Đã hoàn thành' })).toBeVisible()
    }
    await outline.getByRole('link', { name: /Bài kiểm tra cuối khóa/ }).click()
    await page.getByRole('button', { name: 'Bắt đầu làm bài' }).click()
    await expect(page.getByRole('timer')).toBeVisible()
    await page.waitForTimeout(4000)   // chờ thông báo "Đã đánh dấu hoàn thành" tự ẩn
    const questions = page.locator('main fieldset')
    const total = await questions.count()
    for (let i = 0; i < total; i++) {
      const q = questions.nth(i)
      const legend = (await q.locator('legend').innerText()) ?? ''
      const match = legend.match(/"([^"]+)"\?/)
      await q.getByLabel(match ? match[1] : 'Đúng', { exact: true }).check()
    }
    await questions.first().scrollIntoViewIfNeeded()
    await frame(page, '17-lam-bai-kiem-tra')
    await page.getByRole('button', { name: 'Nộp bài' }).click()
    await expect(page.getByText('Đạt — lượt 1')).toBeVisible()
    const certLink = page.locator('a[href^="/xac-minh/EDV-"]')
    certCode = (await certLink.innerText()).trim()
    await settle(page); await frame(page, '18-ket-qua-kiem-tra')
  })
  await shot('19 dashboard học viên', async () => { await go(page, '/hoc-vien'); await frame(page, '19-dashboard-hoc-vien') })
  await shot('20 chứng chỉ của tôi', async () => { await go(page, '/hoc-vien/chung-chi'); await frame(page, '20-chung-chi') })
  await shot('21 kết quả kiểm tra', async () => { await go(page, '/hoc-vien/ket-qua'); await frame(page, '21-ket-qua-hoc-tap') })
  await shot('22 xác minh chứng chỉ', async () => {
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 }, baseURL: 'http://localhost:5174' })
    const anon = await ctx.newPage()
    await go(anon, `/xac-minh/${certCode}`)
    await expect(anon.getByText('Chứng chỉ hợp lệ, do EduViet cấp')).toBeVisible()
    await frame(anon, '22-xac-minh-chung-chi'); await ctx.close()
  })
  await shot('23 hỏi giảng viên', async () => {
    await go(page, '/khoa-hoc/lap-trinh-c-tu-co-ban-den-nang-cao')
    await page.getByRole('button', { name: 'Hỏi giảng viên về khóa học' }).click()
    const dialog = page.getByRole('dialog', { name: /Nguyễn Minh Khoa/ })
    await dialog.getByLabel('Tin nhắn').fill('Thầy ơi, khóa này có dạy LINQ và async/await không ạ?')
    await frame(page, '23-hoi-giang-vien')
    await dialog.getByRole('button', { name: 'Gửi tin nhắn' }).click()
    await page.waitForURL(/\/hoc-vien\/tin-nhan\//)
  })
  await shot('24 chuông thông báo', async () => {
    await go(page, '/')
    await page.getByRole('button', { name: /^Thông báo/ }).click()
    await page.waitForTimeout(600); await frame(page, '24-thong-bao')
    await page.keyboard.press('Escape')
  })

  // ---------------- Giảng viên ----------------
  const ins = await newUser(browser, accounts.instructor, '/giang-day')
  await shot('25 tổng quan giảng viên', async () => { await settle(ins); await frame(ins, '25-giang-vien-tong-quan') })
  await shot('26 khóa học của giảng viên', async () => { await go(ins, '/giang-day/khoa-hoc'); await frame(ins, '26-giang-vien-khoa-hoc') })
  await shot('27 soạn khóa học', async () => {
    await ins.locator('a[href^="/giang-day/khoa-hoc/"]').first().click(); await settle(ins); await frame(ins, '27-soan-khoa-hoc')
  })
  await shot('28 tin nhắn giảng viên', async () => {
    await go(ins, '/giang-day/tin-nhan')
    await ins.locator('a[href^="/giang-day/tin-nhan/"]').first().click(); await settle(ins)
    await ins.getByLabel('Nội dung tin nhắn').fill('Có em nhé, chương 5 nói kỹ về LINQ, chương 6 về async/await.')
    await ins.getByRole('button', { name: 'Gửi tin nhắn' }).click()
    await ins.waitForTimeout(800); await frame(ins, '28-tin-nhan-giang-vien')
  })
  await shot('29 hồ sơ giảng dạy', async () => { await go(ins, '/giang-day/ho-so'); await frame(ins, '29-ho-so-giang-day') })
  await shot('30 học viên của giảng viên', async () => { await go(ins, '/giang-day/hoc-vien'); await frame(ins, '30-giang-vien-hoc-vien') })
  await shot('31 đánh giá', async () => { await go(ins, '/giang-day/danh-gia'); await frame(ins, '31-giang-vien-danh-gia') })
  await ins.context().close()

  // ---------------- Quản trị viên ----------------
  const ad = await newUser(browser, accounts.admin, '/quan-tri')
  const adminPages: [string, string][] = [
    ['32-quan-tri-tong-quan', '/quan-tri'],
    ['33-quan-tri-khoa-hoc', '/quan-tri/khoa-hoc'],
    ['34-quan-tri-nguoi-dung', '/quan-tri/nguoi-dung'],
    ['35-quan-tri-danh-muc', '/quan-tri/danh-muc'],
    ['36-quan-tri-don-hang', '/quan-tri/don-hang'],
    ['37-quan-tri-ma-giam-gia', '/quan-tri/ma-giam-gia'],
    ['38-quan-tri-giao-dich', '/quan-tri/giao-dich'],
    ['39-quan-tri-chung-chi', '/quan-tri/chung-chi'],
    ['40-quan-tri-diem-thuong', '/quan-tri/diem-thuong'],
  ]
  for (const [file, url] of adminPages) await shot(file, async () => { await go(ad, url); await frame(ad, file) })
  await shot('41 quy tắc điểm thưởng', async () => {
    await ad.getByRole('tab', { name: 'Nhiệm vụ & quy tắc' }).click(); await settle(ad); await frame(ad, '41-quan-tri-quy-tac-diem')
  })
  await ad.context().close()

  // ---------------- Swagger + điện thoại ----------------
  await shot('42 swagger', async () => {
    const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 } })
    const sw = await ctx.newPage()
    await sw.goto('http://localhost:5081/swagger/index.html'); await settle(sw); await sw.waitForTimeout(1500)
    await frame(sw, '42-swagger'); await ctx.close()
  })
  await shot('43 điện thoại', async () => {
    const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true, baseURL: 'http://localhost:5174' })
    const m = await ctx.newPage()
    await go(m, '/'); await frame(m, '43-dien-thoai-trang-chu', 390)
    await go(m, '/khoa-hoc/excel-tu-co-ban-den-nang-cao'); await frame(m, '44-dien-thoai-chi-tiet', 390)
    await ctx.close()
  })

  console.log(failures.length ? `Lỗi:\n${failures.join('\n')}` : 'Chụp đủ ảnh.')
  expect(failures).toEqual([])
})

async function newUser(browser: Browser, email: string, next: string) {
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 }, baseURL: 'http://localhost:5174', locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh' })
  const p = await ctx.newPage()
  await login(p, email, undefined, next)
  await settle(p)
  return p
}
