import type { RoleResponse } from '@/api/system/types'

export const rolesData: RoleResponse[] = [
  {
    roleId: 1,
    roleName: '超级管理员',
    roleKey: 'admin',
    roleSort: 1,
    dataScope: '1',
    status: 1,
    menuIds: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
    deptIds: [],
    createTime: '2024-01-01 00:00:00',
    remark: '超级管理员拥有最高权限'
  },
  {
    roleId: 2,
    roleName: '普通用户',
    roleKey: 'common',
    roleSort: 2,
    dataScope: '2',
    status: 1,
    menuIds: [1, 2, 3],
    deptIds: [],
    createTime: '2024-01-01 00:00:00',
    remark: '普通用户权限'
  },
  {
    roleId: 3,
    roleName: '编辑员',
    roleKey: 'editor',
    roleSort: 3,
    dataScope: '3',
    status: 1,
    menuIds: [1, 2, 3, 4, 5],
    deptIds: [],
    createTime: '2024-02-01 00:00:00',
    remark: '内容编辑权限'
  }
]
