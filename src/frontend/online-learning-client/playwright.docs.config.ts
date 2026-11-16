import { defineConfig, devices } from '@playwright/test'
import base from './playwright.config'

/**
 * Chụp ảnh màn hình các chức năng cho báo cáo đồ án (thesis/doc/hinh/anh-*.png).
 * Dùng chung API + Vite + DB mới với kiểm thử E2E (cổng 5081/5174, dữ liệu mẫu).
 *
 * Chạy:  npx playwright test -c playwright.docs.config.ts
 */
export default defineConfig({
  ...base,
  testDir: './docs-shots',
  timeout: 15 * 60_000,
  retries: 0,
  reporter: [['list']],
  projects: [
    {
      name: 'docs',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1366, height: 768 }, channel: process.env.E2E_CHANNEL || undefined },
    },
  ],
})
