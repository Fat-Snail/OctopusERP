import type { ApiResponse } from '../types'
import type { SyncUserResponse } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getOaUserList(): Promise<{ rows: SyncUserResponse[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: SyncUserResponse[]; total: number }>>('/oa/user/list'))
}

export async function updateOaRoles(id: number, oaRoles: string[]): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/oa/user/${id}/roles`, { oaRoles }))
}

export interface OaRoleItem {
  roleKey: string
  roleName: string
}

export async function getOaRoleList(): Promise<OaRoleItem[]> {
  return unwrap(await http.get<ApiResponse<OaRoleItem[]>>('/oa/role/list'))
}

export async function getCurrentUser(): Promise<SyncUserResponse> {
  return unwrap(await http.get<ApiResponse<SyncUserResponse>>('/me'))
}
