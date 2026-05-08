import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { oidcService } from '@/services/oidcService'

export const usePlmUserStore = defineStore('plm-user', () => {
  const user = ref(oidcService.getUser())

  const isLoggedIn = computed(() => oidcService.isLoggedIn())
  const userName = computed(() => user.value?.name ?? '')
  const email = computed(() => user.value?.email ?? '')
  const roles = computed(() => user.value?.roles ?? [])

  function refreshUser() {
    user.value = oidcService.getUser()
  }

  return { user, isLoggedIn, userName, email, roles, refreshUser }
})
