import axios, { type AxiosInstance } from 'axios'
import { oidcService } from '@/services/oidcService'

// 匹配 ISO 时间：带或不带时区后缀
const ISO_UTC_RE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?$/

function formatBeijingTime(iso: string): string {
  try {
    const d = new Date(iso)
    return d.toLocaleString('zh-CN', {
      timeZone: 'Asia/Shanghai',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).replace(/\//g, '-')
  } catch {
    return iso
  }
}

function transformDates(obj: unknown): void {
  if (!obj || typeof obj !== 'object') return

  if (Array.isArray(obj)) {
    for (const item of obj) transformDates(item)
    return
  }

  const record = obj as Record<string, unknown>
  for (const key of Object.keys(record)) {
    const value = record[key]
    if (typeof value === 'string' && ISO_UTC_RE.test(value)) {
      if (/Time$|At$|Date$|^date$|^time$/i.test(key)) {
        record[key] = formatBeijingTime(value)
      }
    } else if (value && typeof value === 'object') {
      transformDates(value)
    }
  }
}

/**
 * 创建带 Bearer 认证 + 时间自动转北京时间 的 axios 实例。
 */
export function createAuthedHttp(baseURL: string): AxiosInstance {
  const http = axios.create({ baseURL })

  http.interceptors.request.use(config => {
    const token = oidcService.getAccessToken()
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
  })

  http.interceptors.response.use(response => {
    if (response.data) transformDates(response.data)
    return response
  })

  return http
}
