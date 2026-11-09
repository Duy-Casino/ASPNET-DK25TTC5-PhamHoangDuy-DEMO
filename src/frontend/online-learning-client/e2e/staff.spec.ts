import { expect, test } from '@playwright/test'
import { accounts, login } from './helpers'

test.describe('Giảng viên & Admin', () => {
  test('giảng viên tạo khóa học, thêm chương/bài/bài kiểm tra; gửi duyệt khi thiếu thông tin bị chặn', async ({ page }) => {
    await login(page, accounts.instructor, undefined, '/giang-day')
    await expect(page.getByRole('heading', { name: 'Tổng quan giảng dạy' })).toBeVisible()
    await expect(page.getByRole('img', { name: 'Biểu đồ doanh thu 12 tháng' })).toBeVisible()

    await page.goto('/giang-day/khoa-hoc')
    await page.getByRole('button', { name: 'Tạo khóa học' }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Tạo khóa học' })
    await dialog.getByLabel('Tên khóa học').fill('Git và GitHub E2E')
    await dialog.getByLabel('Danh mục').selectOption({ label: 'Lập trình' })
    await dialog.getByRole('button', { name: 'Tạo và soạn nội dung' }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Git và GitHub E2E' })).toBeVisible()

    await page.getByRole('tab', { name: 'Chương & bài học' }).click()
    await page.getByLabel('Chương mới').fill('Bắt đầu với Git')
    await page.getByRole('button', { name: 'Thêm chương' }).click()
    await expect(page.getByRole('heading', { name: 'Chương 1: Bắt đầu với Git' })).toBeVisible()

    await page.getByRole('button', { name: 'Thêm bài học' }).click()
    const lesson = page.getByRole('dialog', { name: 'Thêm bài học' })
    await lesson.getByLabel('Tên bài học').fill('Cài đặt Git')
    await lesson.getByLabel('Hoặc dán URL video (https)').fill('https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4')
    await lesson.getByLabel('Thời lượng (phút)').fill('8')
    await lesson.getByRole('button', { name: 'Lưu bài học' }).click()
    await expect(page.getByText('Cài đặt Git')).toBeVisible()

    await page.getByRole('tab', { name: 'Bài kiểm tra' }).click()
    await page.getByRole('button', { name: 'Tạo bài kiểm tra' }).click()
    const quiz = page.getByRole('dialog', { name: 'Tạo bài kiểm tra' })
    await quiz.getByLabel('Tên bài kiểm tra').fill('Kiểm tra Git')
    await quiz.getByLabel('Nội dung câu hỏi').fill('Lệnh nào khởi tạo kho Git?')
    await quiz.getByLabel('Nội dung đáp án 1').fill('git init')
    await quiz.getByLabel('Nội dung đáp án 2').fill('git start')
    await quiz.getByRole('button', { name: 'Lưu bài kiểm tra' }).click()
    await expect(page.getByText('Kiểm tra Git')).toBeVisible()

    await page.getByRole('button', { name: 'Gửi duyệt' }).click()
    await expect(page.getByText(/Khóa học còn thiếu: mô tả ngắn, mô tả chi tiết, ảnh đại diện/)).toBeVisible()
  })

  test('admin xem tổng quan, duyệt khóa học chờ duyệt, tra cứu giao dịch', async ({ page }) => {
    await login(page, accounts.admin, undefined, '/quan-tri')
    await expect(page.getByRole('heading', { name: 'Tổng quan hệ thống' })).toBeVisible()

    await page.goto('/quan-tri/khoa-hoc')
    const row = page.getByRole('row', { name: /Docker và Kubernetes cho Developer/ })
    await expect(row).toBeVisible()
    await row.getByRole('button', { name: 'Duyệt' }).click()
    await expect(page.getByText('Đã duyệt và xuất bản')).toBeVisible()

    // Khóa vừa duyệt xuất hiện công khai
    await page.goto('/khoa-hoc?keyword=Docker')
    await expect(page.getByRole('link', { name: /Docker và Kubernetes cho Developer/ }).first()).toBeVisible()

    await page.goto('/quan-tri/giao-dich')
    await expect(page.getByRole('heading', { name: 'Giao dịch thanh toán' })).toBeVisible()
    await expect(page.getByRole('table')).toBeVisible()
  })

  test('học viên không vào được khu vực quản trị', async ({ page }) => {
    await login(page, accounts.student)
    await page.goto('/quan-tri')
    await expect(page).toHaveURL('/')
    await page.goto('/giang-day')
    await expect(page).toHaveURL('/')
  })
})
