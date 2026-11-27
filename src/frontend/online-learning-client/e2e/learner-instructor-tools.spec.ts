import { expect, test } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

const COURSE = 'ky-nang-thuyet-trinh-chuyen-nghiep'   // khóa miễn phí của giảng viên Lê Anh Thư, kiểm tra cuối khóa tối đa 3 lượt

test.describe('Tiện ích người học và giảng viên', () => {
  test('trình phát: tốc độ được nhớ, phím tắt, tự chuyển bài; tìm bài trong mục lục; ôn câu sai; giảng viên cấp thêm lượt', async ({ page, browser }) => {
    page.on('dialog', (d) => d.accept())   // "còn câu chưa trả lời, vẫn nộp?"
    await registerStudent(page, 'Người Học Chăm')
    await page.goto(`/khoa-hoc/${COURSE}`)
    await page.getByRole('button', { name: 'Đăng ký học miễn phí' }).filter({ visible: true }).click()
    await page.getByRole('button', { name: 'Hoàn tất đăng ký' }).click()
    await expect(page).toHaveURL(/\/don-hang\//)
    await page.goto(`/hoc/${COURSE}`)
    await expect(page).toHaveURL(/\/bai\//)
    const firstLessonUrl = page.url()

    // ---- Tốc độ phát: chọn 1,5x, tải lại trang vẫn giữ
    const video = page.locator('video')
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => v.readyState), { timeout: 15_000 }).toBeGreaterThan(0)
    await page.getByLabel('Tốc độ phát').selectOption('1.5')
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => v.playbackRate)).toBe(1.5)
    await page.reload()
    await expect(page.getByLabel('Tốc độ phát')).toHaveValue('1.5')
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => v.playbackRate), { timeout: 15_000 }).toBe(1.5)

    // ---- Phím tắt: → tua tới 10 giây, có phản hồi trên màn hình; bảng phím tắt mở được
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => v.readyState), { timeout: 15_000 }).toBeGreaterThan(0)
    await video.evaluate((v: HTMLVideoElement) => { v.pause(); v.currentTime = 5 })
    await page.locator('h1').click()
    await page.keyboard.press('ArrowRight')
    await expect(page.getByText('+10 giây')).toBeVisible()
    await expect.poll(() => video.evaluate((v: HTMLVideoElement) => Math.round(v.currentTime))).toBeGreaterThanOrEqual(14)
    await page.getByRole('button', { name: 'Phím tắt' }).click()
    await expect(page.getByText('Lùi 10 giây')).toBeVisible()
    await page.getByRole('button', { name: 'Phím tắt' }).click()

    // ---- Hết video: đếm ngược sang bài tiếp theo, có thể ở lại
    await video.evaluate((v: HTMLVideoElement) => v.dispatchEvent(new Event('ended')))
    const countdown = page.getByRole('status').filter({ hasText: 'Bài tiếp theo sau' })
    await expect(countdown).toBeVisible()
    await countdown.getByRole('button', { name: 'Ở lại bài này' }).click()
    await expect(countdown).toHaveCount(0)
    await expect(page).toHaveURL(firstLessonUrl)
    await video.evaluate((v: HTMLVideoElement) => v.dispatchEvent(new Event('ended')))
    await page.getByRole('button', { name: 'Phát ngay' }).click()
    await expect(page).not.toHaveURL(firstLessonUrl)

    // ---- Tìm bài học trong mục lục (không dấu vẫn khớp)
    const outline = page.getByRole('complementary', { name: 'Nội dung khóa học' })
    const search = outline.getByLabel('Tìm bài học trong khóa')
    await search.fill('khong co bai nao nhu vay')
    await expect(outline.getByText('Không tìm thấy bài học phù hợp.')).toBeVisible()
    await search.fill('kiem tra cuoi khoa')
    await expect(outline.getByRole('link', { name: /Bài kiểm tra cuối khóa/ })).toBeVisible()
    await expect(outline.getByText(/\d+ kết quả/)).toBeVisible()

    // ---- Bài kiểm tra: nộp trống 3 lần; ôn câu sai và luyện lại không tính điểm
    await outline.getByRole('link', { name: /Bài kiểm tra cuối khóa/ }).first().click()
    for (let i = 1; i <= 3; i++) {
      await page.getByRole('button', { name: i === 1 ? 'Bắt đầu làm bài' : 'Làm lại' }).click()
      await page.getByRole('button', { name: 'Nộp bài' }).click()
      await expect(page.getByText(`Chưa đạt — lượt ${i}`)).toBeVisible()
    }
    await page.getByRole('button', { name: /^Câu sai \(\d+\)$/ }).click()
    await expect(page.getByRole('button', { name: /^Câu sai/ })).toHaveAttribute('aria-pressed', 'true')
    await page.getByRole('button', { name: 'Luyện lại câu sai' }).click()
    const practice = page.getByRole('region', { name: 'Luyện lại câu sai' })
    await expect(practice.getByText(/Câu 1\/\d+ · không tính điểm/)).toBeVisible()
    await practice.locator('label').first().click()
    await practice.getByRole('button', { name: 'Kiểm tra' }).click()
    await expect(practice.getByRole('status')).toContainText(/Chính xác!|Chưa đúng/)
    await practice.getByRole('button', { name: 'Thoát luyện tập' }).click()
    await expect(page.getByRole('heading', { name: 'Đáp án chi tiết' })).toBeVisible()

    // ---- Giảng viên thấy học viên hết lượt và cấp thêm một lượt
    const teacherCtx = await browser.newContext()
    const teacher = await teacherCtx.newPage()
    await login(teacher, 'lethu.design@eduviet.local', undefined, '/giang-day/hoc-vien')
    await teacher.goto('/giang-day/hoc-vien')
    await expect(teacher.getByRole('heading', { level: 1, name: 'Học viên' })).toBeVisible()
    await teacher.getByRole('tab', { name: /Hết lượt kiểm tra/ }).click()
    const row = teacher.getByRole('row').filter({ hasText: 'Người Học Chăm' })
    await expect(row.getByText('Hết lượt, chưa đạt')).toBeVisible()
    await expect(row.getByText(/3\/3 lượt/)).toBeVisible()
    await row.getByRole('button', { name: 'Cấp thêm lượt' }).click()
    const dialog = teacher.getByRole('dialog', { name: 'Cấp thêm lượt làm bài' })
    await dialog.getByLabel('Lý do (học viên sẽ thấy)').fill('Hãy xem lại chương 2 rồi làm lại nhé.')
    await dialog.getByRole('button', { name: 'Cấp thêm lượt' }).click()
    await expect(teacher.getByText('Đã cấp thêm 1 lượt cho Người Học Chăm')).toBeVisible()
    await teacherCtx.close()

    await page.reload()
    await expect(page.getByRole('button', { name: 'Làm lại' })).toBeVisible()
  })

  test('ô tìm kiếm gợi ý khóa học và giảng viên khi gõ; chọn bằng bàn phím', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: 'Mở tìm kiếm' }).click()
    const box = page.getByRole('combobox', { name: 'Tìm khóa học' })
    await box.fill('Excel')
    const list = page.getByRole('listbox', { name: 'Gợi ý tìm kiếm' })
    await expect(list.getByRole('option', { name: /Excel từ cơ bản đến nâng cao/ })).toBeVisible()
    await expect(page.getByRole('button', { name: /Xem tất cả kết quả cho “Excel”/ })).toBeVisible()
    await box.press('ArrowDown')
    await expect(box).toHaveAttribute('aria-activedescendant', /.+/)
    await box.press('Enter')
    await expect(page).toHaveURL(/\/khoa-hoc\/[a-z0-9-]+$/)
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Excel')

    // Escape đóng gợi ý trước, ô tìm kiếm vẫn mở
    await page.getByRole('button', { name: 'Mở tìm kiếm' }).click()
    await box.fill('Lê Anh')
    await expect(list.getByText('Giảng viên', { exact: true })).toBeVisible()
    await box.press('Escape')
    await expect(list).toHaveCount(0)
    await expect(box).toBeVisible()
  })

  test('giảng viên tải phụ đề .srt cho bài của khóa đã xuất bản', async ({ page }) => {
    await login(page, accounts.instructor, undefined, '/giang-day/khoa-hoc')
    await page.goto('/giang-day/khoa-hoc')
    await page.getByRole('link').filter({ hasText: 'Đã xuất bản' }).first().click()
    await page.getByRole('tab', { name: 'Chương & bài học' }).click()
    await page.getByRole('button', { name: /^Phụ đề bài / }).first().click()
    const dialog = page.getByRole('dialog', { name: /^Phụ đề:/ })
    await dialog.getByLabel(/Tải phụ đề lên/).setInputFiles({
      name: 'bai-1.srt',
      mimeType: 'application/x-subrip',
      buffer: Buffer.from('1\n00:00:01,000 --> 00:00:04,000\nXin chào các bạn\n\n2\n00:00:05,000 --> 00:00:08,000\nBắt đầu bài học\n', 'utf8'),
    })
    await expect(page.getByText('Đã gắn phụ đề cho bài học')).toBeVisible()
    await expect(dialog.getByText('Đã có phụ đề tiếng Việt.')).toBeVisible()
    await dialog.getByRole('button', { name: 'Đóng' }).last().click()
    await expect(page.getByText(/· Có phụ đề/).first()).toBeVisible()
  })
})
