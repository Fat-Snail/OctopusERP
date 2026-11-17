import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './styles/globals.css'

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      /* PWA 注册失败不影响功能 */
    })
  })
}

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
