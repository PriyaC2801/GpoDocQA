import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The API's CORS policy allows only this exact origin, so fail loudly when the
  // port is taken instead of silently starting on 5174 and breaking every request.
  server: {
    port: 5173,
    strictPort: true,
  },
})
