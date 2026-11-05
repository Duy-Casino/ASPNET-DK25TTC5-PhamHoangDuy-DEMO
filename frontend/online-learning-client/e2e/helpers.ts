import { expect, type Page } from '@playwright/test'

export const PASSWORD = 'E2e@Pass2026'

export const accounts = {
  admin: 'admin@eduviet.local',
  instructor: 'giangvien@eduviet.local',
  student: 'hocvien@eduviet.local',
}

export async function login(page: Page, email: string, password = PASSWORD, next = '/') {
  await page.goto(`/dang-nhap?next=${encodeURIComponent(next)}`)
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Đăng nhập' }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/dang-nhap'))
}

/** Đăng ký tài khoản học viên mới (email ngẫu nhiên) và trả về email. */
export async function registerStudent(page: Page, fullName = 'Học Viên E2E') {
  const email = `e2e${Date.now()}${Math.floor(Math.random() * 1000)}@test.local`
  await page.goto('/dang-ky')
  await page.getByLabel('Họ và tên').fill(fullName)
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Mật khẩu', { exact: true }).fill('Matkhau2026')
  await page.getByLabel('Nhập lại mật khẩu').fill('Matkhau2026')
  await page.getByRole('button', { name: 'Đăng ký' }).click()
  await page.waitForURL((url) => url.pathname === '/')
  await expect(page.getByRole('button', { name: new RegExp(`Tài khoản của ${fullName}`) })).toBeVisible()
  return email
}

/** Không có thanh cuộn ngang (bố cục vỡ trên màn hình hẹp). */
export async function expectNoHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(1)
}
