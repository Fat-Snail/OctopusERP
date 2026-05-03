import type { ApiResponse } from '../types'
import type { ContactDeptNode, ContactUser } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/contact')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getDeptTree(): Promise<ContactDeptNode[]> {
  return unwrap(await http.get<ApiResponse<ContactDeptNode[]>>('/dept/tree'))
}

export async function getUsers(params: { deptId?: number; keyword?: string }): Promise<{ rows: ContactUser[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: ContactUser[]; total: number }>>('/users', { params }))
}

export async function getUser(umcUserId: number): Promise<ContactUser> {
  return unwrap(await http.get<ApiResponse<ContactUser>>(`/user/${umcUserId}`))
}
