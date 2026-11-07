import { expect, test } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

test.describe('Hồ sơ giảng viên & trao đổi', () => {
  test('trang giảng viên có huy hiệu xác minh, kinh nghiệm, chứng nhận và số liệu uy tín', async ({ page }) => {
    await page.goto('/khoa-hoc/lap-trinh-c-tu-co-ban-den-nang-cao')
    const insBlock = page.getByRole('region', { name: 'Giảng viên' })
    await expect(insBlock.getByText('Đã xác minh')).toBeVisible()
    await insBlock.getByRole('link', { name: 'Xem hồ sơ đầy đủ' }).click()

    await expect(page.getByRole('heading', { level: 1, name: 'Nguyễn Minh Khoa' })).toBeVisible()
    await expect(page.getByText('10 năm kinh nghiệm', { exact: true })).toBeVisible()
    await expect(page.getByRole('list', { name: 'Chuyên môn' }).getByText('ASP.NET Core')).toBeVisible()
    for (const name of ['Quá trình làm việc và học tập', 'Chứng nhận chuyên môn', 'Thông tin xác thực']) {
      await expect(page.getByRole('heading', { name })).toBeVisible()
    }
    await expect(page.getByText('Kiến trúc sư phần mềm')).toBeVisible()
    await expect(page.getByText('Microsoft Certified: Azure Developer Associate')).toBeVisible()
    // Dữ liệu có cấu trúc Person cho công cụ tìm kiếm
    const ld = JSON.parse((await page.locator('#seo-jsonld').textContent())!)
    expect(ld['@type']).toBe('Person')

    // Khách bấm nhắn tin → chuyển tới đăng nhập kèm đường quay lại
    await page.getByRole('button', { name: 'Nhắn tin cho giảng viên' }).first().click()
    await expect(page).toHaveURL(/\/dang-nhap\?next=%2Fgiang-vien%2F/)
  })

  test('học viên hỏi về khóa học → giảng viên nhận thông báo, trả lời → học viên thấy trả lời', async ({ page, browser }) => {
    await registerStudent(page, 'Lê Hỏi Bài')
    await page.goto('/khoa-hoc/lap-trinh-c-tu-co-ban-den-nang-cao')
    await page.getByRole('button', { name: 'Hỏi giảng viên về khóa học' }).click()
    const dialog = page.getByRole('dialog', { name: /Nguyễn Minh Khoa/ })
    await expect(dialog.getByText('Về khóa học:')).toContainText('Lập trình C# từ cơ bản đến nâng cao')
    await dialog.getByLabel('Tin nhắn').fill('Thầy ơi, khóa này có dạy LINQ không ạ?')
    await dialog.getByRole('button', { name: 'Gửi tin nhắn' }).click()

    await expect(page).toHaveURL(/\/hoc-vien\/tin-nhan\/[0-9a-f-]{36}$/)
    const conversationUrl = new URL(page.url()).pathname
    const conversationId = conversationUrl.split('/').pop()!
    await expect(page.getByRole('region', { name: 'Nội dung trao đổi' }).getByText('Thầy ơi, khóa này có dạy LINQ không ạ?', { exact: true })).toBeVisible()

    // Giảng viên: chuông thông báo + mục "Tin nhắn" có số chưa đọc
    const instructorPage = await browser.newPage()
    await login(instructorPage, accounts.instructor, undefined, '/')
    await instructorPage.getByRole('button', { name: /^Thông báo, \d+ chưa đọc$/ }).click()
    await instructorPage.getByRole('dialog', { name: 'Thông báo' }).getByRole('button', { name: /Tin nhắn mới từ Lê Hỏi Bài/ }).click()
    await expect(instructorPage).toHaveURL(new RegExp(`/giang-day/tin-nhan/${conversationId}$`))
    await expect(instructorPage.getByRole('region', { name: 'Nội dung trao đổi' }).getByText('Thầy ơi, khóa này có dạy LINQ không ạ?', { exact: true })).toBeVisible()
    await instructorPage.getByLabel('Nội dung tin nhắn').fill('Có em nhé, chương 5 nói kỹ về LINQ.')
    await instructorPage.getByRole('button', { name: 'Gửi tin nhắn' }).click()
    await expect(instructorPage.getByRole('region', { name: 'Nội dung trao đổi' }).getByText('Có em nhé, chương 5 nói kỹ về LINQ.', { exact: true })).toBeVisible()
    await instructorPage.close()

    // Học viên thấy câu trả lời (tải lại hoặc chờ polling)
    await page.reload()
    await expect(page.getByRole('region', { name: 'Nội dung trao đổi' }).getByText('Có em nhé, chương 5 nói kỹ về LINQ.', { exact: true })).toBeVisible()
    await expect(page.getByRole('navigation', { name: 'Học viên' }).getByRole('link', { name: /Tin nhắn/ })).toBeVisible()
  })

  test('giảng viên cập nhật hồ sơ: chuyên môn, kinh nghiệm; lỗi được báo đúng ô', async ({ page }) => {
    await login(page, 'lethu.design@eduviet.local', undefined, '/giang-day/ho-so')
    await expect(page.getByRole('heading', { level: 1, name: 'Hồ sơ giảng viên' })).toBeVisible()

    await page.getByLabel('Số năm kinh nghiệm').fill('7')
    await page.getByLabel(/Chuyên môn/).fill('Prototyping')
    await page.getByLabel(/Chuyên môn/).press('Enter')

    // Thêm một chứng nhận với đường dẫn không hợp lệ → báo lỗi
    const certPanel = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Chứng nhận', exact: true }) })
    await certPanel.getByRole('button', { name: 'Thêm' }).click()
    await certPanel.getByLabel('Tên chứng nhận').fill('Google UX Design Certificate')
    await certPanel.getByLabel(/Đường dẫn xác minh/).fill('http://khong-an-toan.example')
    await page.getByRole('button', { name: 'Lưu hồ sơ' }).click()
    await expect(certPanel.getByText('Đường dẫn chứng nhận phải là https.')).toBeVisible()

    await certPanel.getByLabel(/Đường dẫn xác minh/).fill('https://www.coursera.org/account/accomplishments/example')
    await page.getByRole('button', { name: 'Lưu hồ sơ' }).click()
    await expect(page.getByText('Đã lưu hồ sơ giảng viên')).toBeVisible()

    // Trang công khai phản ánh thay đổi
    await page.getByRole('link', { name: /Xem trang công khai/ }).evaluate((a: HTMLAnchorElement) => a.removeAttribute('target'))
    await page.getByRole('link', { name: /Xem trang công khai/ }).click()
    await expect(page.getByText('7 năm kinh nghiệm', { exact: true })).toBeVisible()
    await expect(page.getByRole('list', { name: 'Chuyên môn' }).getByText('Prototyping')).toBeVisible()
    await expect(page.getByText('Google UX Design Certificate')).toBeVisible()
    await expect(page.getByRole('link', { name: /Xem chứng nhận/ })).toHaveAttribute('href', /coursera\.org/)
  })

  test('admin xác minh giảng viên từ trang người dùng → huy hiệu hiện trên hồ sơ', async ({ page }) => {
    await login(page, accounts.admin, undefined, '/quan-tri/nguoi-dung')
    await page.getByLabel('Tìm kiếm').fill('lethu')
    const row = page.getByRole('row', { name: /Lê Anh Thư/ })
    await row.getByText('Chưa xác minh', { exact: true }).click()
    await expect(row.getByRole('checkbox', { name: 'Đã xác minh' })).toBeChecked()

    await page.goto('/khoa-hoc/thiet-ke-ui-ux-voi-figma')
    await expect(page.getByRole('region', { name: 'Giảng viên' }).getByText('Đã xác minh')).toBeVisible()
  })
})
