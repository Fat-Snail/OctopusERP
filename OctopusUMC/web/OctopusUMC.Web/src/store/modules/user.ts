import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { post, get } from '@/utils/http'
import type { LoginRequest, LoginResponse } from '@/api/types'

export const useUserStore = defineStore('user', () => {
  // Cookie 认证：后端颁发 HttpOnly Cookie，前端不存储 token
  const userInfo = ref<LoginResponse | null>(null)

  const isLoggedIn = computed(() => !!userInfo.value)
  const permissions = computed(() => userInfo.value?.permissions ?? [])
  const roles = computed(() => userInfo.value?.roles ?? [])

  function hasPermission(perm: string): boolean {
    if (permissions.value.includes('*:*:*')) return true
    return permissions.value.includes(perm)
  }

  async function login(payload: LoginRequest): Promise<void> {
    const result = await post<LoginResponse>('/account/login', payload)
    userInfo.value = result
  }

  async function logout(): Promise<void> {
    try {
      await post<null>('/account/logout', {})
    } finally {
      userInfo.value = null
    }
  }

  /** 刷新页面后通过 /account/me 恢复登录状态 */
  async function fetchMe(): Promise<void> {
    try {
      const result = await get<LoginResponse>('/account/me')
      userInfo.value = result
    } catch {
      userInfo.value = null
    }
  }

  return { userInfo, isLoggedIn, permissions, roles, hasPermission, login, logout, fetchMe }
})
