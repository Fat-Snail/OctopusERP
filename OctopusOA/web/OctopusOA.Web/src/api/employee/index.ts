import type { ApiResponse } from '../types'
import type { EmployeeResponse, CreateEmployeeRequest, H5OnboardResponse, H5SubmitRequest } from './types'
import { createAuthedHttp, createPublicHttp } from '@/utils/http'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// HR 端（需登录）

export async function createEmployee(data: CreateEmployeeRequest): Promise<EmployeeResponse> {
  return unwrap(await http.post<ApiResponse<EmployeeResponse>>('/employee', data))
}

export async function getEmployeeList(params?: { status?: string; name?: string }): Promise<{ rows: EmployeeResponse[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: EmployeeResponse[]; total: number }>>('/employee/list', { params }))
}

export async function getEmployeeById(id: number): Promise<EmployeeResponse> {
  return unwrap(await http.get<ApiResponse<EmployeeResponse>>(`/employee/${id}`))
}

export async function updateEmployee(id: number, data: CreateEmployeeRequest): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/employee/${id}`, data))
}

export async function confirmEmployee(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/employee/${id}/confirm`, {}))
}

export async function rejectEmployee(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/employee/${id}/reject`, {}))
}

export async function resignEmployee(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/employee/${id}/resign`, {}))
}

export async function deleteEmployee(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/employee/${id}`))
}

// H5 端（无需登录，独立 axios 无 token）

const h5Http = createPublicHttp('/api/h5/onboard')

export async function h5GetOnboard(token: string): Promise<H5OnboardResponse> {
  return unwrap(await h5Http.get<ApiResponse<H5OnboardResponse>>(`/${token}`))
}

export async function h5Submit(token: string, data: H5SubmitRequest): Promise<void> {
  unwrap(await h5Http.post<ApiResponse<null>>(`/${token}`, data))
}
