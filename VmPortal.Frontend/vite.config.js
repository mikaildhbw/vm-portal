import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Im Development leitet der Dev-Server alle /api-Anfragen an die echte API auf dem
// Windows Server weiter. Dadurch laufen Frontend und API unter derselben Origin und das
// httpOnly-Cookie greift ohne CORS-Sonderbehandlung.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://192.168.122.196:5000',
        changeOrigin: true,
      },
    },
  },
})
