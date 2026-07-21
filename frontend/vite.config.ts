import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
    plugins:[
        react(),
        VitePWA({
            registerType: 'autoUpdate',
            injectRegister: 'auto',
            includeAssets: ['favicon.ico', 'apple-touch-icon-180x180.png'],
            manifest: {
                name: 'Uni-Verein Vereinsverwaltung',
                short_name: 'Vereinsverwaltung',
                description: 'Verwaltung von Vereinsmitgliedern, Beiträgen und SEPA-Lastschriften',
                theme_color: '#2563eb',
                background_color: '#f8fafc',
                display: 'standalone',
                start_url: '/',
                scope: '/',
                icons: [
                    { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
                    { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
                    { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
                    { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
                ],
            },
            workbox: {
                globPatterns: ['**/*.{js,css,html,svg,woff2}'],
                maximumFileSizeToCacheInBytes: 6 * 1024 * 1024,
                navigateFallback: '/index.html',
                navigateFallbackDenylist: [/^\/api\//, /^\/emailProgress/],
                skipWaiting: true,
                clientsClaim: true,
                cleanupOutdatedCaches: true,
                runtimeCaching: [
                    {
                        urlPattern: ({ url }) => url.pathname.startsWith('/api/'),
                        handler: 'NetworkOnly',
                    },
                    {
                        urlPattern: ({ url }) => url.pathname.startsWith('/emailProgress'),
                        handler: 'NetworkOnly',
                    },
                ],
            },
            devOptions: {
                enabled: false,
            },
        }),
    ],
    server:{
        proxy:{
            "/api":"http://backend:8080"
        }
    }
})
