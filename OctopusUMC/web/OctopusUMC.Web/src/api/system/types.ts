import type { Status } from '../types'

export type DataScope = '1' | '2' | '3' | '4' | '5'
export type MenuType = 'M' | 'C' | 'F'

export interface UserResponse {
  userId: number
  userName: string
  nickName: string
  email: string
  phoneNumber: string | null
  sex: string
  avatar: string | null
  status: Status
  deptId: number
  deptName: string
  postId: number | null
  postName: string | null
  deptIds: number[]
  postIds: number[]
  roles: string[]
  createTime: string
  remark: string | null
}

export interface CreateUserRequest {
  userName: string
  nickName: string
  email: string
  phoneNumber?: string
  sex: string
  password: string
  deptIds: number[]
  postIds: number[]
  roleIds: number[]
  status: Status
  remark?: string
}

export interface UpdateUserRequest {
  userId: number
  userName: string
  nickName: string
  email: string
  phoneNumber?: string
  sex: string
  deptIds: number[]
  postIds: number[]
  roleIds: number[]
  status: Status
  remark?: string
}

export interface UserQuery {
  pageNum: number
  pageSize: number
  userName?: string
  phoneNumber?: string
  status?: Status
  deptId?: number
}

export interface DeptResponse {
  deptId: number
  parentId: number
  deptName: string
  orderNum: number
  status: Status
  children: DeptResponse[]
  createTime: string
}

export interface CreateDeptRequest {
  parentId: number
  deptName: string
  orderNum: number
  status: Status
}

export interface UpdateDeptRequest extends CreateDeptRequest {
  deptId: number
}

export interface RoleResponse {
  roleId: number
  roleName: string
  roleKey: string
  roleSort: number
  dataScope: DataScope
  status: Status
  menuIds: number[]
  deptIds: number[]
  createTime: string
  remark: string | null
}

export interface CreateRoleRequest {
  roleName: string
  roleKey: string
  roleSort: number
  dataScope: DataScope
  menuIds: number[]
  status: Status
  remark?: string
}

export interface UpdateRoleRequest extends CreateRoleRequest {
  roleId: number
}

export interface MenuResponse {
  menuId: number
  parentId: number
  menuName: string
  menuType: MenuType
  path: string
  component: string | null
  permission: string | null
  icon: string | null
  orderNum: number
  status: Status
  isCache: boolean
  isFrame: boolean
  visible: boolean
  children: MenuResponse[]
}

export interface CreateMenuRequest {
  parentId: number
  menuName: string
  menuType: MenuType
  path: string
  component?: string
  permission?: string
  icon?: string
  orderNum: number
  status: Status
  isCache: boolean
  isFrame: boolean
  visible: boolean
}

export interface UpdateMenuRequest extends CreateMenuRequest {
  menuId: number
}

export interface PostResponse {
  postId: number
  postName: string
  postCode: string
  postSort: number
  status: Status
  createTime: string
  remark: string | null
}

export interface CreatePostRequest {
  postName: string
  postCode: string
  postSort: number
  status: Status
  remark?: string
}

export interface UpdatePostRequest extends CreatePostRequest {
  postId: number
}

export interface DictTypeResponse {
  dictId: number
  dictName: string
  dictType: string
  status: Status
  createTime: string
  remark: string | null
}

export interface DictDataResponse {
  dictCode: number
  dictType: string
  dictLabel: string
  dictValue: string
  dictSort: number
  status: Status
  isDefault: boolean
  createTime: string
  remark: string | null
}

export interface CreateDictTypeRequest {
  dictName: string
  dictType: string
  status: Status
  remark?: string
}

export interface CreateDictDataRequest {
  dictType: string
  dictLabel: string
  dictValue: string
  dictSort: number
  status: Status
  isDefault: boolean
  remark?: string
}

export interface UpdateDictTypeRequest {
  dictId: number
  dictName: string
  dictType: string
  status: Status
  remark?: string
}

export interface UpdateDictDataRequest {
  dictCode: number
  dictType: string
  dictLabel: string
  dictValue: string
  dictSort: number
  status: Status
  isDefault: boolean
  remark?: string
}

export interface ResetPasswordRequest {
  userId: number
  newPassword: string
}

export interface UpdateStatusRequest {
  userId: number
  status: Status
}

export interface RoleMenuRequest {
  roleId: number
  menuIds: number[]
}

export interface RoleDeptRequest {
  roleId: number
  dataScope: DataScope
  deptIds: number[]
}

export interface CreateOidcClientRequest {
  clientId: string
  clientName: string
  clientType: 'public' | 'confidential'
  redirectUris: string[]
  postLogoutRedirectUris: string[]
  status: Status
}

export interface UpdateOidcClientRequest extends CreateOidcClientRequest {
  id: string
}

export interface OidcClientResponse {
  id: string
  clientId: string
  clientName: string
  clientType: 'public' | 'confidential'
  redirectUris: string[]
  postLogoutRedirectUris: string[]
  status: Status
  createdAt: string
}

export interface NoticeResponse {
  noticeId: number
  noticeTitle: string
  noticeType: '1' | '2'
  noticeContent: string
  status: Status
  createBy: string
  createTime: string
  remark: string | null
}

export interface JobResponse {
  jobId: number
  jobName: string
  jobGroup: string
  invokeTarget: string
  cronExpression: string
  status: Status
  createTime: string
  remark: string | null
}

export interface ConfigResponse {
  configId: number
  configName: string
  configKey: string
  configValue: string
  configType: boolean
  createTime: string
  remark: string | null
}

export interface FileResponse {
  ossId: number
  fileName: string
  originalName: string
  fileSuffix: string
  url: string
  createTime: string
  createBy: string
  service: string
}
