import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { QuoteListRow, QuoteDetail, QuoteQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/quote')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getQuoteList(
  params: QuoteQuery & PageQuery
): Promise<PagedResult<QuoteListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<QuoteListRow>>>('', { params }))
}

export async function getQuoteById(id: number): Promise<QuoteDetail> {
  return unwrap(await http.get<ApiResponse<QuoteDetail>>(`/${id}`))
}

export async function submitQuote(id: number): Promise<void> {
  return unwrap(await http.put<ApiResponse<void>>(`/${id}/submit`))
}

export async function confirmQuote(id: number): Promise<void> {
  return unwrap(await http.put<ApiResponse<void>>(`/${id}/confirm`))
}
