import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

// Tên miền công khai dùng trong index.html (%VITE_SITE_URL%) cho thẻ Open Graph/canonical.
// Khi build production đặt biến môi trường VITE_SITE_URL (docker-compose truyền PUBLIC_URL).
process.env.VITE_SITE_URL ??= 'http://localhost:5173'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  build: {
    rollupOptions: {
      output: {
        // Tách thư viện ít thay đổi ra chunk riêng: lần deploy sau trình duyệt vẫn dùng bản đã cache
        manualChunks(id: string) {
          if (!id.includes('node_modules')) return undefined
          if (/node_modules\/(react|react-dom|scheduler|react-router|react-router-dom)\//.test(id)) return 'react-vendor'
          if (/node_modules\/(axios|sonner|lucide-react)\//.test(id)) return 'ui-vendor'
          return undefined
        },
      },
    },
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Gọi /api/* từ client sẽ được chuyển tới ASP.NET Core API khi chạy dev
      '/uploads': { target: process.env.API_PROXY_TARGET ?? 'http://localhost:5080', changeOrigin: true },
      '/sitemap.xml': { target: process.env.API_PROXY_TARGET ?? 'http://localhost:5080', changeOrigin: true },
      '/api': {
        target: process.env.API_PROXY_TARGET ?? 'http://localhost:5080',
        changeOrigin: true,
        
      },
    },
  },
})
