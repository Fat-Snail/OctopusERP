import { http, HttpResponse } from 'msw'
import { rolesData } from '../../data/roles'
import type { RoleResponse, CreateRoleRequest, UpdateRoleRequest } from '@/api/system/types'

let roles = JSON.parse(JSON.stringify(rolesData)) as RoleResponse[]
let nextId = roles.length + 1

export const roleHandlers = [
  http.get('/api/system/role/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const roleName = url.searchParams.get('roleName') ?? ''
    const roleKey = url.searchParams.get('roleKey') ?? ''
    const status = url.searchParams.get('status')

    let filtered = roles
    if (roleName) filtered = filtered.filter(r => r.roleName.includes(roleName))
    if (roleKey) filtered = filtered.filter(r => r.roleKey.includes(roleKey))
    if (status !== null && status !== '') filtered = filtered.filter(r => r.status === Number(status))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.get('/api/system/role/:id', ({ params }) => {
    const role = roles.find(r => r.roleId === Number(params['id']))
    if (!role) return HttpResponse.json({ code: 404, msg: '角色不存在', data: null })
    return HttpResponse.json({ code: 200, msg: 'success', data: role })
  }),

  http.post('/api/system/role', async ({ request }) => {
    const body = await request.json() as CreateRoleRequest
    const newRole: RoleResponse = {
      roleId: nextId++,
      roleName: body.roleName,
      roleKey: body.roleKey,
      roleSort: body.roleSort,
      dataScope: body.dataScope,
      status: body.status,
      menuIds: body.menuIds,
      deptIds: [],
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
      remark: body.remark ?? null
    }
    roles.push(newRole)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newRole })
  }),

  http.put('/api/system/role', async ({ request }) => {
    const body = await request.json() as UpdateRoleRequest
    const idx = roles.findIndex(r => r.roleId === body.roleId)
    if (idx === -1) return HttpResponse.json({ code: 404, msg: '角色不存在', data: null })
    roles[idx] = { ...roles[idx], ...body }
    return HttpResponse.json({ code: 200, msg: '修改成功', data: roles[idx] })
  }),

  http.delete('/api/system/role/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    roles = roles.filter(r => !ids.includes(r.roleId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  http.post('/api/system/role/menu', async ({ request }) => {
    const body = await request.json() as { roleId: number; menuIds: number[] }
    const role = roles.find(r => r.roleId === body.roleId)
    if (role) role.menuIds = body.menuIds
    return HttpResponse.json({ code: 200, msg: '菜单绑定成功', data: null })
  }),

  http.post('/api/system/role/dept', async ({ request }) => {
    const body = await request.json() as { roleId: number; deptIds: number[]; dataScope: string }
    const role = roles.find(r => r.roleId === body.roleId)
    if (role) {
      role.deptIds = body.deptIds
      role.dataScope = body.dataScope as RoleResponse['dataScope']
    }
    return HttpResponse.json({ code: 200, msg: '数据权限设置成功', data: null })
  })
]
