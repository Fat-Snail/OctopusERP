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
    isSupplier: (state) => state.roles.includes('supplier'),
    initials: (state) => (state.userName ? state.userName.slice(0, 1) : '供'),
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
