import { expect, test } from '@playwright/test'

test.describe('Trang công khai', () => {
  test('trang chủ hiển thị hero chứng chỉ và các khu vực chính', async ({ page }) => {
    await page.goto('/')
    await expect(page.getByRole('heading', { level: 1, name: 'Học online, nhận chứng chỉ thật' })).toBeVisible()

    // Thứ khách tương tác được: gõ tên → in lên chứng chỉ
    await page.getByLabel('Thử in tên bạn lên chứng chỉ').fill('Phạm Gia Bảo')
    await expect(page.getByText('Phạm Gia Bảo', { exact: true })).toBeVisible()
    await expect(page.getByText(/^EDV-\d{4}-[2-9A-Z]{8}$/)).toBeVisible()

    for (const name of ['Học theo lĩnh vực', 'Khóa học nổi bật', 'Học viên chọn nhiều nhất', 'Thư viện đang mở', 'Học viên nói gì']) {
      await expect(page.getByRole('heading', { name })).toBeVisible()
    }
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
