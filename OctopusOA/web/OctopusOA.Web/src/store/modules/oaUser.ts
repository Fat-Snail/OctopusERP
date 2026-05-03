import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getCurrentUser } from '@/api/oa/user'
import type { SyncUserResponse } from '@/api/oa/types'

export const useOaUserStore = defineStore('oaUser', () => {
  const currentUser = ref<SyncUserResponse | null>(null)
  const loaded = ref(false)

  const oaRoles = computed(() => currentUser.value?.oaRoles ?? [])
  const isOaAdmin = computed(() => oaRoles.value.includes('oa_admin'))
  const isOaManager = computed(() => oaRoles.value.includes('oa_manager'))

  function hasOaRole(role: string): boolean {
    return oaRoles.value.includes(role)
  }

  async function fetchCurrentUser() {
    try {
      currentUser.value = await getCurrentUser()
    } catch {
      currentUser.value = null
    }
    loaded.value = true
  }

  return { currentUser, loaded, oaRoles, isOaAdmin, isOaManager, hasOaRole, fetchCurrentUser }
})
