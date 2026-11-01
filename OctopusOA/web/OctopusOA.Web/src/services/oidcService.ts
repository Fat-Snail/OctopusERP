import { UserManager, WebStorageStateStore } from 'oidc-client-ts'
import type { OidcUserClaims } from '@/api/oa/types'

const UMC_AUTHORITY = 'http://localhost:5001'

const userManager = new UserManager({
  authority: UMC_AUTHORITY,
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

/**
 * 预检 UMC 是否就绪 — 5 秒超时 + 优先 readiness 探针。
 * 失败抛出友好错误，避免 oidc-client-ts 内部 fetch 无限挂起。
 */
async function probeUmcReady(timeoutMs = 5000): Promise<void> {
  const tryProbe = async (path: string): Promise<boolean> => {
    const ctrl = new AbortController()
    const timer = setTimeout(() => ctrl.abort(), timeoutMs)
    try {
      const r = await fetch(`${UMC_AUTHORITY}${path}`, { signal: ctrl.signal, cache: 'no-store' })
      return r.ok
    } catch { return false }
    finally { clearTimeout(timer) }
  }

  // 优先 /health/ready（DB+OIDC 客户端注册完成）；旧版本回退 /health
  if (await tryProbe('/health/ready')) return
  if (await tryProbe('/health')) return
  if (await tryProbe('/.well-known/openid-configuration')) return

  throw new Error(
    'UMC 后端未就绪（5s 内无响应）。\n' +
    '请在 Aspire Dashboard (http://localhost:15256) 检查 umc-api 状态，\n' +
    '或手动重启该资源后重试。'
  )
}

export const oidcService = {
  /** 在执行 signinRedirect 前先 ping UMC，避免浏览器无限 spinner */
  async login(): Promise<void> {
    try {
      await probeUmcReady()
    } catch (e) {
      const msg = (e as Error).message
      console.error('[OIDC] UMC 不可达：', msg)
      // eslint-disable-next-line no-alert
      alert(msg)
      return
    }
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
