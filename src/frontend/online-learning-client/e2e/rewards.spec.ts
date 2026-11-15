import { expect, test } from '@playwright/test'
import { accounts, login, registerStudent } from './helpers'

test.describe('Điểm thưởng', () => {
  test('học viên điểm danh, xem nhiệm vụ, lịch sử; chia sẻ khóa học tạo liên kết riêng; mã giới thiệu khi đăng ký', async ({ page, browser, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await registerStudent(page, 'Người Tích Điểm')
    await page.goto('/hoc-vien/diem-thuong')
    await expect(page.getByRole('heading', { level: 1, name: 'Điểm thưởng của tôi' })).toBeVisible()
    for (const name of ['Điểm danh hằng ngày', 'Nhiệm vụ nhận điểm', 'Chia sẻ & giới thiệu', 'Đổi điểm lấy mã giảm giá', 'Lịch sử cộng/trừ điểm']) {
      await expect(page.getByRole('heading', { name })).toBeVisible()
    }

    // Điểm danh: +5, nút khóa lại, lịch sử có dòng mới
    await page.getByRole('button', { name: 'Điểm danh +5' }).click()
    await expect(page.getByText(/Điểm danh thành công \+5 điểm/)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Đã điểm danh hôm nay' })).toBeDisabled()
    await expect(page.getByText(/Điểm danh ngày \d{2}\/\d{2}\/\d{4}/)).toBeVisible()
    await expect(page.getByText('Chuỗi 1 ngày', { exact: true })).toBeVisible()
    // Chưa đủ điểm thì không đổi được mã
    await expect(page.getByRole('button', { name: /10\.000đ\s*100 điểm/ })).toBeDisabled()

    // Mã giới thiệu riêng
    const code = (await page.locator('text=/^[2-9A-Z]{8}$/').first().textContent())!.trim()

    // Chia sẻ khóa học từ trang khóa học: liên kết riêng có ?s=token&ref=mã
    await page.goto('/khoa-hoc/excel-tu-co-ban-den-nang-cao')
    await page.getByRole('button', { name: 'Chia sẻ khóa học' }).click()
    const dialog = page.getByRole('dialog', { name: 'Chia sẻ khóa học' })
    await dialog.getByRole('button', { name: 'Sao chép' }).click()
    const link = await dialog.getByLabel('Liên kết của bạn').inputValue()
    expect(link).toMatch(new RegExp(`/khoa-hoc/excel-tu-co-ban-den-nang-cao\\?s=[2-9A-Z]{12}&ref=${code}$`))

    // Khách mở liên kết → trang báo backend một lượt mở và nhớ mã giới thiệu cho lúc đăng ký
    const guest = await browser.newContext()
    const guestPage = await guest.newPage()
    const opened = guestPage.waitForResponse((r) => r.url().includes('/api/rewards/shares/') && r.url().endsWith('/open'))
    await guestPage.goto(link)
    expect((await opened).status()).toBe(204)
    await guestPage.goto('/dang-ky')
    await expect(guestPage.getByText(`Bạn được giới thiệu bằng mã ${code}.`)).toBeVisible()
    await guest.close()
  })

  test('học viên chọn dùng điểm thưởng để giảm giá khi thanh toán', async ({ page }) => {
    await registerStudent(page, 'Người Dùng Điểm')
    await page.goto('/hoc-vien/diem-thuong')
    await page.getByRole('button', { name: 'Điểm danh +5' }).click()
    await expect(page.getByRole('button', { name: 'Đã điểm danh hôm nay' })).toBeDisabled()
    await expect(page.getByText(/Hoặc trừ điểm trực tiếp khi thanh toán/)).toBeVisible()

    await page.goto('/khoa-hoc/excel-tu-co-ban-den-nang-cao')
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click()
    await expect(page.getByRole('link', { name: 'Giỏ hàng, 1 khóa học' })).toBeVisible()
    await page.goto('/thanh-toan')
    const receipt = page.getByRole('complementary', { name: 'Hóa đơn' })
    await expect(receipt.getByText('249.000đ').last()).toBeVisible()

    // Mặc định không dùng điểm; bật lên thì dùng tối đa (5 điểm = 500đ)
    const toggle = page.getByRole('checkbox', { name: /Dùng điểm thưởng để giảm giá/ })
    await expect(toggle).not.toBeChecked()
    await page.getByText('Dùng điểm thưởng để giảm giá').click()
    await expect(toggle).toBeChecked()
    await expect(receipt.getByText('Điểm thưởng (5 điểm)')).toBeVisible()
    await expect(receipt.getByText('248.500đ')).toBeVisible()

    // Nhập quá mức tối đa → báo lỗi, quay về mức tối đa
    await page.getByLabel(/Số điểm sử dụng/).fill('50')
    await page.getByRole('button', { name: 'Áp dụng' }).last().click()
    await expect(page.getByText('Bạn chỉ dùng được tối đa 5 điểm cho đơn hàng này.')).toBeVisible()

    // Tắt đi thì giá trở lại như cũ; bật lại rồi đặt hàng
    await page.getByText('Dùng điểm thưởng để giảm giá').click()
    await expect(receipt.getByText('Điểm thưởng (5 điểm)')).toHaveCount(0)
    await page.getByText('Dùng điểm thưởng để giảm giá').click()
    await expect(receipt.getByText('Điểm thưởng (5 điểm)')).toBeVisible()
    await page.getByLabel(/Kênh thanh toán thử nghiệm/).check()
    await page.getByRole('button', { name: 'Đặt hàng và thanh toán' }).click()
    await page.waitForURL(/\/thanh-toan\/ket-qua/)
    await expect(page.getByRole('heading', { name: 'Thanh toán thành công' })).toBeVisible()

    // Lịch sử điểm: trừ 5 điểm cho đơn, cộng điểm mua hàng trên số tiền thật (248.500đ → 24 điểm)
    await page.goto('/hoc-vien/diem-thuong')
    await expect(page.getByText(/Dùng 5 điểm giảm 500đ cho đơn/)).toBeVisible()
    await expect(page.getByText(/Mua khóa học, đơn .* \(248\.500đ\)/)).toBeVisible()
  })

  test('admin xem tổng quan, cấu hình nhiệm vụ, điều chỉnh điểm có lý do và xem lịch sử', async ({ page, browser }) => {
    // Tạo học viên có giao dịch điểm
    const studentCtx = await browser.newContext()
    const student = await studentCtx.newPage()
    const email = await registerStudent(student, 'Học Viên Được Thưởng')
    await student.goto('/hoc-vien/diem-thuong')
    await student.getByRole('button', { name: 'Điểm danh +5' }).click()
    await expect(student.getByRole('button', { name: 'Đã điểm danh hôm nay' })).toBeVisible()

    await login(page, accounts.admin, undefined, '/quan-tri/diem-thuong')
    await expect(page.getByRole('heading', { level: 1, name: 'Điểm thưởng' })).toBeVisible()
    await expect(page.getByText('Tổng điểm đã phát hành')).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Nhiệm vụ được thực hiện nhiều nhất' })).toBeVisible()

    // Nhiệm vụ: sửa số điểm điểm danh rồi đặt lại
    await page.getByRole('tab', { name: 'Nhiệm vụ & quy tắc' }).click()
    const checkInRow = page.getByRole('row', { name: /Điểm danh hằng ngày/ })
    await checkInRow.getByRole('button', { name: 'Sửa' }).click()
    const ruleDialog = page.getByRole('dialog', { name: /Sửa: Điểm danh hằng ngày/ })
    await ruleDialog.getByLabel('Số điểm').fill('6')
    await ruleDialog.getByRole('button', { name: 'Lưu' }).click()
    await expect(checkInRow.getByRole('cell', { name: '6', exact: true })).toBeVisible()
    await checkInRow.getByRole('button', { name: 'Sửa' }).click()
    await page.getByRole('dialog').getByLabel('Số điểm').fill('5')
    await page.getByRole('dialog').getByRole('button', { name: 'Lưu' }).click()
    await expect(checkInRow.getByRole('cell', { name: '5', exact: true })).toBeVisible()

    // Điều chỉnh điểm: thiếu lý do bị chặn, đủ lý do thì thành công
    await page.getByRole('tab', { name: 'Điểm học viên' }).click()
    await page.getByLabel('Tìm học viên').fill(email)
    const row = page.getByRole('row', { name: /Học Viên Được Thưởng/ })
    await row.getByRole('button', { name: 'Điều chỉnh' }).click()
    const adjust = page.getByRole('dialog', { name: /Điều chỉnh điểm/ })
    await adjust.getByLabel(/Số điểm/).fill('50')
    await adjust.getByRole('button', { name: 'Xác nhận điều chỉnh' }).click()
    await expect(adjust.getByText('Bắt buộc ghi lý do (ít nhất 5 ký tự).')).toBeVisible()
    await adjust.getByLabel(/Lý do/).fill('Thưởng hoạt động tích cực')
    await adjust.getByRole('button', { name: 'Xác nhận điều chỉnh' }).click()
    await expect(page.getByText(/Đã cộng 50 điểm cho Học Viên Được Thưởng/)).toBeVisible()
    await expect(row.getByRole('cell', { name: '55', exact: true }).first()).toBeVisible()

    // Lịch sử theo học viên
    await row.getByRole('button', { name: 'Lịch sử' }).click()
    await expect(page.getByText('Điều chỉnh thủ công: Thưởng hoạt động tích cực')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Xuất CSV' })).toBeVisible()

    // Học viên thấy điểm và lý do
    await student.reload()
    await expect(student.getByText('Điều chỉnh thủ công: Thưởng hoạt động tích cực')).toBeVisible()
    await studentCtx.close()
  })
})
