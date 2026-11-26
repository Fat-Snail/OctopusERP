import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { oidcService } from '@/services/oidcService'
import type { OidcUserClaims } from '@/services/oidcService'

export const useUserStore = defineStore('user', () => {
  const user = ref<OidcUserClaims | null>(oidcService.getUser())

  const isLoggedIn = computed(() => oidcService.isLoggedIn())
  const userName = computed(() => user.value?.name ?? '')
  const email = computed(() => user.value?.email ?? '')
  const roles = computed(() => user.value?.roles ?? [])

  function refreshUser() {
    user.value = oidcService.getUser()
  }

  return { user, isLoggedIn, userName, email, roles, refreshUser }
})
