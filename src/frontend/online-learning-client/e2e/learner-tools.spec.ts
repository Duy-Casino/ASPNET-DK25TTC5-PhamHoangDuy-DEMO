import { expect, test } from '@playwright/test'
import { login, registerStudent } from './helpers'

const COURSE = 'ky-nang-thuyet-trinh-chuyen-nghiep'   // khóa miễn phí của giảng viên Lê Anh Thư

test.describe('Tiện ích người học', () => {
  test('video mẫu phát được; ghi chú gắn mốc thời gian; hỏi đáp với giảng viên; tắt email nhắc nhở', async ({ page, browser }) => {
    await registerStudent(page, 'Người Hay Hỏi')
    await page.goto(`/khoa-hoc/${COURSE}`)
    await page.getByRole('button', { name: 'Đăng ký học miễn phí' }).filter({ visible: true }).click()
    await page.getByRole('button', { name: 'Hoàn tất đăng ký' }).click()
    await expect(page).toHaveURL(/\/don-hang\//)
    await page.goto(`/hoc/${COURSE}`)
    await expect(page).toHaveURL(/\/bai\//)

    // ---- Video mẫu tải được (không còn khung đen), không hiện thông báo lỗi
    const video = page.locator('video')
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => v.readyState), { timeout: 15_000 }).toBeGreaterThan(0)
    await expect(page.getByText('Không phát được video')).toHaveCount(0)

    // ---- Ghi chú gắn mốc thời gian: bấm mốc thì tua video
    await video.evaluate((v: HTMLVideoElement) => { v.currentTime = 12 })
    await page.getByRole('tab', { name: 'Ghi chú' }).click()
    await expect(page).toHaveURL(/tab=ghi-chu/)
    await page.getByLabel(/Ghi chú mới/).fill('Quy tắc 10-20-30 cho slide')
    await page.getByRole('button', { name: 'Lưu ghi chú' }).click()
    const stamp = page.getByRole('button', { name: /Tua video đến 0?0:12/ })
    await expect(stamp).toBeVisible()
    await video.evaluate((v: HTMLVideoElement) => { v.pause(); v.currentTime = 30 })
    await stamp.click()
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => Math.round(v.currentTime))).toBeLessThan(15)

    // ---- Hỏi đáp
    await page.getByRole('tab', { name: 'Hỏi đáp' }).click()
    await page.getByLabel('Đặt câu hỏi về bài học này').fill('Mục tiêu thuyết trình nên dài bao nhiêu câu?')
    await page.getByRole('button', { name: 'Gửi câu hỏi' }).click()
    await expect(page.getByText('Mục tiêu thuyết trình nên dài bao nhiêu câu?')).toBeVisible()

    // ---- Giảng viên thấy câu hỏi trong hộp "Hỏi đáp" và trả lời ngay trong bài học
    const teacherCtx = await browser.newContext()
    const teacher = await teacherCtx.newPage()
    await login(teacher, 'lethu.design@eduviet.local', undefined, '/giang-day/hoi-dap')
    await teacher.goto('/giang-day/hoi-dap')
    await expect(teacher.getByRole('heading', { level: 1, name: 'Hỏi đáp của học viên' })).toBeVisible()
    const item = teacher.getByRole('listitem').filter({ hasText: 'Mục tiêu thuyết trình nên dài bao nhiêu câu?' })
    await item.getByRole('link', { name: 'Mở và trả lời' }).click()
    await expect(teacher).toHaveURL(/tab=hoi-dap/)
    const question = teacher.getByRole('listitem', { name: 'Câu hỏi của Người Hay Hỏi' })
    await question.getByRole('button', { name: 'Trả lời' }).click()
    await question.getByLabel('Câu trả lời').fill('Một câu thôi, có động từ hành động và kết quả đo được.')
    await question.getByRole('button', { name: 'Gửi trả lời' }).click()
    await expect(question.getByText('Giảng viên', { exact: true })).toBeVisible()
    await teacherCtx.close()

    await page.reload()
    const mine = page.getByRole('listitem', { name: 'Câu hỏi của Người Hay Hỏi' })
    await expect(mine.getByText('Một câu thôi, có động từ hành động')).toBeVisible()
    await mine.getByRole('button', { name: 'Đánh dấu đã giải đáp' }).click()
    await expect(mine.getByText('Đã giải đáp')).toBeVisible()

    // ---- Tắt email nhắc nhở ở hồ sơ cá nhân
    await page.goto('/tai-khoan')
    const toggle = page.getByRole('checkbox', { name: 'Nhận email nhắc nhở' })
    await expect(toggle).toBeChecked()
    await toggle.click({ force: true })
    await expect(page.getByText('Đã tắt email nhắc nhở')).toBeVisible()
    await page.reload()
    await expect(page.getByRole('checkbox', { name: 'Nhận email nhắc nhở' })).not.toBeChecked()
  })
})
