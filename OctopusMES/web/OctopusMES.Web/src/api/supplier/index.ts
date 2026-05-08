import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { SupplierListRow, SupplierDetail, CreateSupplierRequest, UpdateSupplierRequest, SupplierQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/supplier')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getSupplierList(params: SupplierQuery & PageQuery): Promise<PagedResult<SupplierListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<SupplierListRow>>>('', { params }))
}

export async function getSupplierById(id: number): Promise<SupplierDetail> {
  return unwrap(await http.get<ApiResponse<SupplierDetail>>(`/${id}`))
}

export async function createSupplier(data: CreateSupplierRequest): Promise<SupplierDetail> {
  return unwrap(await http.post<ApiResponse<SupplierDetail>>('', data))
}

export async function updateSupplier(data: UpdateSupplierRequest): Promise<SupplierDetail> {
  return unwrap(await http.put<ApiResponse<SupplierDetail>>('', data))
}

export async function deleteSupplier(id: number): Promise<void> {
  return unwrap(await http.delete<ApiResponse<void>>(`/${id}`))
}
