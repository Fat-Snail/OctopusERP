import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type { ContractListRow, ContractDetail, ContractQuery } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/contract')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getContractList(
  params: ContractQuery & PageQuery
): Promise<PagedResult<ContractListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<ContractListRow>>>('', { params }))
}

export async function getContractById(id: number): Promise<ContractDetail> {
  return unwrap(await http.get<ApiResponse<ContractDetail>>(`/${id}`))
}

export async function submitContract(id: number): Promise<void> {
  return unwrap(await http.put<ApiResponse<void>>(`/${id}/submit`))
}

export async function shipContract(id: number): Promise<void> {
  return unwrap(await http.put<ApiResponse<void>>(`/${id}/ship`))
}

export async function completeContract(id: number): Promise<void> {
  return unwrap(await http.put<ApiResponse<void>>(`/${id}/complete`))
}
