import { expect, test, type Page } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

/** Học viên mua khóa học qua kênh thử nghiệm, trả về mã đơn. */
async function buyCourse(page: Page, slug: string): Promise<string> {
  await page.goto(`/khoa-hoc/${slug}`)
  await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).filter({ visible: true }).click()
  await expect(page.getByRole('link', { name: 'Giỏ hàng, 1 khóa học' })).toBeVisible()
  await page.goto('/thanh-toan')
  await page.getByLabel(/Kênh thanh toán thử nghiệm/).check()
  await page.getByRole('button', { name: 'Đặt hàng và thanh toán' }).click()
  await page.waitForURL(/\/thanh-toan\/ket-qua/)
  await expect(page.getByRole('heading', { name: 'Thanh toán thành công' })).toBeVisible()
  await page.goto('/tai-khoan/don-hang')
  const link = page.getByRole('link', { name: /^EDV/ }).first()
  const code = (await link.textContent())!.trim()
  await link.click()
  await expect(page).toHaveURL(/\/don-hang\//)
  return code
}

test.describe('Vận hành bán hàng', () => {
  test('biên nhận PDF, yêu cầu hoàn tiền, Admin duyệt; giảng viên xem thu nhập, rút tiền, Admin xác nhận chi; xuất CSV', async ({ page, browser }) => {
    // ---- Học viên: mua, tải biên nhận, gửi yêu cầu hoàn tiền
    await registerStudent(page, 'Người Muốn Hoàn Tiền')
    const code = await buyCourse(page, 'sql-server-cho-lap-trinh-vien')

    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: 'Tải biên nhận (PDF)' }).click()
    expect((await download).suggestedFilename()).toBe(`bien-nhan-${code}.pdf`)

    const refund = page.getByRole('region', { name: 'Hoàn tiền' })
    await expect(refund.getByText(/nếu học không quá 20% nội dung/)).toBeVisible()
    await refund.getByRole('button', { name: 'Yêu cầu hoàn tiền' }).click()
    await refund.getByLabel('Lý do yêu cầu hoàn tiền').fill('Tôi mua nhầm khóa học, muốn đổi sang khóa khác.')
    await refund.getByRole('button', { name: 'Gửi yêu cầu' }).click()
    await expect(refund.getByText('Chờ xử lý')).toBeVisible()
    await expect(refund.getByRole('button', { name: 'Yêu cầu hoàn tiền' })).toHaveCount(0)

    // ---- Admin: duyệt yêu cầu hoàn tiền
    const adminCtx = await browser.newContext()
    const admin = await adminCtx.newPage()
    await login(admin, accounts.admin, undefined, '/quan-tri/hoan-tien')
    await admin.goto('/quan-tri/hoan-tien')
    await expect(admin.getByRole('heading', { level: 1, name: 'Yêu cầu hoàn tiền' })).toBeVisible()
    const row = admin.getByRole('row').filter({ hasText: code })
    await expect(row.getByText('Tôi mua nhầm khóa học')).toBeVisible()
    await row.getByRole('button', { name: 'Hoàn tiền' }).click()
    const dialog = admin.getByRole('dialog', { name: `Hoàn tiền đơn ${code}` })
    await dialog.getByRole('button', { name: 'Xác nhận đã hoàn tiền' }).click()
    await expect(admin.getByText(`Đã hoàn tiền đơn ${code}`)).toBeVisible()

    // Học viên thấy đơn đã hoàn tiền
    await page.reload()
    await expect(page.getByRole('region', { name: 'Hoàn tiền' }).getByText('Đã hoàn tiền')).toBeVisible()

    // ---- Giảng viên: sổ thu nhập có dòng bán và dòng hoàn của đơn; gửi yêu cầu rút tiền
    const teacherCtx = await browser.newContext()
    const teacher = await teacherCtx.newPage()
    await login(teacher, accounts.instructor, undefined, '/giang-day/doanh-thu')
    await teacher.goto('/giang-day/doanh-thu')
    await expect(teacher.getByRole('heading', { level: 1, name: 'Doanh thu và rút tiền' })).toBeVisible()
    await expect(teacher.getByText('Có thể rút')).toBeVisible()
    await expect(teacher.getByRole('row').filter({ hasText: code })).toHaveCount(2)
    await expect(teacher.getByRole('row').filter({ hasText: code }).getByText('Hoàn tiền')).toBeVisible()

    await teacher.getByRole('button', { name: 'Rút tiền' }).click()
    const payout = teacher.getByRole('dialog', { name: 'Rút tiền về tài khoản ngân hàng' })
    await payout.getByLabel('Số tiền (đ)').fill('150000')
    await payout.getByLabel('Ngân hàng').fill('Vietcombank')
    await payout.getByLabel('Số tài khoản').fill('0123456789')
    await payout.getByLabel('Chủ tài khoản').fill('Nguyen Minh Khoa')
    await payout.getByRole('button', { name: 'Gửi yêu cầu' }).click()
    await expect(teacher.getByText(/Đã gửi yêu cầu rút tiền/)).toBeVisible()
    await expect(teacher.getByRole('row').filter({ hasText: '150.000đ' }).getByText('Chờ xử lý')).toBeVisible()
    await expect(teacher.getByRole('button', { name: 'Rút tiền' })).toBeDisabled()

    // ---- Admin: xác nhận đã chuyển khoản
    await admin.goto('/quan-tri/chi-tra')
    await expect(admin.getByRole('heading', { level: 1, name: 'Doanh thu và chi trả giảng viên' })).toBeVisible()
    await expect(admin.getByText('Doanh thu nền tảng', { exact: true })).toBeVisible()
    const payoutRow = admin.getByRole('row').filter({ hasText: 'NGUYEN MINH KHOA' }).filter({ hasText: 'Chờ xử lý' })
    await payoutRow.getByRole('button', { name: 'Đã chuyển' }).click()
    const confirm = admin.getByRole('dialog', { name: 'Xác nhận đã chuyển khoản' })
    await confirm.getByLabel('Mã giao dịch chuyển khoản').fill('FT26329E2E001')
    await confirm.getByRole('button', { name: 'Xác nhận đã chuyển' }).click()
    await expect(admin.getByText(/Đã ghi nhận chi 150\.000đ/)).toBeVisible()

    await teacher.reload()
    await expect(teacher.getByRole('row').filter({ hasText: 'FT26329E2E001' }).getByText('Đã chi')).toBeVisible()

    // ---- Admin: xuất đơn hàng ra CSV
    await admin.goto('/quan-tri/don-hang')
    const csv = admin.waitForEvent('download')
    await admin.getByRole('button', { name: 'Xuất CSV' }).click()
    expect((await csv).suggestedFilename()).toMatch(/^don-hang-\d{4}-\d{2}-\d{2}\.csv$/)

    await adminCtx.close()
    await teacherCtx.close()
  })
})
