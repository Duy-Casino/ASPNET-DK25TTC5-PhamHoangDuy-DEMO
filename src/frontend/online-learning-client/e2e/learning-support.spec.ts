import { expect, test } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

const COURSE = 'ky-nang-thuyet-trinh-chuyen-nghiep'   // khóa miễn phí, bài kiểm tra cuối khóa tối đa 3 lượt

test.describe('Quản lý học tập và hỗ trợ', () => {
  test('hết lượt bài kiểm tra cuối khóa → xin thêm lượt → Admin cấp từ trang tiến độ; báo sự cố và Admin phản hồi', async ({ page, browser }) => {
    page.on('dialog', (d) => d.accept())   // xác nhận "còn câu chưa trả lời, vẫn nộp?"
    await registerStudent(page, 'Người Thi Lại')
    await page.goto(`/khoa-hoc/${COURSE}`)
    await page.getByRole('button', { name: 'Đăng ký học miễn phí' }).filter({ visible: true }).click()
    await page.getByRole('button', { name: 'Hoàn tất đăng ký' }).click()
    await expect(page).toHaveURL(/\/don-hang\//)

    // ---- Làm bài cuối khóa 3 lần đều chưa đạt (nộp bài trống)
    await page.goto(`/hoc/${COURSE}`)
    await page.getByRole('link', { name: /Bài kiểm tra cuối khóa/ }).first().click()
    for (let i = 1; i <= 3; i++) {
      await page.getByRole('button', { name: i === 1 ? 'Bắt đầu làm bài' : 'Làm lại' }).click()
      await page.getByRole('button', { name: 'Nộp bài' }).click()
      await expect(page.getByText(`Chưa đạt — lượt ${i}`)).toBeVisible()
      if (i < 3) await expect(page.getByRole('region', { name: 'Bước tiếp theo' })).toContainText(`còn ${3 - i} lượt`)
    }

    // ---- Hết lượt: hướng dẫn bước tiếp theo và gửi yêu cầu cấp thêm lượt
    const exhausted = page.getByRole('region', { name: 'Đã hết lượt làm bài' })
    await expect(exhausted).toContainText('Bạn đã dùng hết 3 lượt nhưng chưa đạt')
    await expect(page.getByRole('button', { name: 'Làm lại' })).toHaveCount(0)
    await exhausted.getByRole('button', { name: 'Gửi yêu cầu cấp thêm lượt' }).click()
    await exhausted.getByRole('button', { name: 'Gửi yêu cầu' }).click()
    await expect(exhausted.getByText('Đã gửi yêu cầu cấp thêm lượt — đang chờ xử lý')).toBeVisible()

    // ---- Báo sự cố khi học
    await page.getByRole('button', { name: 'Báo sự cố' }).click()
    const report = page.getByRole('dialog', { name: 'Báo sự cố khi học' })
    await report.getByLabel('Mô tả').fill('Video bài 2 dừng ở phút thứ 3, đã thử tải lại trang.')
    await report.getByRole('button', { name: 'Gửi báo cáo' }).click()
    await expect(report.getByText('Đang chờ xử lý').first()).toBeVisible()
    await report.getByRole('button', { name: 'Đóng' }).last().click()

    // ---- Admin: trang Tiến độ học tập, lọc "Hết lượt, chưa đạt", cấp thêm lượt
    const adminCtx = await browser.newContext()
    const admin = await adminCtx.newPage()
    await login(admin, accounts.admin, undefined, '/quan-tri/hoc-tap')
    await admin.goto('/quan-tri/hoc-tap')
    await expect(admin.getByRole('heading', { level: 1, name: 'Tiến độ học tập' })).toBeVisible()
    await admin.getByRole('tab', { name: /Hết lượt, chưa đạt/ }).click()
    const row = admin.getByRole('row').filter({ hasText: 'Người Thi Lại' })
    await expect(row.getByText('Hết lượt, chưa đạt')).toBeVisible()
    await row.getByRole('link', { name: 'Chi tiết' }).click()
    await expect(admin.getByRole('heading', { level: 1, name: 'Người Thi Lại' })).toBeVisible()
    await expect(admin.getByRole('list', { name: 'Dòng thời gian hoạt động' }).getByText(/Chưa đạt "Bài kiểm tra cuối khóa"/)).toHaveCount(3)
    await admin.getByRole('button', { name: 'Cấp thêm lượt' }).click()
    const grant = admin.getByRole('dialog', { name: /Cấp thêm lượt/ })
    await grant.getByLabel('Lý do (ghi vào nhật ký, bắt buộc)').fill('Học viên đã ôn tập lại toàn bộ khóa học')
    await grant.getByRole('button', { name: 'Xác nhận' }).click()
    await expect(admin.getByText('Đã thực hiện và ghi nhật ký')).toBeVisible()
    await expect(admin.getByText('(cấp thêm 1)')).toBeVisible()

    // ---- Admin phản hồi báo cáo sự cố video
    await admin.goto('/quan-tri/ho-tro-hoc-tap')
    const req = admin.getByRole('listitem').filter({ hasText: 'Video bài 2 dừng ở phút thứ 3' })
    await req.getByRole('button', { name: 'Phản hồi' }).click()
    const respond = admin.getByRole('dialog', { name: /Phản hồi/ })
    await respond.getByLabel('Phản hồi gửi học viên').fill('Đã kiểm tra máy chủ video, bạn tải lại trang là xem được.')
    await respond.getByRole('button', { name: 'Gửi phản hồi' }).click()
    await expect(admin.getByText('Đã phản hồi học viên')).toBeVisible()
    await adminCtx.close()

    // ---- Học viên làm tiếp được
    await page.reload()
    await expect(page.getByRole('button', { name: 'Làm lại' })).toBeVisible()
    await expect(page.getByText('3/4', { exact: true })).toBeVisible()
  })
})
