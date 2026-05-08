import type { ApiResponse, PagedResult, PageQuery } from '../types'
import type {
  CustomerListRow,
  CustomerDetail,
  CreateCustomerRequest,
  UpdateCustomerRequest,
  CreateContactRequest,
  CustomerQuery,
} from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/customer')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getCustomerList(
  params: CustomerQuery & PageQuery
): Promise<PagedResult<CustomerListRow>> {
  return unwrap(await http.get<ApiResponse<PagedResult<CustomerListRow>>>('', { params }))
}

export async function getCustomerById(id: number): Promise<CustomerDetail> {
  return unwrap(await http.get<ApiResponse<CustomerDetail>>(`/${id}`))
}

export async function createCustomer(data: CreateCustomerRequest): Promise<CustomerDetail> {
  return unwrap(await http.post<ApiResponse<CustomerDetail>>('', data))
}

export async function updateCustomer(data: UpdateCustomerRequest): Promise<CustomerDetail> {
  return unwrap(await http.put<ApiResponse<CustomerDetail>>('', data))
}

export async function deleteCustomer(id: number): Promise<void> {
  return unwrap(await http.delete<ApiResponse<void>>(`/${id}`))
}

export async function addContact(data: CreateContactRequest): Promise<void> {
  return unwrap(await http.post<ApiResponse<void>>('/contact', data))
}
