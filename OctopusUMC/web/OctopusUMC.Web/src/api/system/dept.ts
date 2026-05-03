import { get, post, put, del } from '@/utils/http'
import type { DeptResponse, CreateDeptRequest, UpdateDeptRequest } from './types'

interface DeptQuery {
  deptName?: string
  status?: 0 | 1
}

/** GET /api/system/dept/tree — 部门树 */
export function getDeptTree() {
  return get<DeptResponse[]>('/system/dept/tree')
}

/** GET /api/system/dept/list — 部门列表 */
export function getDeptList(params?: DeptQuery) {
  return get<DeptResponse[]>('/system/dept/list', params)
}

/** GET /api/system/dept/{id} — 部门详情 */
export function getDeptById(id: number) {
  return get<DeptResponse>(`/system/dept/${id}`)
}

/** GET /api/system/dept/exclude/{id} — 排除指定节点的部门树（编辑时用） */
export function getDeptTreeExclude(id: number) {
  return get<DeptResponse[]>(`/system/dept/exclude/${id}`)
}

/** POST /api/system/dept — 新增部门 */
export function createDept(data: CreateDeptRequest) {
  return post<DeptResponse>('/system/dept', data)
}

/** PUT /api/system/dept — 修改部门 */
export function updateDept(data: UpdateDeptRequest) {
  return put<DeptResponse>('/system/dept', data)
}

/** DELETE /api/system/dept/{id} — 删除部门 */
export function deleteDept(id: number) {
  return del<null>(`/system/dept/${id}`)
}
