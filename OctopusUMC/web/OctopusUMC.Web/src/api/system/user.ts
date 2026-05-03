import { get, post, put, del } from '@/utils/http'
import type { PagedResult } from '../types'
import type {
  UserResponse,
  CreateUserRequest,
  UpdateUserRequest,
  ResetPasswordRequest,
  UpdateStatusRequest,
  UserQuery
} from './types'

/** GET /api/system/user/list — 用户分页列表 */
export function getUserList(params: UserQuery) {
  return get<PagedResult<UserResponse>>('/system/user/list', params)
}

/** GET /api/system/user/{id} — 用户详情 */
export function getUserById(id: number) {
  return get<UserResponse>(`/system/user/${id}`)
}

/** POST /api/system/user — 新增用户 */
export function createUser(data: CreateUserRequest) {
  return post<UserResponse>('/system/user', data)
}

/** PUT /api/system/user — 修改用户 */
export function updateUser(data: UpdateUserRequest) {
  return put<UserResponse>('/system/user', data)
}

/** DELETE /api/system/user/{ids} — 删除用户（批量，逗号分隔） */
export function deleteUser(ids: string) {
  return del<null>(`/system/user/${ids}`)
}

/** PUT /api/system/user/status — 启用/禁用用户 */
export function updateUserStatus(data: UpdateStatusRequest) {
  return put<null>('/system/user/status', data)
}

/** PUT /api/system/user/resetPwd — 重置密码 */
export function resetUserPassword(data: ResetPasswordRequest) {
  return put<null>('/system/user/resetPwd', data)
}

/** GET /api/system/user/authRole/{userId} — 用户已绑定角色 */
export function getUserAuthRole(userId: number) {
  return get<{ assignedRoles: unknown[]; roles: unknown[] }>(`/system/user/authRole/${userId}`)
}

/** PUT /api/system/user/authRole — 为用户绑定角色 */
export function bindUserRoles(userId: number, roleIds: number[]) {
  return put<null>(`/system/user/authRole?userId=${userId}&roleIds=${roleIds.join(',')}`, null)
}
