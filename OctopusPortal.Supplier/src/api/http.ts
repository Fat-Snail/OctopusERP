// ── 供应商门户 HTTP 客户端（经 OctopusGateway） ──
// /gw/mes 前缀剥离后转发到 MES(5006)；/gw/crm 转发到 CRM(5004)。网关强制 JWT 验签 + 限流。
// LIVE 开关：VITE_PORTAL_LIVE=1 时走真实 API；默认关闭（保持 mock，后端端点/身份绑定未就绪时不崩页）。

import axios, { type AxiosInstance } from 'axios'
import { oidcService } from '@/services/oidcService'

export const LIVE = import.meta.env['VITE_PORTAL_LIVE'] === '1'

const GATEWAY = import.meta.env['VITE_GATEWAY_URL'] ?? 'http://localhost:5100'

export const http: AxiosInstance = axios.create({
  baseURL: `${GATEWAY}/gw/mes`,
  timeout: 8000,
  headers: { 'Content-Type': 'application/json' },
})

http.interceptors.request.use((cfg) => {
  const token = oidcService.getAccessToken()
  if (token) cfg.headers.Authorization = `Bearer ${token}`
  return cfg
})

/** 拆后端统一响应 { code, msg, data }；code !== 200 视为业务错误。 */
export async function unwrap<T>(promise: Promise<{ data: { code: number; msg?: string; message?: string; data: T } }>): Promise<T> {
  const resp = await promise
  const body = resp.data
  if (body?.code !== 200) throw new Error(body?.msg ?? body?.message ?? `业务错误 code=${body?.code}`)
  return body.data
}
