import axios, { type AxiosRequestConfig, type AxiosResponse } from 'axios'
import type { ApiResponse } from '@/api/types'

const service = axios.create({
  baseURL: '/api',
  timeout: 10000,
  withCredentials: true
})

service.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const url: string = error.config?.url ?? ''
      const isProbe = url.includes('/account/me')
      if (window.location.pathname !== '/login' && !isProbe) {
        window.location.href = '/login'
      }
    }
    return Promise.reject(error as Error)
  }
)

function unwrap<T>(res: AxiosResponse<ApiResponse<T>>): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
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
