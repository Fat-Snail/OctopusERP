import axios, { type AxiosInstance } from 'axios'
import { oidcService } from '@/services/oidcService'

/**
 * 创建带 Bearer 认证的 axios 实例。
 */
export function createAuthedHttp(baseURL: string): AxiosInstance {
  const http = axios.create({ baseURL })

  http.interceptors.request.use(config => {
    const token = oidcService.getAccessToken()
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
  })

  return http
}

/**
 * 创建无认证的 axios 实例（H5 端用）。
 */
export function createPublicHttp(baseURL: string): AxiosInstance {
  return axios.create({ baseURL })
}
