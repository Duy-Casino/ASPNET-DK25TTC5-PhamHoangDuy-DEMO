import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
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
      '/api': {
        target: process.env.API_PROXY_TARGET ?? 'http://localhost:5080',
        changeOrigin: true,
        
      },
    },
  },
})
