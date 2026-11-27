import { expect, test, type Page } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

/** Thanh toán qua kênh thử nghiệm ở trang /thanh-toan, rồi mở đơn mới nhất. */
async function payAndOpenOrder(page: Page) {
  await page.getByLabel(/Kênh thanh toán thử nghiệm/).check()
  await page.getByRole('button', { name: 'Đặt hàng và thanh toán' }).click()
  await page.waitForURL(/\/thanh-toan\/ket-qua/)
  await expect(page.getByRole('heading', { name: 'Thanh toán thành công' })).toBeVisible()
  await page.goto('/tai-khoan/don-hang')
  await page.getByRole('link', { name: /^EDV/ }).first().click()
  await expect(page).toHaveURL(/\/don-hang\//)
}

test.describe('Tăng doanh số', () => {
  test('yêu thích, gợi ý, flash sale, combo, mua tặng và nhận quà; trang quản lý combo và khuyến mãi', async ({ page, browser }) => {
    await registerStudent(page, 'Người Mua Tặng')

    // ---- Yêu thích: bấm trái tim trên thẻ khóa học → có trong trang Yêu thích
    await page.goto('/khoa-hoc')
    await page.getByRole('button', { name: 'Thêm "SQL Server cho lập trình viên" vào yêu thích' }).click()
    await expect(page.getByRole('button', { name: 'Bỏ "SQL Server cho lập trình viên" khỏi yêu thích' })).toHaveAttribute('aria-pressed', 'true')
    await expect(page.getByText('Đã thêm "SQL Server cho lập trình viên" vào yêu thích')).toBeVisible()   // đã lưu ở máy chủ
    await page.goto('/hoc-vien/yeu-thich')
    await expect(page.getByRole('heading', { level: 1, name: 'Khóa học yêu thích' })).toBeVisible()
    await expect(page.getByRole('link', { name: /SQL Server cho lập trình viên/ })).toBeVisible()

    // ---- Gợi ý + flash sale ở trang chi tiết
    await page.goto('/khoa-hoc/sql-server-cho-lap-trinh-vien')
    await expect(page.getByRole('heading', { name: 'Học viên cũng quan tâm' })).toBeVisible()
    await page.goto('/khoa-hoc/photoshop-co-ban-cho-nguoi-di-lam')
    await expect(page.getByRole('timer').filter({ visible: true }).first()).toContainText('Flash sale kết thúc sau')

    // ---- Combo: mua trọn bộ, giá combo chia cho từng khóa
    await page.goto('/combo')
    await page.getByRole('link', { name: /Phân tích dữ liệu văn phòng/ }).click()
    await expect(page.getByText('Tiết kiệm', { exact: false }).first()).toBeVisible()
    await page.getByRole('button', { name: 'Mua combo' }).click()
    await expect(page).toHaveURL(/\/thanh-toan\?combo=/)
    await expect(page.getByRole('complementary', { name: 'Hóa đơn' }).getByText('990.000đ').last()).toBeVisible()
    await payAndOpenOrder(page)
    await expect(page.getByText('Mua theo combo: Phân tích dữ liệu văn phòng')).toBeVisible()
    await page.goto('/combo/combo-phan-tich-du-lieu-van-phong')
    await expect(page.getByText('Bạn đã sở hữu toàn bộ combo')).toBeVisible()

    // ---- Mua tặng: người mua không được ghi danh, người nhận nhận bằng đúng email
    const recipient = `qua${Date.now()}@test.local`
    await page.goto('/khoa-hoc/sql-server-cho-lap-trinh-vien')
    await page.getByRole('button', { name: 'Mua tặng người khác' }).filter({ visible: true }).click()
    await expect(page.getByRole('heading', { level: 1, name: 'Thanh toán quà tặng' })).toBeVisible()
    await page.getByLabel('Email người nhận').fill(recipient)
    await page.getByLabel('Tên người nhận').fill('Bạn Thân')
    await page.getByLabel('Lời nhắn (không bắt buộc)').fill('Chúc bạn học tốt!')
    await payAndOpenOrder(page)
    const gift = page.getByRole('region', { name: 'Quà tặng' })
    await expect(gift.getByText('Đã gửi, chờ nhận')).toBeVisible()
    const redeemUrl = await gift.getByLabel('Liên kết nhận quà').inputValue()
    const code = redeemUrl.split('/qua-tang/')[1]

    const friendCtx = await browser.newContext()
    const friend = await friendCtx.newPage()
    await friend.goto(`/qua-tang/${code}`)
    await expect(friend.getByRole('heading', { level: 1 })).toContainText('tặng bạn một khóa học')
    await expect(friend.getByText('“Chúc bạn học tốt!”')).toBeVisible()
    await friend.getByRole('link', { name: 'Đăng ký tài khoản' }).click()
    await friend.getByLabel('Họ và tên').fill('Bạn Thân')
    await friend.getByLabel('Email').fill(recipient)
    await friend.getByLabel('Mật khẩu', { exact: true }).fill('Matkhau2026')
    await friend.getByLabel('Nhập lại mật khẩu').fill('Matkhau2026')
    await friend.getByRole('button', { name: 'Đăng ký' }).click()
    await friend.waitForURL(new RegExp(`/qua-tang/${code}$`))
    await friend.getByRole('button', { name: 'Nhận quà và vào học' }).click()
    await friend.waitForURL(/\/hoc-vien$/)
    await expect(friend.getByText('SQL Server cho lập trình viên').first()).toBeVisible()
    await friendCtx.close()

    await page.reload()
    await expect(page.getByRole('region', { name: 'Quà tặng' }).getByText('Đã nhận')).toBeVisible()

    // ---- Admin: trang combo; giảng viên: ô khuyến mãi có thời hạn trong trang soạn khóa học
    const adminCtx = await browser.newContext()
    const admin = await adminCtx.newPage()
    await login(admin, accounts.admin, undefined, '/quan-tri/combo')
    await admin.goto('/quan-tri/combo')
    await expect(admin.getByRole('heading', { level: 1, name: 'Combo khóa học' })).toBeVisible()
    await expect(admin.getByRole('row').filter({ hasText: 'Phân tích dữ liệu văn phòng' })).toBeVisible()
    await adminCtx.close()

    const teacherCtx = await browser.newContext()
    const teacher = await teacherCtx.newPage()
    await login(teacher, accounts.instructor, undefined, '/giang-day/khoa-hoc')
    await teacher.goto('/giang-day/khoa-hoc')
    await teacher.getByRole('link', { name: 'ReactJS và TypeScript cho người mới' }).first().click()
    await expect(teacher.getByRole('region', { name: 'Khuyến mãi có thời hạn' })).toContainText('Chưa có khuyến mãi')
    await teacherCtx.close()
  })
})
