import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { PurchaseListRow, PurchaseDetail, CreatePurchaseRequest, PurchaseQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/purchase')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getPurchaseList(params: PurchaseQuery & PageQuery): Promise<PagedResult<PurchaseListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<PurchaseListRow>>>('', { params }))
}

export async function getPurchaseById(id: number): Promise<PurchaseDetail> {
  return unwrap(await http.get<ApiResponse<PurchaseDetail>>(`/${id}`))
}

export async function createPurchase(data: CreatePurchaseRequest): Promise<PurchaseDetail> {
  return unwrap(await http.post<ApiResponse<PurchaseDetail>>('', data))
}

export async function submitPurchase(id: number): Promise<PurchaseDetail> {
  return unwrap(await http.put<ApiResponse<PurchaseDetail>>(`/${id}/submit`, {}))
}

export async function approvePurchase(id: number, comment?: string): Promise<PurchaseDetail> {
  return unwrap(await http.put<ApiResponse<PurchaseDetail>>(`/${id}/approve`, { comment }))
}

export async function rejectPurchase(id: number, comment?: string): Promise<PurchaseDetail> {
  return unwrap(await http.put<ApiResponse<PurchaseDetail>>(`/${id}/reject`, { comment }))
}
