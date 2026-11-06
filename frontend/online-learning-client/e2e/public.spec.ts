import { expect, test } from '@playwright/test'

test.describe('Trang công khai', () => {
  test('trang chủ: hero, các section, tab lĩnh vực, menu khóa học', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1, name: /Học kỹ năng thực tế/ })).toBeVisible()

    for (const name of ['Học trực tuyến nghiêm túc, có kết quả rõ ràng', 'Khóa học cho mọi lĩnh vực', 'Khóa học được chọn lọc',
      'Từ lúc chọn khóa đến khi có chứng chỉ', 'Học viên nói gì về EduViet', 'Đội ngũ đứng lớp', 'Chứng chỉ ai cũng kiểm tra được']) {
      await expect(page.getByRole('heading', { name })).toBeAttached()
    }

    // Tab lĩnh vực: chọn tab thứ hai → bảng nội dung đổi theo, nút dẫn tới trang đã lọc
    const tabs = page.getByRole('tablist', { name: 'Lĩnh vực' }).getByRole('tab')
    const second = tabs.nth(1)
    const secondName = (await second.textContent())!.trim()
    await second.click()
    await expect(second).toHaveAttribute('aria-selected', 'true')
    await expect(page.getByRole('tabpanel').getByRole('heading', { level: 3 })).toHaveText(secondName)

    // Menu "Khóa học" trên header liệt kê lĩnh vực
    await page.getByRole('navigation', { name: 'Chính' }).getByRole('button', { name: 'Khóa học' }).click()
    await page.locator('#courses-menu').getByRole('link', { name: /Lập trình/ }).click()
    await expect(page).toHaveURL(/category=lap-trinh/)
    await expect(page.getByRole('heading', { level: 1, name: 'Lập trình' })).toBeVisible()

    // Ô xác minh ở trang chủ
    await page.goto('/')
    await page.getByLabel('Mã chứng chỉ').fill('edv-2026-khongco1')
    await page.getByRole('button', { name: 'Kiểm tra' }).click()
    await expect(page).toHaveURL(/\/xac-minh\/EDV-2026-KHONGCO1/)
  })

  test('tìm kiếm, lọc và mở chi tiết khóa học', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: 'Mở tìm kiếm' }).click()
    await page.getByRole('searchbox', { name: 'Tìm khóa học' }).fill('Excel')
    await page.keyboard.press('Enter')
    await expect(page).toHaveURL(/\/khoa-hoc\?keyword=Excel/)
    await expect(page.getByRole('heading', { level: 1 })).toContainText('Excel')

    await page.getByRole('link', { name: /Excel từ cơ bản đến nâng cao/ }).first().click()
    await expect(page.getByRole('heading', { level: 1, name: 'Excel từ cơ bản đến nâng cao' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Nội dung khóa học' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Mua ngay' })).toBeVisible()

    // Lọc theo lĩnh vực + sắp xếp, trạng thái nằm trên URL
    await page.goto('/khoa-hoc')
    await page.getByRole('complementary', { name: 'Bộ lọc' }).getByLabel(/Lập trình/).click()
    await expect(page).toHaveURL(/category=lap-trinh/)
    await page.getByLabel('Sắp xếp').selectOption('price_asc')
    await expect(page).toHaveURL(/sort=price_asc/)
  })

  test('mua khi chưa đăng nhập → chuyển tới đăng nhập và quay lại', async ({ page }) => {
    await page.goto('/khoa-hoc/excel-tu-co-ban-den-nang-cao')
    await page.getByRole('button', { name: 'Mua ngay' }).click()
    await expect(page).toHaveURL(/\/dang-nhap\?next=%2Fkhoa-hoc%2Fexcel/)
  })

  test('xác minh chứng chỉ: mã không tồn tại', async ({ page }) => {
    await page.goto('/xac-minh')
    await page.getByLabel('Mã chứng chỉ').fill('EDV-2026-ZZZZZZZZ')
    await page.getByRole('button', { name: 'Kiểm tra' }).click()
    await expect(page.getByRole('heading', { name: 'Không tìm thấy chứng chỉ' })).toBeVisible()
  })

  test('khu vực quản trị yêu cầu đăng nhập', async ({ page }) => {
    await page.goto('/quan-tri')
    await expect(page).toHaveURL(/\/dang-nhap\?next=%2Fquan-tri/)
  })
})
