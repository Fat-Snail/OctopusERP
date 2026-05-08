import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { OutboundListRow, OutboundDetail, CreateOutboundRequest, OutboundQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/outbound')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getOutboundList(params: OutboundQuery & PageQuery): Promise<PagedResult<OutboundListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<OutboundListRow>>>('', { params }))
}

export async function getOutboundById(id: number): Promise<OutboundDetail> {
  return unwrap(await http.get<ApiResponse<OutboundDetail>>(`/${id}`))
}

export async function createOutbound(data: CreateOutboundRequest): Promise<OutboundDetail> {
  return unwrap(await http.post<ApiResponse<OutboundDetail>>('', data))
}

export async function shipOutbound(id: number): Promise<OutboundDetail> {
  return unwrap(await http.put<ApiResponse<OutboundDetail>>(`/${id}/ship`, {}))
}
