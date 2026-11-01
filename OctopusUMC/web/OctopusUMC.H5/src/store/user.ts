import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { get, post } from '@/utils/http'
import type { LoginRequest, LoginResponse } from '@/api/types'

export const useUserStore = defineStore('user', () => {
  const userInfo = ref<LoginResponse | null>(null)

  const isLoggedIn = computed(() => !!userInfo.value)
  const userName = computed(() => userInfo.value?.nickName || userInfo.value?.userName || '')

  async function login(payload: LoginRequest): Promise<void> {
    const result = await post<LoginResponse>('/account/login', payload)
    userInfo.value = result
  }

  async function logout(): Promise<void> {
    try { await post<null>('/account/logout', {}) } finally { userInfo.value = null }
  }

  async function fetchMe(): Promise<void> {
    try {
      userInfo.value = await get<LoginResponse>('/account/me')
    } catch {
      userInfo.value = null
    }
  }

  return { userInfo, isLoggedIn, userName, login, logout, fetchMe }
})
