import axios, { type AxiosInstance } from 'axios'
import { oidcService } from '@/services/oidcService'
import { formatBeijingTime } from './datetime'

// 匹配 ISO 时间：带或不带时区后缀（C# 序列化有时不带 Z）
const ISO_UTC_RE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?$/

/**
 * 递归把对象/数组中所有 ISO UTC 时间字符串替换为北京时间格式。
 * 仅对典型时间字段名（以 Time/At 结尾或完全命名为时间）生效，避免误伤。
 */
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
      // 时间字段名约定：以 Time / At / Date 结尾
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

/**
 * 创建无认证的 axios 实例（H5 端用），仍自动转换时间。
 */
export function createPublicHttp(baseURL: string): AxiosInstance {
  const http = axios.create({ baseURL })
  http.interceptors.response.use(response => {
    if (response.data) transformDates(response.data)
    return response
  })
  return http
}
