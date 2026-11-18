import path from 'node:path'
import { expect, test, type Page } from '@playwright/test'
import { accounts, login } from '../e2e/helpers'
import { frame, settle } from './frame'

/**
 * Ảnh cho mục "Soạn và chia sẻ khóa học" trong báo cáo: giảng viên tạo khóa học, cập nhật thông tin,
 * tải ảnh bìa, video bài giảng, tài liệu, soạn bài kiểm tra, gửi duyệt; Admin duyệt; khóa học xuất hiện công khai.
 *
 * Tệp mẫu (ảnh, video, PDF) đặt trong thư mục DOCS_ASSETS (mặc định: docs-shots/assets).
 */
const ASSETS = process.env.DOCS_ASSETS ?? path.resolve('docs-shots/assets')
const TITLE = 'Git và GitHub cho người mới bắt đầu'

async function toastGone(page: Page) {
  await page.waitForTimeout(4200)
}

test('giảng viên soạn và chia sẻ khóa học mới, admin duyệt', async ({ page, browser }) => {
  await login(page, accounts.instructor, undefined, '/giang-day/khoa-hoc')
  await settle(page)

  // 1. Tạo khóa học
  await page.getByRole('button', { name: 'Tạo khóa học' }).first().click()
  const dialog = page.getByRole('dialog', { name: 'Tạo khóa học' })
  await dialog.getByLabel('Tên khóa học').fill(TITLE)
  await dialog.getByLabel('Danh mục').selectOption({ label: 'Lập trình' })
  await frame(page, '45-tao-khoa-hoc')
  await dialog.getByRole('button', { name: 'Tạo và soạn nội dung' }).click()
  await expect(page.getByRole('heading', { level: 1, name: TITLE })).toBeVisible()

  // 2. Cập nhật thông tin và tải ảnh bìa
  await page.getByLabel('Mô tả ngắn').fill('Làm chủ Git và GitHub qua các bài thực hành: lưu lịch sử mã nguồn, làm việc nhóm, mở pull request.')
  await page.getByLabel(/Mô tả chi tiết/).fill('<p>Khóa học giúp bạn dùng Git hằng ngày và cộng tác trên GitHub một cách tự tin.</p><ul><li>Cài đặt và cấu hình Git</li><li>Nhánh, gộp nhánh, xử lý xung đột</li><li>Pull request và review mã</li></ul>')
  await page.getByLabel(/Học viên sẽ học được/).fill('Dùng thành thạo các lệnh Git cơ bản\nLàm việc nhóm với nhánh và pull request\nĐưa dự án lên GitHub')
  await page.getByLabel(/Yêu cầu/).fill('Biết sử dụng máy tính cơ bản')
  await page.getByLabel('Cấp độ').selectOption({ index: 0 })
  await page.getByLabel('Giá gốc (đồng)').fill('590000')
  await page.getByLabel('Giá khuyến mãi (đồng)').fill('349000')
  await page.getByLabel('Tải ảnh đại diện lên').setInputFiles(path.join(ASSETS, 'anh-bia-git.png'))
  await expect(page.getByAltText('Ảnh đại diện')).toBeVisible()
  await page.getByRole('heading', { name: 'Ảnh & video' }).scrollIntoViewIfNeeded()
  await settle(page)
  await frame(page, '46-cap-nhat-thong-tin')
  await page.getByRole('button', { name: 'Lưu thông tin' }).click()
  await expect(page.getByText('Đã lưu thông tin khóa học')).toBeVisible()
  await toastGone(page)

  // 3. Chương và bài học: tải video bài giảng
  await page.getByRole('tab', { name: 'Chương & bài học' }).click()
  await page.getByLabel('Chương mới').fill('Bắt đầu với Git')
  await page.getByRole('button', { name: 'Thêm chương' }).click()
  await expect(page.getByRole('heading', { name: 'Chương 1: Bắt đầu với Git' })).toBeVisible()
  await page.getByRole('button', { name: 'Thêm bài học' }).click()
  const lesson = page.getByRole('dialog', { name: 'Thêm bài học' })
  await lesson.getByLabel('Tên bài học').fill('Cài đặt và cấu hình Git')
  await lesson.getByLabel('Tải video lên').setInputFiles(path.join(ASSETS, 'bai-1-cai-dat-git.mp4'))
  await expect(lesson.getByText(/Đã có video tải lên/)).toBeVisible({ timeout: 30_000 })
  await lesson.getByLabel('Thời lượng (phút)').fill('8')
  await lesson.getByText('Cho học thử miễn phí').click()
  await page.waitForTimeout(500)
  await frame(page, '47-tai-video-bai-giang')
  await lesson.getByRole('button', { name: 'Lưu bài học' }).click()
  await expect(page.getByText('Đã lưu bài học')).toBeVisible()
  await toastGone(page)

  // 4. Tài liệu đính kèm của bài học
  await page.getByRole('button', { name: 'Sửa bài học' }).first().click()
  const edit = page.getByRole('dialog').last()
  await edit.getByLabel('Tải tài liệu lên').setInputFiles(path.join(ASSETS, 'huong-dan-cai-dat-git.pdf'))
  await expect(page.getByText('Đã thêm tài liệu')).toBeVisible()
  await edit.getByRole('heading', { name: 'Tài liệu đính kèm' }).scrollIntoViewIfNeeded()
  await page.waitForTimeout(500)
  await frame(page, '48-tai-tai-lieu')
  await page.keyboard.press('Escape')
  await toastGone(page)

  // Thêm bài thứ hai bằng URL video để đề cương có nhiều bài
  await page.getByRole('button', { name: 'Thêm bài học' }).click()
  const l2 = page.getByRole('dialog', { name: 'Thêm bài học' })
  await l2.getByLabel('Tên bài học').fill('Commit, nhánh và gộp nhánh')
  await l2.getByLabel('Hoặc dán URL video (https)').fill('https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4')
  await l2.getByLabel('Thời lượng (phút)').fill('12')
  await l2.getByRole('button', { name: 'Lưu bài học' }).click()
  await expect(page.getByText('Commit, nhánh và gộp nhánh')).toBeVisible()
  await toastGone(page)
  await frame(page, '49-de-cuong-giang-vien')

  // 5. Bài kiểm tra
  await page.getByRole('tab', { name: 'Bài kiểm tra' }).click()
  await page.getByRole('button', { name: 'Tạo bài kiểm tra' }).click()
  const quiz = page.getByRole('dialog', { name: 'Tạo bài kiểm tra' })
  await quiz.getByLabel('Tên bài kiểm tra').fill('Kiểm tra cuối khóa Git')
  await quiz.getByLabel('Nội dung câu hỏi').fill('Lệnh nào khởi tạo một kho Git mới?')
  await quiz.getByLabel('Nội dung đáp án 1').fill('git init')
  await quiz.getByLabel('Nội dung đáp án 2').fill('git start')
  await page.waitForTimeout(300)
  await frame(page, '50-soan-bai-kiem-tra')
  await quiz.getByRole('button', { name: 'Lưu bài kiểm tra' }).click()
  await expect(page.getByText('Kiểm tra cuối khóa Git')).toBeVisible()
  await toastGone(page)

  // 6. Gửi duyệt
  await page.getByRole('button', { name: 'Gửi duyệt' }).click()
  await expect(page.getByText(/Chờ duyệt/).first()).toBeVisible()
  await page.waitForTimeout(800)
  await frame(page, '51-gui-duyet')

  // 7. Admin duyệt, khóa học xuất hiện công khai
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 768 }, baseURL: 'http://localhost:5174', locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh' })
  const ad = await ctx.newPage()
  await login(ad, accounts.admin, undefined, '/quan-tri/khoa-hoc')
  await settle(ad)
  const row = ad.getByRole('row', { name: new RegExp(TITLE) })
  await expect(row).toBeVisible()
  await frame(ad, '52-admin-duyet-khoa-hoc')
  await row.getByRole('button', { name: 'Duyệt' }).click()
  await expect(ad.getByText('Đã duyệt và xuất bản')).toBeVisible()
  await ad.goto('/khoa-hoc?q=Git')
  await settle(ad)
  await ad.getByRole('link', { name: new RegExp(TITLE) }).first().click()
  await settle(ad)
  await frame(ad, '53-khoa-hoc-moi-cong-khai')
  await ctx.close()
})
