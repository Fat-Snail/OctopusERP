/**
 * Keycloak OIDC 客户端（PKCE）。
 *
 * client_id = octopus-retail，与 aspire 中 realm 配置一致。
 * 注意：零售域只信任 token；不做任何「回查 UMC」的动作。
 *
 * ⚠️ 默认是 **https**：Aspire 的 AddKeycloak(port: 8088) 把 8088 映射到容器的
 * 8443(HTTPS)，issuer 为 https://localhost:8088/realms/octopus。
 * 用 http:// 会直接连不上（curl 返回 000）。
 * 开发环境是自签证书，浏览器首次会提示不安全——点继续即可。
 *
 * 可用 VITE_KEYCLOAK_AUTHORITY 覆盖（见 .env.example）。
 */
import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'

export const AUTHORITY =
  import.meta.env.VITE_KEYCLOAK_AUTHORITY ?? 'https://localhost:8088/realms/octopus'
const CLIENT_ID = 'octopus-retail'
const REDIRECT_URI = `${window.location.origin}/callback`

export const userManager = new UserManager({
  authority: AUTHORITY,
  client_id: CLIENT_ID,
  redirect_uri: REDIRECT_URI,
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code',
  scope: 'openid profile email',
  automaticSilentRenew: false,
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

export async function getUser(): Promise<User | null> {
  return await userManager.getUser()
}

export async function getAccessToken(): Promise<string | null> {
  const user = await userManager.getUser()
  if (!user || user.expired) return null
  return user.access_token
}

export async function login(returnUrl?: string): Promise<void> {
  await userManager.signinRedirect({
    state: { returnUrl: returnUrl ?? window.location.pathname },
  })
}

export async function handleCallback(): Promise<string> {
  const user = await userManager.signinRedirectCallback()
  const state = user.state as { returnUrl?: string } | undefined
  return state?.returnUrl ?? '/'
}

export async function logout(): Promise<void> {
  await userManager.signoutRedirect()
}

/**
 * 从 token 的 groups claim 提取可进入的租户（与后端 TenantClaims 同一契约）。
 * 前端只用它做展示与预选，**授权判定始终在服务端**。
 */
export function extractTenantIds(accessToken: string): string[] {
  const payload = decodeJwtPayload(accessToken)
  if (!payload) return []
  const groups = payload['groups']
  const list = Array.isArray(groups) ? groups : groups ? [groups] : []
  const prefix = '/tenants/'
  return list
    .filter((g): g is string => typeof g === 'string' && g.startsWith(prefix))
    .map((g) => g.slice(prefix.length).replace(/\/+$/, ''))
    .filter((t) => t.length > 0 && !t.includes('/'))
}

export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const part = token.split('.')[1]
    if (!part) return null
    const json = atob(part.replace(/-/g, '+').replace(/_/g, '/'))
    return JSON.parse(json) as Record<string, unknown>
  } catch {
    return null
  }
}
