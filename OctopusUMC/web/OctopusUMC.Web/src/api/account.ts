import { get, post } from '@/utils/http'
import type { LoginRequest, LoginResponse } from './types'

/** POST /api/account/login — 登录，颁发 HttpOnly Cookie */
export function login(data: LoginRequest) {
  return post<LoginResponse>('/account/login', data)
}

/** POST /api/account/logout — 登出，清除 Cookie */
export function logout() {
  return post<null>('/account/logout', {})
}

/** GET /api/account/me — 当前登录用户信息 */
export function getMe() {
  return get<LoginResponse>('/account/me')
}
