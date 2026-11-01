import axios from 'axios'
import type { AxiosResponse } from 'axios'
import { oidcService } from '@/services/oidcService'
import type { ApiResponse } from '@/api/types'

const ISO_TIME_RE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/

function toBeijingTime(isoStr: string): string {
  const d = new Date(isoStr)
  return d.toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai', hour12: false })
    .replace(/\//g, '-')
}

function transformDates(obj: unknown): unknown {
  if (obj === null || obj === undefined) return obj
  if (typeof obj === 'string' && ISO_TIME_RE.test(obj)) return toBeijingTime(obj)
  if (Array.isArray(obj)) return obj.map(transformDates)
  if (typeof obj === 'object') {
    const result: Record<string, unknown> = {}
    for (const [k, v] of Object.entries(obj as Record<string, unknown>)) {
      if (typeof v === 'string' && ISO_TIME_RE.test(v) &&
          /time|at|date/i.test(k)) {
        result[k] = toBeijingTime(v)
      } else {
        result[k] = transformDates(v)
      }
    }
    return result
  }
  return obj
}

export const http = axios.create({ baseURL: '/api' })

http.interceptors.request.use((config) => {
  const token = oidcService.getAccessToken()
  if (token) config.headers['Authorization'] = `Bearer ${token}`
  return config
})

http.interceptors.response.use(
  (res: AxiosResponse) => {
    if (res.data && typeof res.data === 'object') {
      res.data = transformDates(res.data)
    }
    return res
  },
  (err) => {
    if (err.response?.status === 401) {
      const url: string = err.config?.url ?? ''
      if (!url.includes('/me')) {
        oidcService.login()
      }
    }
    return Promise.reject(err)
  }
)

export function unwrap<T>(res: AxiosResponse<ApiResponse<T>>): T {
  const body = res.data
  if (body.code !== 200) throw new Error(body.msg || '请求失败')
  return body.data
}
