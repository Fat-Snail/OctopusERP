import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './styles/globals.css'
import { useUserStore } from '@/store/modules/user'

async function bootstrap() {
  if (import.meta.env.VITE_ENABLE_MOCK === 'true' || import.meta.env.DEV) {
    // 探测真实后端是否在线：能拿到 HTTP 响应（哪怕 401）就说明在线，跳过 MSW
    // 否则（network error = 后端未启动）才启用 MSW Mock
    const backendAlive = await fetch('/api/account/me', { credentials: 'include' })
      .then(r => r.status > 0)
      .catch(() => false)

    if (!backendAlive) {
      const { worker } = await import('./mock/index')
      await worker.start({ onUnhandledRequest: 'bypass' })
    }
  }

  const app = createApp(App)

  // 先安装 Pinia，fetchMe 需要 Pinia 实例
  app.use(createPinia())

  // 刷新页面时通过 /account/me 从 Cookie 恢复登录状态
  // 必须在 app.use(router) 之前完成，确保路由守卫读到正确的 isLoggedIn
  const userStore = useUserStore()
  await userStore.fetchMe()

  // 安装路由（此时守卫判断 isLoggedIn 已有正确值）
  app.use(router)
  app.mount('#app')
}

bootstrap()
