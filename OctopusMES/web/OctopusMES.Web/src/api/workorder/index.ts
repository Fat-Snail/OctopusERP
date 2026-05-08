import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { WorkOrderListRow, WorkOrderDetail, CreateWorkOrderRequest, WorkOrderQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/workorder')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getWorkOrderList(params: WorkOrderQuery & PageQuery): Promise<PagedResult<WorkOrderListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<WorkOrderListRow>>>('', { params }))
}

export async function getWorkOrderById(id: number): Promise<WorkOrderDetail> {
  return unwrap(await http.get<ApiResponse<WorkOrderDetail>>(`/${id}`))
}

export async function createWorkOrder(data: CreateWorkOrderRequest): Promise<WorkOrderDetail> {
  return unwrap(await http.post<ApiResponse<WorkOrderDetail>>('', data))
}

export async function startWorkOrder(id: number): Promise<WorkOrderDetail> {
  return unwrap(await http.put<ApiResponse<WorkOrderDetail>>(`/${id}/start`, {}))
}

export async function completeWorkOrder(id: number, completedQuantity: number): Promise<WorkOrderDetail> {
  return unwrap(await http.put<ApiResponse<WorkOrderDetail>>(`/${id}/complete`, { completedQuantity }))
}
