import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { InquiryListRow, CreateInquiryRequest, InquiryQuery, InquiryStatus } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/inquiry')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getInquiryList(
  params: InquiryQuery & PageQuery
): Promise<PagedResult<InquiryListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<InquiryListRow>>>('', { params }))
}

export async function createInquiry(data: CreateInquiryRequest): Promise<InquiryListRow> {
  return unwrap(await http.post<ApiResponse<InquiryListRow>>('', data))
}

export async function updateInquiryStatus(id: number, status: InquiryStatus): Promise<void> {
  return unwrap(await http.put<ApiResponse<void>>(`/${id}/status`, { status }))
}
