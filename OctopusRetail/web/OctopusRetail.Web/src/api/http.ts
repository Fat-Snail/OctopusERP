import axios, { AxiosError, type AxiosRequestConfig } from 'axios'
import { getAccessToken, login } from '@/services/oidcService'
import { getActiveTenant } from './tenantContext'
import type { ApiResponse } from './types'

export class ApiError extends Error {
  constructor(
    readonly code: number,
    message: string,
    readonly status?: number,
    /** 是否属于租户上下文问题（缺失/越权），用于引导用户重新选择 workspace */
    readonly tenantIssue = false,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

const http = axios.create({ baseURL: '/api/retail', timeout: 15000 })

http.interceptors.request.use(async (config) => {
  const token = await getAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`

  // 租户内端点需要，跨租户端点会忽略该头
  const tenant = getActiveTenant()
  if (tenant) config.headers['X-Tenant-Id'] = tenant
  else delete config.headers['X-Tenant-Id']

  return config
})

/**
 * 匿名可访问的页面路径前缀。
 *
 * 这些页面上的任何 401 都**不允许**触发全局跳登录：
 * 邀请落地页本来就是给"还没有账号"的人看的，把它踢去 Keycloak
 * 等于把邀请流程彻底堵死（实测踩过：落地页一进来就跳登录页）。
 */
const PUBLIC_PATH_PREFIXES = ['/invite', '/i/', '/join', '/callback', '/error', '/login']

function isPublicPath(pathname: string): boolean {
  return PUBLIC_PATH_PREFIXES.some((p) => pathname === p || pathname.startsWith(p))
}

http.interceptors.response.use(
  (res) => res,
  async (error: AxiosError) => {
    if (error.response?.status === 401) {
      const path = window.location.pathname
      // 诊断用：把吃 401 的具体接口打出来，避免"莫名其妙跳登录"
      console.warn('[http] 401 来自', error.config?.url, '当前页面', path)
      if (!isPublicPath(path)) {
        await login(path)
      }
    }
    return Promise.reject(error)
  },
)

const TENANT_HINTS = ['租户', 'X-Tenant-Id', '无权访问该租户']

async function call<T>(config: AxiosRequestConfig): Promise<T> {
  try {
    const res = await http.request<ApiResponse<T>>(config)
    if (res.data.code !== 200) {
      throw new ApiError(res.data.code, res.data.msg, res.status, isTenantIssue(res.data.msg))
    }
    return res.data.data
  } catch (e) {
    if (e instanceof ApiError) throw e
    const err = e as AxiosError<ApiResponse>
    if (err.response) {
      const msg = err.response.data?.msg ?? `请求失败（${err.response.status}）`
      throw new ApiError(err.response.status, msg, err.response.status, isTenantIssue(msg))
    }
    throw e
  }
}

function isTenantIssue(msg: string): boolean {
  return TENANT_HINTS.some((h) => msg.includes(h))
}

export const api = {
  get: <T>(url: string, params?: Record<string, unknown>) => call<T>({ method: 'GET', url, params }),
  post: <T>(url: string, data?: unknown) => call<T>({ method: 'POST', url, data }),
}

export default http
