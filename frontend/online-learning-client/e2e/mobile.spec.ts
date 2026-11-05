import { expect, test } from '@playwright/test'
import { expectNoHorizontalOverflow } from './helpers'

// Chạy ở project "mobile" (Pixel 7)
test.describe('Điện thoại', () => {
  for (const path of ['/', '/khoa-hoc', '/khoa-hoc/excel-tu-co-ban-den-nang-cao', '/xac-minh', '/dang-nhap', '/dang-ky']) {
    test(`không tràn ngang: ${path}`, async ({ page }) => {
      await page.goto(path)
      await page.waitForLoadState('networkidle')
      await expect(page.locator('h1').first()).toBeVisible()
      await expectNoHorizontalOverflow(page)
    })
  }

  test('menu và bộ lọc dạng sheet hoạt động', async ({ page }) => {
    await page.goto('/')
    await page.getByRole('button', { name: 'Mở menu' }).click()
    await expect(page.getByRole('link', { name: 'Xác minh chứng chỉ' }).first()).toBeVisible()

    await page.goto('/khoa-hoc')
    await page.getByRole('button', { name: 'Bộ lọc' }).click()
    const sheet = page.getByRole('dialog', { name: 'Bộ lọc' })
    await expect(sheet).toBeVisible()
    await sheet.getByLabel('Cơ bản').click()
    await sheet.getByRole('button', { name: 'Xong' }).click()
    await expect(page).toHaveURL(/level=Beginner/)
  })
})
