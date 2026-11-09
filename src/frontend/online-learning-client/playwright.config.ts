import { defineConfig, devices } from '@playwright/test'
import os from 'node:os'
import path from 'node:path'

/**
 * Kiểm thử E2E: khởi động API thật (chế độ demo SQLite, DB mới mỗi lần chạy, dữ liệu mẫu)
 * và Vite dev server trên cổng riêng (5081/5174) để không đụng server đang dùng.
 *
 * Chạy:  npm run test:e2e
 * Biến môi trường:
 *   DOTNET       đường dẫn dotnet (mặc định "dotnet" trong PATH)
 *   E2E_CHANNEL  "chrome" | "msedge" để dùng trình duyệt đã cài; để trống thì dùng Chromium của Playwright (CI)
 */
const API_PORT = 5081
const WEB_PORT = 5174
const WEB_URL = `http://localhost:${WEB_PORT}`
export const DEMO_PASSWORD = 'E2e@Pass2026'
const dbFile = path.join(os.tmpdir(), `eduviet-e2e-${Date.now()}.db`)

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: WEB_URL,
    locale: 'vi-VN',
    timezoneId: 'Asia/Ho_Chi_Minh',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    channel: process.env.E2E_CHANNEL || undefined,
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], channel: process.env.E2E_CHANNEL || undefined }, testIgnore: /mobile.spec.ts/ },
    { name: 'mobile', use: { ...devices['Pixel 7'], channel: process.env.E2E_CHANNEL || undefined }, testMatch: /mobile\.spec\.ts/ },
  ],
  webServer: [
    {
      command: `"${process.env.DOTNET ?? 'dotnet'}" run --project ../../backend/src/OnlineLearning.API --no-launch-profile --urls http://localhost:${API_PORT}`,
      url: `http://localhost:${API_PORT}/api/health`,
      timeout: 240_000,
      reuseExistingServer: false,
      stdout: 'ignore',
      stderr: 'pipe',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        Database__Provider: 'SqliteDemo',
        ConnectionStrings__SqliteDemo: `Data Source=${dbFile}`,
        Jwt__SecretKey: 'e2e-only-secret-key-0123456789-abcdefghijklmnop',
        Seed__DemoPassword: DEMO_PASSWORD,
        Seed__RunOnStartup: 'true',
        BackgroundJobs__Enabled: 'false',
        RateLimiting__Enabled: 'false',
        Payments__TestGateway__Enabled: 'true',
        Payments__ReturnUrl: `${WEB_URL}/thanh-toan/ket-qua`,
        App__ClientUrl: WEB_URL,
        Storage__LocalRoot: path.join(os.tmpdir(), 'eduviet-e2e-storage'),
      },
    },
    {
      command: `npx vite --port ${WEB_PORT} --strictPort`,
      url: WEB_URL,
      timeout: 120_000,
      reuseExistingServer: false,
      env: { API_PROXY_TARGET: `http://localhost:${API_PORT}` },
    },
  ],
})
