import { UserManager, WebStorageStateStore } from 'oidc-client-ts'

export interface OidcUserClaims {
  sub: string
  name: string
  email: string
  roles: string[]
  iat: number
  exp: number
}

const PLM_AUTHORITY = 'http://localhost:5001'
const PLM_CLIENT_ID = 'octopus-plm-web'
const PLM_REDIRECT_URI = 'http://localhost:5175/callback'

const userManager = new UserManager({
  authority: PLM_AUTHORITY,
  client_id: PLM_CLIENT_ID,
  redirect_uri: PLM_REDIRECT_URI,
  post_logout_redirect_uri: 'http://localhost:5175',
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

const storageKey = `oidc.user:${PLM_AUTHORITY}:${PLM_CLIENT_ID}`

export const oidcService = {
  login(): void {
    userManager.signinRedirect()
  },

  logout(): void {
    userManager.signoutRedirect()
  },

  getUser(): OidcUserClaims | null {
    const raw = localStorage.getItem(storageKey)
    if (!raw) return null

    try {
      const data = JSON.parse(raw) as Record<string, unknown>
      const profile = data['profile'] as Record<string, unknown> | undefined
      if (!profile) return null

      const roleClaim = profile['role'] ?? profile['roles']
      return {
        sub: (profile['sub'] as string) ?? '',
        name: (profile['name'] as string) ?? (profile['preferred_username'] as string) ?? '',
        email: (profile['email'] as string) ?? '',
        roles: Array.isArray(roleClaim) ? (roleClaim as string[]) : roleClaim ? [roleClaim as string] : [],
        iat: (profile['iat'] as number) ?? 0,
        exp: (data['expires_at'] as number) ?? 0,
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
    const raw = localStorage.getItem(storageKey)
    if (!raw) return null
    try {
      const data = JSON.parse(raw) as Record<string, unknown>
      return (data['access_token'] as string) ?? null
    } catch {
      return null
    }
  },

  getUserManager() {
    return userManager
  },
}
