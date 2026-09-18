import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'prompt',
      injectRegister: 'auto',
      includeAssets: ['favicon.svg', 'brand-mark.svg'],
      manifest: {
        name: 'FinFlow',
        short_name: 'FinFlow',
        description: 'Gestão financeira pessoal',
        display: 'standalone',
        orientation: 'any',
        start_url: '/',
        scope: '/',
        theme_color: '#0F172A',
        background_color: '#F8FAFC',
        lang: 'pt-BR',
        icons: [
          { src: '/pwa-192x192.svg', sizes: '192x192', type: 'image/svg+xml' },
          { src: '/pwa-512x512.svg', sizes: '512x512', type: 'image/svg+xml' },
          {
            src: '/pwa-maskable-512x512.svg',
            sizes: '512x512',
            type: 'image/svg+xml',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        navigateFallback: '/index.html',
        navigateFallbackDenylist: [/^\/api(?:\/|$)/, /^\/mcp(?:\/|$)/, /^\/oauth(?:\/|$)/],
        cleanupOutdatedCaches: true,
        clientsClaim: false,
        skipWaiting: false,
        runtimeCaching: [
          {
            urlPattern: ({ url }) =>
              url.pathname.startsWith('/api/') ||
              url.pathname === '/mcp' ||
              url.pathname.startsWith('/mcp/') ||
              url.pathname.startsWith('/oauth/') ||
              url.pathname.startsWith('/connect/') ||
              url.pathname.startsWith('/.well-known/'),
            handler: 'NetworkOnly',
          },
          {
            urlPattern: ({ url }) =>
              url.origin === 'https://my-finance-api-a51s.onrender.com' &&
              (url.pathname.startsWith('/api/') ||
                url.pathname === '/mcp' ||
                url.pathname.startsWith('/mcp/') ||
                url.pathname.startsWith('/oauth/') ||
                url.pathname.startsWith('/connect/') ||
                url.pathname.startsWith('/.well-known/')),
            handler: 'NetworkOnly',
          },
        ],
      },
      // Keep development free of service-worker state unless explicitly requested.
      devOptions: { enabled: globalThis.process?.env?.VITE_PWA_DEV === 'true', type: 'module' },
    }),
  ],
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
