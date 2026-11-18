import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // ASP.NET Core API (backend/src/SmartERP.Api) runs on http://localhost:5080
      '/api': { target: 'http://localhost:5080', changeOrigin: true },
    },
  },
})
