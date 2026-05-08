import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { InventoryRow, InventorySummary, AdjustInventoryRequest, InventoryQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/inventory')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getInventoryList(params: InventoryQuery & PageQuery): Promise<PagedResult<InventoryRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<InventoryRow>>>('', { params }))
}

export async function getInventorySummary(): Promise<InventorySummary> {
  return unwrap(await http.get<ApiResponse<InventorySummary>>('/summary'))
}

export async function getLowStock(): Promise<InventoryRow[]> {
  return unwrap(await http.get<ApiResponse<InventoryRow[]>>('/lowstock'))
}

export async function adjustInventory(data: AdjustInventoryRequest): Promise<void> {
  return unwrap(await http.post<ApiResponse<void>>('/adjust', data))
}
