import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// /api/auth и /api/users — сервис авторизации :5167; остальной /api — закупочный API :5165.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173,
    proxy: {
      '/api/auth': { target: 'http://localhost:5167', changeOrigin: true },
      '/api/users': { target: 'http://localhost:5167', changeOrigin: true },
      '/api': { target: 'http://localhost:5165', changeOrigin: true, timeout: 300_000 },
    },
  },
})
