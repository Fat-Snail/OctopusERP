import { defineStore } from 'pinia'
import { oidcService, type OidcUserInfo } from '@/services/oidcService'

export const useUserStore = defineStore('user', {
  state: () => ({
    sub: '',
    userName: '',
    email: '',
    roles: [] as string[],
  }),
  getters: {
    isLoggedIn: () => oidcService.isLoggedIn(),
    isCustomer: (state) => state.roles.includes('customer'),
    initials: (state) => (state.userName ? state.userName.slice(0, 1) : '客'),
  },
  actions: {
    loadFromOidc(): OidcUserInfo | null {
      const user = oidcService.getUser()
      if (user) {
        this.sub = user.sub
        this.userName = user.name
        this.email = user.email
        this.roles = user.roles
      }
      return user
    },
    logout(): void {
      oidcService.logout()
    },
  },
})
