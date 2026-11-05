import { expect, test } from '@playwright/test'

test.describe('Trang công khai', () => {
  test('trang chủ kiểu sàn: banner, các hàng khóa học, thanh lĩnh vực', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1, name: 'Khóa học trực tuyến có chứng chỉ' })).toBeVisible()

    for (const name of ['Khóa học đặc biệt', 'Xu hướng tuần này', 'Mới xuất bản', 'Giảng viên nổi bật', 'Xác minh chứng chỉ EduViet']) {
      await expect(page.getByRole('heading', { name })).toBeVisible()
    }

    // Banner: nút "tiếp theo" chuyển sang trang banner kế
    const next = page.getByRole('button', { name: 'Banner tiếp theo' })
    await next.click()
    await expect(page.getByRole('button', { name: 'Trang banner 2' })).toHaveAttribute('aria-current', 'true')

    // Thanh lĩnh vực dẫn tới trang khóa học đã lọc
    await page.getByRole('navigation', { name: 'Lĩnh vực' }).getByRole('link', { name: 'Lập trình' }).click()
    await expect(page).toHaveURL(/category=lap-trinh/)
    await expect(page.getByRole('navigation', { name: 'Lĩnh vực' }).getByRole('link', { name: 'Lập trình' })).toHaveAttribute('aria-current', 'page')

    // Ô xác minh ở trang chủ
    await page.goto('/')
    await page.getByLabel('Mã chứng chỉ').fill('edv-2026-khongco1')
    await page.getByRole('button', { name: 'Kiểm tra' }).click()
    await expect(page).toHaveURL(/\/xac-minh\/EDV-2026-KHONGCO1/)
  })

  test('tìm kiếm, lọc và mở chi tiết khóa học', async ({ page }) => {
    await page.goto('/')
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
