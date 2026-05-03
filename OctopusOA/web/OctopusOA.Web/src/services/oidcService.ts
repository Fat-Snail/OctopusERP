import { UserManager, WebStorageStateStore } from 'oidc-client-ts'
import type { OidcUserClaims } from '@/api/oa/types'

const userManager = new UserManager({
  authority: 'http://localhost:5001',
  client_id: 'octopus-oa-web',
  redirect_uri: 'http://localhost:5174/callback',
  post_logout_redirect_uri: 'http://localhost:5174',
  response_type: 'code',
  scope: 'openid profile email roles offline_access',
  automaticSilentRenew: true,
  silentRequestTimeoutInSeconds: 10,
  accessTokenExpiringNotificationTimeInSeconds: 60,
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

// Token 静默刷新失败时重新登录
userManager.events.addSilentRenewError(() => {
  oidcService.login()
})

export const oidcService = {
  login(): void {
    userManager.signinRedirect()
  },

  logout(): void {
    userManager.signoutRedirect()
  },

  getUser(): OidcUserClaims | null {
    // 同步获取：从 localStorage 读取缓存的用户
    const key = `oidc.user:http://localhost:5001:octopus-oa-web`
    const raw = localStorage.getItem(key)
    if (!raw) return null

    try {
      const data = JSON.parse(raw)
      if (!data.profile) return null

      return {
        sub: data.profile.sub ?? '',
        name: data.profile.name ?? data.profile.preferred_username ?? '',
        email: data.profile.email ?? '',
        roles: Array.isArray(data.profile.role) ? data.profile.role : data.profile.role ? [data.profile.role] : [],
        iat: data.profile.iat ?? 0,
        exp: data.expires_at ?? 0,
      }
    } catch {
      return null
    }
  },

  isLoggedIn(): boolean {
    const user = this.getUser()
    if (!user) return false
    return user.exp > Math.floor(Date.now() / 1000)
  },

  async handleCallback(): Promise<void> {
    await userManager.signinRedirectCallback()
  },

  getAccessToken(): string | null {
    const key = `oidc.user:http://localhost:5001:octopus-oa-web`
    const raw = localStorage.getItem(key)
    if (!raw) return null
    try {
      const data = JSON.parse(raw)
      return data.access_token ?? null
    } catch {
      return null
    }
  },

  getUserManager() {
    return userManager
  },
}
