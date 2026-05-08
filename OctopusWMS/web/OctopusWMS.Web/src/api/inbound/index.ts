import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { InboundListRow, InboundDetail, CreateInboundRequest, ReceiveInboundRequest, InboundQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/inbound')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getInboundList(params: InboundQuery & PageQuery): Promise<PagedResult<InboundListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<InboundListRow>>>('', { params }))
}

export async function getInboundById(id: number): Promise<InboundDetail> {
  return unwrap(await http.get<ApiResponse<InboundDetail>>(`/${id}`))
}

export async function createInbound(data: CreateInboundRequest): Promise<InboundDetail> {
  return unwrap(await http.post<ApiResponse<InboundDetail>>('', data))
}

export async function receiveInbound(data: ReceiveInboundRequest): Promise<InboundDetail> {
  return unwrap(await http.put<ApiResponse<InboundDetail>>('/receive', data))
}
