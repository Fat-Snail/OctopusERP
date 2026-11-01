import { defineStore } from 'pinia'
import { ref } from 'vue'
import { oidcService } from '@/services/oidcService'

export const useUserStore = defineStore('user', () => {
  const userName = ref('')
  const email = ref('')
  const sub = ref('')
  const roles = ref<string[]>([])

  function loadFromOidc() {
    const user = oidcService.getUser()
    if (user) {
      userName.value = user.name
      email.value = user.email
      sub.value = user.sub
      roles.value = user.roles
    }
  }

  function clear() {
    userName.value = ''
    email.value = ''
    sub.value = ''
    roles.value = []
  }

  return { userName, email, sub, roles, loadFromOidc, clear }
})
