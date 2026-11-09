import { expect, test } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

test.describe('Học viên', () => {
  test('đăng ký → mua khóa có phí qua kênh test → vào học', async ({ page }) => {
    await registerStudent(page, 'Người Mua Test')

    await page.goto('/khoa-hoc/power-bi-phan-tich-du-lieu-kinh-doanh')
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click()
    await expect(page.getByRole('link', { name: 'Giỏ hàng, 1 khóa học' })).toBeVisible()

    await page.goto('/gio-hang')
    await page.getByRole('button', { name: 'Thanh toán' }).click()
    await expect(page).toHaveURL(/\/thanh-toan$/)

    // Mã sai → báo lỗi; mã đúng → giảm giá
    await page.getByRole('textbox', { name: 'Mã giảm giá' }).fill('SAIMA')
    await page.getByRole('button', { name: 'Áp dụng' }).click()
    await expect(page.getByText('Mã giảm giá không hợp lệ hoặc đã hết hạn.')).toBeVisible()
    await page.getByRole('textbox', { name: 'Mã giảm giá' }).fill('CHAOBANMOI')
    await page.getByRole('button', { name: 'Áp dụng' }).click()
    await expect(page.getByText(/Đã áp dụng mã CHAOBANMOI/)).toBeVisible()

    // Kênh test: có thông báo chưa thu tiền thật
    await page.getByLabel(/Kênh thanh toán thử nghiệm/).check()
    await expect(page.getByRole('note')).toContainText('Hiện tại đang trong giai đoạn test kênh thanh toán, chưa thu tiền thật')

    await page.getByRole('button', { name: 'Đặt hàng và thanh toán' }).click()
    await page.waitForURL(/\/thanh-toan\/ket-qua/)
    await expect(page.getByRole('heading', { name: 'Thanh toán thành công' })).toBeVisible()
    await expect(page.getByRole('note')).toContainText('chưa thu tiền thật')
    await expect(page.getByText('Kênh thử nghiệm')).toBeVisible()

    await page.getByRole('link', { name: 'Vào học ngay' }).click()
    await expect(page).toHaveURL(/\/hoc-vien$/)
    await expect(page.getByText('Power BI phân tích dữ liệu kinh doanh').first()).toBeVisible()

    // Trang khóa học báo đã sở hữu + vào học
    await page.goto('/khoa-hoc/power-bi-phan-tich-du-lieu-kinh-doanh')
    // Thẻ mua hàng có 2 bản (desktop dính + điện thoại), chỉ lấy bản đang hiển thị
    await expect(page.getByText('Bạn đã sở hữu khóa học này').filter({ visible: true })).toBeVisible()
    await page.getByRole('link', { name: 'Vào học', exact: true }).filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/hoc\/power-bi-phan-tich-du-lieu-kinh-doanh\/bai\//)
    await expect(page.locator('video')).toBeVisible()
  })

  test('học hết khóa miễn phí → đạt bài kiểm tra → nhận chứng chỉ → xác minh công khai', async ({ page, browser }) => {
    await registerStudent(page, 'Trần Chứng Chỉ')
    await page.goto('/khoa-hoc/ky-nang-thuyet-trinh-chuyen-nghiep')
    await page.getByRole('button', { name: 'Đăng ký học miễn phí' }).click()
    await page.getByRole('button', { name: 'Hoàn tất đăng ký' }).click()
    await expect(page.getByRole('heading', { name: 'Đăng ký thành công' })).toBeVisible()

    await page.goto('/hoc/ky-nang-thuyet-trinh-chuyen-nghiep')
    const outline = page.getByRole('complementary', { name: 'Nội dung khóa học' })
    const lessonLinks = outline.locator('a[href*="/bai/"]')
    await expect(lessonLinks.first()).toBeVisible()
    const count = await lessonLinks.count()
    expect(count).toBeGreaterThan(0)
    for (let i = 0; i < count; i++) {
      await lessonLinks.nth(i).click()
      const markDone = page.getByRole('button', { name: 'Đánh dấu hoàn thành' })
      await markDone.click()
      await expect(page.getByRole('button', { name: 'Đã hoàn thành' })).toBeVisible()
    }
    await expect(outline).toContainText(`${count}/${count} bài bắt buộc · 100%`)

    // Bài kiểm tra cuối khóa: đáp án đúng là tên bài trong ngoặc kép, câu đúng/sai chọn "Đúng"
    await outline.getByRole('link', { name: /Bài kiểm tra cuối khóa/ }).click()
    await page.getByRole('button', { name: 'Bắt đầu làm bài' }).click()
    await expect(page.getByRole('timer')).toBeVisible()
    const questions = page.locator('main fieldset')
    const total = await questions.count()
    for (let i = 0; i < total; i++) {
      const q = questions.nth(i)
      const legend = (await q.locator('legend').innerText()) ?? ''
      const match = legend.match(/"([^"]+)"\?/)
      await q.getByLabel(match ? match[1] : 'Đúng', { exact: true }).check()
    }
    await page.getByRole('button', { name: 'Nộp bài' }).click()
    await expect(page.getByText('Đạt — lượt 1')).toBeVisible()
    const certLink = page.locator('a[href^="/xac-minh/EDV-"]')
    await expect(certLink).toBeVisible()
    const code = (await certLink.innerText()).trim()
    expect(code).toMatch(/^EDV-\d{4}-[2-9A-Z]{8}$/)

    // Trang chứng chỉ của học viên + tải PDF
    await page.goto('/hoc-vien/chung-chi')
    await expect(page.getByText(code)).toBeVisible()
    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Tải PDF' }).click()
    expect((await download).suggestedFilename()).toBe(`chung-chi-${code}.pdf`)

    // Chuông thông báo: có thông báo "Bạn đã nhận chứng chỉ" chưa đọc → mở → hết chưa đọc
    await page.goto('/')
    const bell = page.getByRole('button', { name: /^Thông báo, \d+ chưa đọc$/ })
    await expect(bell).toBeVisible()
    await bell.click()
    const panel = page.getByRole('dialog', { name: 'Thông báo' })
    await expect(panel.getByText('Bạn đã nhận chứng chỉ')).toBeVisible()
    await panel.getByRole('button', { name: 'Đánh dấu đã đọc tất cả' }).click()
    await expect(page.getByRole('button', { name: 'Thông báo', exact: true })).toBeVisible()

    // Người lạ (không đăng nhập) xác minh được
    const anon = await browser.newPage()
    await anon.goto(`/xac-minh/${code}`)
    await expect(anon.getByText('Chứng chỉ hợp lệ, do EduViet cấp')).toBeVisible()
    await expect(anon.getByText('Trần Chứng Chỉ')).toBeVisible()
    await anon.close()
  })

  test('dashboard học viên mẫu có số liệu và tiếp tục học', async ({ page }) => {
    await login(page, accounts.student, undefined, '/hoc-vien')
    await expect(page.getByRole('heading', { name: /học tiếp nhé/ })).toBeVisible()
    await expect(page.getByText('Khóa học đã mua')).toBeVisible()
    await page.getByRole('link', { name: /Học tiếp/ }).click()
    await expect(page).toHaveURL(/\/hoc\/.+\/bai\//)
  })
})
