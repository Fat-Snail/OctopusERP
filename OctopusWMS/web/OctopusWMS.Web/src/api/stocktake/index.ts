import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { StocktakeListRow, StocktakeDetail, SubmitStocktakeRequest, StocktakeQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/stocktake')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getStocktakeList(params: StocktakeQuery & PageQuery): Promise<PagedResult<StocktakeListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<StocktakeListRow>>>('', { params }))
}

export async function getStocktakeById(id: number): Promise<StocktakeDetail> {
  return unwrap(await http.get<ApiResponse<StocktakeDetail>>(`/${id}`))
}

export async function createStocktake(warehouseId: number, remark?: string): Promise<StocktakeDetail> {
  return unwrap(await http.post<ApiResponse<StocktakeDetail>>('', { warehouseId, remark }))
}

export async function submitStocktake(data: SubmitStocktakeRequest): Promise<StocktakeDetail> {
  return unwrap(await http.put<ApiResponse<StocktakeDetail>>('/submit', data))
}
