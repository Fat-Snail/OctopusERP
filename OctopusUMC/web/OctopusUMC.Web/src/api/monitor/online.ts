import { get, del } from '@/utils/http'
import type { PagedResult, PageQuery } from '../types'
import type { OnlineUserResponse } from './types'

/** GET /api/monitor/online/list — 在线用户列表 */
export function getOnlineUsers(params: PageQuery) {
  return get<PagedResult<OnlineUserResponse>>('/monitor/online/list', params)
}

/** DELETE /api/monitor/online/{tokenId} — 强制下线 */
export function forceLogout(tokenId: string) {
  return del<null>(`/monitor/online/${tokenId}`)
}
