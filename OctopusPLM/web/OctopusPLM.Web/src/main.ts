import { createApp } from 'vue'
import { createPinia } from 'pinia'
import router from './router'
import App from './App.vue'
import './styles/globals.css'
import { oidcService } from './services/oidcService'
import { usePlmUserStore } from './store/modules/plmUser'

async function bootstrap() {
  const app = createApp(App)

  // Install Pinia first
  app.use(createPinia())

  // Refresh user from OIDC storage before router guard runs
  const userStore = usePlmUserStore()
  userStore.refreshUser()

  // Install router (guards read isLoggedIn which is now ready)
  app.use(router)
  app.mount('#app')
}

// Suppress unused import warning — oidcService is used for its side effects (silent renew setup)
void oidcService

bootstrap()
