import { get, post, put, del } from '@/utils/http'
import type { MenuResponse, CreateMenuRequest, UpdateMenuRequest } from './types'

interface MenuQuery {
  menuName?: string
  status?: 0 | 1
  menuType?: 'M' | 'C' | 'F'
}

/** GET /api/system/menu/tree — 菜单树（供前端动态路由使用） */
export function getMenuTree() {
  return get<MenuResponse[]>('/system/menu/tree')
}

/** GET /api/system/menu/list — 菜单列表（管理用） */
export function getMenuList(params?: MenuQuery) {
  return get<MenuResponse[]>('/system/menu/list', params)
}

/** GET /api/system/menu/{id} — 菜单详情 */
export function getMenuById(id: number) {
  return get<MenuResponse>(`/system/menu/${id}`)
}

/** POST /api/system/menu — 新增菜单 */
export function createMenu(data: CreateMenuRequest) {
  return post<MenuResponse>('/system/menu', data)
}

/** PUT /api/system/menu — 修改菜单 */
export function updateMenu(data: UpdateMenuRequest) {
  return put<MenuResponse>('/system/menu', data)
}

/** DELETE /api/system/menu/{id} — 删除菜单 */
export function deleteMenu(id: number) {
  return del<null>(`/system/menu/${id}`)
}

/** GET /api/system/menu/role/{roleId} — 角色已绑定菜单 ID 列表 */
export function getRoleMenuIds(roleId: number) {
  return get<number[]>(`/system/menu/role/${roleId}`)
}
