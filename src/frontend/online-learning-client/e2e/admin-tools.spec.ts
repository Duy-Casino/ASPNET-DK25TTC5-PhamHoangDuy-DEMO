import { expect, test } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

test.describe('Công cụ quản trị', () => {
  test('đăng ký làm giảng viên → Admin duyệt → có quyền giảng dạy; thông báo hàng loạt; nhật ký; cài đặt áp dụng ngay', async ({ page, browser }) => {
    // ---- Học viên gửi hồ sơ giảng dạy
    await registerStudent(page, 'Ứng Viên Giảng Dạy E2E')
    await page.goto('/dang-ky-giang-day')
    await expect(page.getByRole('heading', { level: 1, name: 'Trở thành giảng viên' })).toBeVisible()
    await page.getByLabel('Chức danh').fill('Chuyên viên phân tích dữ liệu')
    await page.getByLabel('Lĩnh vực chuyên môn').fill('Excel, Power BI')
    await page.getByLabel('Số năm kinh nghiệm').fill('5')
    await page.getByLabel('Khóa học dự định dạy').fill('Power BI cho người đi làm')
    await page.getByLabel('Giới thiệu bản thân và kinh nghiệm giảng dạy').fill('Tôi có 5 năm làm báo cáo quản trị và đã đào tạo nội bộ Power BI cho nhiều phòng ban.')
    await page.getByRole('button', { name: 'Gửi hồ sơ' }).click()
    await expect(page.getByRole('status').getByText('Chờ duyệt')).toBeVisible()

    // ---- Admin duyệt hồ sơ
    const adminCtx = await browser.newContext()
    const admin = await adminCtx.newPage()
    await login(admin, accounts.admin, undefined, '/quan-tri/dang-ky-giang-vien')
    await admin.goto('/quan-tri/dang-ky-giang-vien')
    const card = admin.locator('article').filter({ hasText: 'Ứng Viên Giảng Dạy E2E' })
    await expect(card.getByText('Power BI cho người đi làm')).toBeVisible()
    await card.getByRole('button', { name: 'Duyệt' }).click()
    await admin.getByRole('dialog', { name: /Duyệt hồ sơ/ }).getByRole('button', { name: 'Duyệt và cấp vai trò' }).click()
    await expect(admin.getByText('Đã cấp vai trò Giảng viên cho Ứng Viên Giảng Dạy E2E')).toBeVisible()

    // ---- Học viên giờ là giảng viên, vào được khu vực giảng dạy
    await page.reload()
    await expect(page.getByRole('heading', { name: 'Bạn đã là giảng viên' })).toBeVisible()
    await page.getByRole('button', { name: 'Vào khu vực giảng dạy' }).click()
    await expect(page).toHaveURL(/\/giang-day\/khoa-hoc$/)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()

    // ---- Thông báo hàng loạt tới giảng viên
    const title = `Cập nhật chính sách ${Date.now()}`
    await admin.goto('/quan-tri/thong-bao')
    await admin.getByLabel('Người nhận').selectOption('Instructors')
    await admin.getByLabel('Tiêu đề').fill(title)
    await admin.getByLabel('Nội dung').fill('Từ tháng sau, tỷ lệ chia sẻ doanh thu được cập nhật trong trang Doanh thu.')
    await admin.getByLabel('Đường dẫn khi bấm vào (không bắt buộc)').fill('/giang-day/doanh-thu')
    await expect(admin.getByText(/Sẽ gửi tới \d+ người/)).toBeVisible()
    await admin.getByRole('button', { name: 'Gửi thông báo' }).click()
    await admin.getByRole('dialog', { name: 'Xác nhận gửi thông báo' }).getByRole('button', { name: 'Gửi ngay' }).click()
    await expect(admin.getByText(/Đã gửi tới \d+ người/)).toBeVisible()
    await expect(admin.getByRole('row').filter({ hasText: title })).toBeVisible()

    // ---- Nhật ký quản trị ghi nhận các thao tác trên
    await admin.goto('/quan-tri/nhat-ky')
    await expect(admin.getByRole('row').filter({ hasText: 'Duyệt hồ sơ giảng viên' }).first()).toBeVisible()
    await expect(admin.getByRole('row').filter({ hasText: 'Gửi thông báo hàng loạt' }).first()).toBeVisible()

    // ---- Cài đặt: đổi số ngày hoàn tiền → trang chính sách hiển thị ngay, rồi khôi phục mặc định
    await admin.goto('/quan-tri/cai-dat')
    const days = admin.getByLabel('Số ngày được yêu cầu hoàn tiền')
    await days.fill('12')
    await admin.getByRole('button', { name: /Lưu thay đổi \(1\)/ }).click()
    await expect(admin.getByText('Đã lưu 1 cài đặt')).toBeVisible()
    await page.goto('/chinh-sach/hoan-tien')
    await expect(page.getByText('12 ngày', { exact: true })).toBeVisible()
    await admin.getByRole('button', { name: 'Về mặc định' }).first().click()
    await expect(admin.getByText(/Đã khôi phục mặc định/)).toBeVisible()
    await page.reload()
    await expect(page.getByText('7 ngày', { exact: true })).toBeVisible()

    // ---- Báo cáo, trạng thái hệ thống, kiểm duyệt đánh giá
    await admin.goto('/quan-tri/bao-cao')
    await expect(admin.getByRole('heading', { name: 'Doanh thu theo tháng' })).toBeVisible()
    await expect(admin.getByRole('heading', { name: 'Khóa học bán chạy' })).toBeVisible()
    await admin.getByRole('button', { name: 'Bài học' }).first().click()
    await expect(admin.getByRole('dialog', { name: /Tỷ lệ hoàn thành từng bài/ })).toContainText('học viên đang có quyền học')
    await admin.keyboard.press('Escape')

    await admin.goto('/quan-tri/he-thong')
    await expect(admin.getByText('Cơ sở dữ liệu')).toBeVisible()
    await admin.getByRole('row').filter({ hasText: 'Hủy đơn' }).getByRole('button', { name: 'Chạy ngay' }).click()
    await expect(admin.getByRole('row').filter({ hasText: 'Hủy đơn' }).getByText('Thành công')).toBeVisible()

    await admin.goto('/quan-tri/danh-gia')
    await expect(admin.getByRole('heading', { level: 1, name: 'Kiểm duyệt đánh giá' })).toBeVisible()
    await expect(admin.getByRole('row').nth(1)).toBeVisible()
    await adminCtx.close()
  })
})
