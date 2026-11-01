import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './styles/globals.css'

async function bootstrap() {
  if (import.meta.env.DEV) {
    const { worker } = await import('./mock/index')
    await worker.start({ onUnhandledFrame: 'bypass' })
  }

  const app = createApp(App)
  app.use(createPinia())
  app.use(router)
  app.mount('#app')
}

bootstrap()
