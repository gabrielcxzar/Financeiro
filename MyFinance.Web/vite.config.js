import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom'],
          antd: ['antd', 'antd-mobile', '@ant-design/icons'],
          charts: ['chart.js', 'react-chartjs-2'],
          utils: ['axios', 'dayjs', 'styled-components', 'react-icons'],
        },
      },
    },
  },
})
