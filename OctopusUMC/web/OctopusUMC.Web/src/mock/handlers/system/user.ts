import { http, HttpResponse } from 'msw'
import { usersData } from '../../data/users'
import type { UserResponse, CreateUserRequest, UpdateUserRequest } from '@/api/system/types'

let users = [...usersData]
let nextId = users.length + 1

export const userHandlers = [
  http.get('/api/system/user/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const userName = url.searchParams.get('userName') ?? ''
    const phoneNumber = url.searchParams.get('phoneNumber') ?? ''
    const status = url.searchParams.get('status')
    const deptId = url.searchParams.get('deptId')

    let filtered = users
    if (userName) filtered = filtered.filter(u => u.userName.includes(userName))
    if (phoneNumber) filtered = filtered.filter(u => u.phoneNumber?.includes(phoneNumber))
    if (status !== null && status !== '') filtered = filtered.filter(u => u.status === Number(status))
    if (deptId) filtered = filtered.filter(u => u.deptId === Number(deptId))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.get('/api/system/user/:id', ({ params }) => {
    const user = users.find(u => u.userId === Number(params['id']))
    if (!user) return HttpResponse.json({ code: 404, msg: '用户不存在', data: null })
    return HttpResponse.json({ code: 200, msg: 'success', data: user })
  }),

  http.post('/api/system/user', async ({ request }) => {
    const body = await request.json() as CreateUserRequest
    const newUser: UserResponse = {
      userId: nextId++,
      userName: body.userName,
      nickName: body.nickName,
      email: body.email,
      phoneNumber: body.phoneNumber ?? null,
      sex: body.sex,
      avatar: null,
      status: body.status,
      deptId: body.deptIds?.[0] ?? 0,
      deptName: '未知部门',
      postId: body.postIds?.[0] ?? null,
      postName: null,
      deptIds: body.deptIds ?? [],
      postIds: body.postIds ?? [],
      roles: [],
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
      remark: body.remark ?? null
    }
    users.push(newUser)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newUser })
  }),

  http.put('/api/system/user', async ({ request }) => {
    const body = await request.json() as UpdateUserRequest
    const idx = users.findIndex(u => u.userId === body.userId)
    if (idx === -1) return HttpResponse.json({ code: 404, msg: '用户不存在', data: null })
    users[idx] = { ...users[idx], ...body }
    return HttpResponse.json({ code: 200, msg: '修改成功', data: users[idx] })
  }),

  http.delete('/api/system/user/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    users = users.filter(u => !ids.includes(u.userId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  http.put('/api/system/user/resetPwd', async ({ request }) => {
    const body = await request.json() as { userId: number; password: string }
    const user = users.find(u => u.userId === body.userId)
    if (!user) return HttpResponse.json({ code: 404, msg: '用户不存在', data: null })
    return HttpResponse.json({ code: 200, msg: '密码重置成功', data: null })
  }),

  http.put('/api/system/user/status', async ({ request }) => {
    const body = await request.json() as { userId: number; status: 0 | 1 }
    const user = users.find(u => u.userId === body.userId)
    if (!user) return HttpResponse.json({ code: 404, msg: '用户不存在', data: null })
    user.status = body.status
    return HttpResponse.json({ code: 200, msg: '状态修改成功', data: null })
  }),

  http.put('/api/system/user/authRole', async ({ request }) => {
    const body = await request.json() as { userId: number; roleIds: number[] }
    return HttpResponse.json({ code: 200, msg: '授权成功', data: null })
  })
]
