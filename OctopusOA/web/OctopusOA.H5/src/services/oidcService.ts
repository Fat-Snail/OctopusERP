import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

const UMC_AUTHORITY = 'http://localhost:5001'
const CLIENT_ID = 'octopus-oa-h5'
const REDIRECT_URI = 'http://localhost:5180/callback'
const POST_LOGOUT_URI = 'http://localhost:5180'

const userManager = new UserManager({
  authority: UMC_AUTHORITY,
  client_id: CLIENT_ID,
  redirect_uri: REDIRECT_URI,
  post_logout_redirect_uri: POST_LOGOUT_URI,
  response_type: 'code',
  scope: 'openid profile email roles offline_access',
  automaticSilentRenew: true,
  silentRequestTimeoutInSeconds: 10,
  accessTokenExpiringNotificationTimeInSeconds: 60,
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

userManager.events.addSilentRenewError(() => {
  oidcService.login()
})

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

  if (await tryProbe('/health/ready')) return
  if (await tryProbe('/health')) return
  if (await tryProbe('/.well-known/openid-configuration')) return

  throw new Error(
    'UMC 后端未就绪（5s 内无响应）。\n' +
    '请在 Aspire Dashboard (http://localhost:15256) 检查 umc-api 状态后重试。'
  )
}

export interface OidcUserInfo {
  sub: string
  name: string
  email: string
  roles: string[]
  exp: number
}

export const oidcService = {
  async login(): Promise<void> {
    try {
      await probeUmcReady()
    } catch (e) {
      // eslint-disable-next-line no-alert
      alert((e as Error).message)
      return
    }
    userManager.signinRedirect()
  },

  logout(): void {
    userManager.signoutRedirect()
  },

  getUser(): OidcUserInfo | null {
    const key = `oidc.user:${UMC_AUTHORITY}:${CLIENT_ID}`
    const raw = localStorage.getItem(key)
    if (!raw) return null
    try {
      const data = JSON.parse(raw) as { profile?: { sub?: string; name?: string; preferred_username?: string; email?: string; role?: string | string[] }; expires_at?: number }
      if (!data.profile) return null
      return {
        sub: data.profile.sub ?? '',
        name: data.profile.name ?? data.profile.preferred_username ?? '',
        email: data.profile.email ?? '',
        roles: Array.isArray(data.profile.role) ? data.profile.role : data.profile.role ? [data.profile.role] : [],
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
    const key = `oidc.user:${UMC_AUTHORITY}:${CLIENT_ID}`
    const raw = localStorage.getItem(key)
    if (!raw) return null
    try {
      const data = JSON.parse(raw) as { access_token?: string }
      return data.access_token ?? null
    } catch {
      return null
    }
  },
}
