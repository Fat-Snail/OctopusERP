import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { resolve } from 'path'
import { readFileSync } from 'fs'

const pkg = JSON.parse(readFileSync(resolve(__dirname, 'package.json'), 'utf-8')) as { version: string }

export default defineConfig({
  plugins: [
    vue(),
    tailwindcss(),
  ],
  resolve: {
    alias: { '@': resolve(__dirname, 'src') }
  },
  define: {
    __APP_VERSION__: JSON.stringify(`v${pkg.version}`),
    __BUILD_DATE__: JSON.stringify(new Date().toISOString().slice(0, 10)),
  },
  server: {
    port: parseInt(process.env['PORT'] ?? '5185'),
    // Mock 版本地数据渲染；接入真实后端后启用对应代理（网关 OctopusGateway 就绪前可直连各子系统）
    proxy: {
      '/api': {
        target: process.env['PORTAL_API_TARGET'] ?? 'http://localhost:5006',
        changeOrigin: true,
      }
    }
  }
})
