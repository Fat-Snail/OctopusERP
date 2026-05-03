import { get, post, put, del } from '@/utils/http'
import type { PagedResult, PageQuery } from '../types'
import type {
  RoleResponse,
  CreateRoleRequest,
  UpdateRoleRequest,
  UpdateStatusRequest,
  RoleMenuRequest,
  RoleDeptRequest
} from './types'

interface RoleQuery extends PageQuery {
  roleName?: string
  roleKey?: string
  status?: 0 | 1
}

/** GET /api/system/role/list — 角色分页列表 */
export function getRoleList(params: RoleQuery) {
  return get<PagedResult<RoleResponse>>('/system/role/list', params)
}

/** GET /api/system/role/{id} — 角色详情 */
export function getRoleById(id: number) {
  return get<RoleResponse>(`/system/role/${id}`)
}

/** POST /api/system/role — 新增角色 */
export function createRole(data: CreateRoleRequest) {
  return post<RoleResponse>('/system/role', data)
}

/** PUT /api/system/role — 修改角色 */
export function updateRole(data: UpdateRoleRequest) {
  return put<RoleResponse>('/system/role', data)
}

/** DELETE /api/system/role/{ids} — 删除角色（批量，逗号分隔） */
export function deleteRole(ids: string) {
  return del<null>(`/system/role/${ids}`)
}

/** PUT /api/system/role/status — 启用/禁用角色 */
export function updateRoleStatus(data: UpdateStatusRequest) {
  return put<null>('/system/role/status', data)
}

/** POST /api/system/role/menu — 角色绑定菜单权限 */
export function bindRoleMenus(data: RoleMenuRequest) {
  return post<null>('/system/role/menu', data)
}

/** POST /api/system/role/dept — 角色绑定数据权限（部门范围） */
export function bindRoleDepts(data: RoleDeptRequest) {
  return post<null>('/system/role/dept', data)
}
