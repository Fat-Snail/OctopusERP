import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { WarehouseListRow, WarehouseDetail, CreateWarehouseRequest, UpdateWarehouseRequest, WarehouseQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/warehouse')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getWarehouseList(params: WarehouseQuery & PageQuery): Promise<PagedResult<WarehouseListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<WarehouseListRow>>>('', { params }))
}

export async function getWarehouseById(id: number): Promise<WarehouseDetail> {
  return unwrap(await http.get<ApiResponse<WarehouseDetail>>(`/${id}`))
}

export async function createWarehouse(data: CreateWarehouseRequest): Promise<WarehouseDetail> {
  return unwrap(await http.post<ApiResponse<WarehouseDetail>>('', data))
}

export async function updateWarehouse(data: UpdateWarehouseRequest): Promise<WarehouseDetail> {
  return unwrap(await http.put<ApiResponse<WarehouseDetail>>('', data))
}

export async function deleteWarehouse(id: number): Promise<void> {
  return unwrap(await http.delete<ApiResponse<void>>(`/${id}`))
}
