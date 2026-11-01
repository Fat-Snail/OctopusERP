import { http, unwrap } from '@/utils/http'
import type { PagedResult } from '@/api/types'
import type { ContactDeptNode, ContactUser } from './types'

export async function getDeptTree(): Promise<ContactDeptNode[]> {
  const res = await http.get<{ code: number; msg: string; data: ContactDeptNode[] }>('/contact/dept/tree')
  return unwrap(res)
}

export async function getUsers(params?: { deptId?: number; keyword?: string }): Promise<PagedResult<ContactUser>> {
  const res = await http.get<{ code: number; msg: string; data: PagedResult<ContactUser> }>('/contact/users', { params })
  return unwrap(res)
}

export async function getUser(umcUserId: number): Promise<ContactUser> {
  const res = await http.get<{ code: number; msg: string; data: ContactUser }>(`/contact/user/${umcUserId}`)
  return unwrap(res)
}
