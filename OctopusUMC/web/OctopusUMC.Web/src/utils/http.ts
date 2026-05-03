import axios, { type AxiosRequestConfig, type AxiosResponse } from 'axios'
import type { ApiResponse } from '@/api/types'

/**
 * 后端使用 Cookie 认证（HttpOnly Cookie），不需要手动传 Authorization 头。
 * withCredentials: true 确保跨域请求携带 Cookie。
 */
const service = axios.create({
  baseURL: '/api',
  timeout: 10000,
  withCredentials: true
})

service.interceptors.response.use(
  (response: AxiosResponse<ApiResponse>) => {
    return response
  },
  error => {
    if (error.response?.status === 401) {
      const url: string = error.config?.url ?? ''
      const alreadyOnLogin = window.location.pathname === '/login'
      // /account/me 是 bootstrap 阶段的会话探测请求，401 表示未登录属正常情况，
      // 由 fetchMe() 的 catch 块静默处理，不触发跳转（否则会造成死循环）。
      // 其他接口 401（Cookie 过期等），且当前不在 /login 时才跳转。
      const isSessionProbe = url.includes('/account/me')
      if (!alreadyOnLogin && !isSessionProbe) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error as Error)
  }
)

function unwrap<T>(res: AxiosResponse<ApiResponse<T>>): T {
  if (res.data.code !== 200) {
    throw new Error(res.data.msg || '请求失败')
  }
  return res.data.data
}

export async function get<T>(url: string, params?: unknown): Promise<T> {
  const res = await service.get<ApiResponse<T>>(url, { params } as AxiosRequestConfig)
  return unwrap(res)
}

export async function post<T>(url: string, data?: unknown): Promise<T> {
  const res = await service.post<ApiResponse<T>>(url, data)
  return unwrap(res)
}

export async function put<T>(url: string, data?: unknown): Promise<T> {
  const res = await service.put<ApiResponse<T>>(url, data)
  return unwrap(res)
}

export async function del<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  const res = await service.delete<ApiResponse<T>>(url, { params } as AxiosRequestConfig)
  return unwrap(res)
}

export default service
