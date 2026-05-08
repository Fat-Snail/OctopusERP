import { get, del } from '@/utils/http'
import type { PagedResult, PageQuery } from '../types'
import type { OperlogResponse, LoginfoResponse, ServerInfo, DashboardSummary } from './types'

interface OperlogQuery extends PageQuery {
  title?: string
  operName?: string
  status?: 0 | 1
}

interface LoginfoQuery extends PageQuery {
  userName?: string
  status?: 0 | 1
}

/** GET /api/monitor/operlog/list — 操作日志列表 */
export function getOperlogList(params: OperlogQuery) {
  return get<PagedResult<OperlogResponse>>('/monitor/operlog/list', params)
}

/** DELETE /api/monitor/operlog/clean — 清空操作日志 */
export function cleanOperlog() {
  return del<null>('/monitor/operlog/clean')
}

/** GET /api/monitor/logininfor/list — 登录日志列表 */
export function getLogininforList(params: LoginfoQuery) {
  return get<PagedResult<LoginfoResponse>>('/monitor/logininfor/list', params)
}

/** DELETE /api/monitor/logininfor/clean — 清空登录日志 */
export function cleanLogininfor() {
  return del<null>('/monitor/logininfor/clean')
}

/** GET /api/monitor/server — 服务器信息 */
export function getServerInfo() {
  return get<ServerInfo>('/monitor/server')
}

/** GET /api/monitor/dashboard — 工作台概览 */
export function getDashboardSummary() {
  return get<DashboardSummary>('/monitor/dashboard')
}
