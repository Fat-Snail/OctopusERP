import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

export interface OidcUserClaims {
  sub: string
  name: string
  email: string
  roles: string[]
  iat: number
  exp: number
}

const authority = 'http://localhost:5001'
const clientId = 'octopus-hrm-web'
const storageKey = `oidc.user:${authority}:${clientId}`

const userManager = new UserManager({
  authority,
  client_id: clientId,
  redirect_uri: 'http://localhost:5182/callback',
  post_logout_redirect_uri: 'http://localhost:5182',
  response_type: 'code',
  scope: 'openid profile email roles offline_access',
  automaticSilentRenew: true,
  silentRequestTimeoutInSeconds: 10,
  accessTokenExpiringNotificationTimeInSeconds: 60,
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

userManager.events.addSilentRenewError(() => { oidcService.login() })

export const oidcService = {
  login(): void { void userManager.signinRedirect() },
  logout(): void { void userManager.signoutRedirect() },
  getUser(): OidcUserClaims | null {
    const raw = localStorage.getItem(storageKey)
    if (!raw) return null
    try {
      const data = JSON.parse(raw) as {
        profile?: { sub?: string; name?: string; preferred_username?: string; email?: string; role?: string | string[]; iat?: number }
        expires_at?: number
      }
      if (!data.profile) return null
      return {
        sub: data.profile.sub ?? '',
        name: data.profile.name ?? data.profile.preferred_username ?? '',
        email: data.profile.email ?? '',
        roles: Array.isArray(data.profile.role) ? data.profile.role : (data.profile.role ? [data.profile.role] : []),
        iat: data.profile.iat ?? 0,
        exp: data.expires_at ?? 0,
      }
    } catch { return null }
  },
  isLoggedIn(): boolean {
    const user = this.getUser()
    return !!user && user.exp > Math.floor(Date.now() / 1000)
  },
  async handleCallback(): Promise<void> { await userManager.signinRedirectCallback() },
  getAccessToken(): string | null {
    const raw = localStorage.getItem(storageKey)
    if (!raw) return null
    try { return (JSON.parse(raw) as { access_token?: string }).access_token ?? null } catch { return null }
  },
  getUserManager() { return userManager },
}
