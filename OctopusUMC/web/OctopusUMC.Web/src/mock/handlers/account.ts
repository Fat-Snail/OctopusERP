import { http, HttpResponse } from 'msw'
import { usersData } from '../data/users'
import type { LoginResponse } from '@/api/types'

// 模拟已登录用户（Cookie 认证由 MSW 拦截模拟，无需真实 Cookie）
let loggedInUserId: number | null = null

function buildLoginResponse(userId: number): LoginResponse {
  const user = usersData.find(u => u.userId === userId)!
  return {
    userId: user.userId,
    userName: user.userName,
    nickName: user.nickName,
    avatar: user.avatar,
    roles: user.roles,
    // 超级管理员返回通配权限，其他角色返回空（Step 2 阶段暂无按钮权限数据）
    permissions: user.roles.includes('admin') ? ['*:*:*'] : []
  }
}

export const accountHandlers = [
  http.post('/api/account/login', async ({ request }) => {
    const body = await request.json() as { userName: string; password: string }
    const user = usersData.find(u => u.userName === body.userName)
    if (!user) {
      return HttpResponse.json({ code: 401, msg: '用户名或密码错误', data: null })
    }
    loggedInUserId = user.userId
    return HttpResponse.json({
      code: 200,
      msg: '登录成功',
      data: buildLoginResponse(user.userId)
    })
  }),

  http.post('/api/account/logout', () => {
    loggedInUserId = null
    return HttpResponse.json({ code: 200, msg: '退出成功', data: null })
  }),

  http.get('/api/account/me', () => {
    if (!loggedInUserId) {
      return HttpResponse.json({ code: 401, msg: '未登录', data: null }, { status: 401 })
    }
    return HttpResponse.json({
      code: 200,
      msg: 'success',
      data: buildLoginResponse(loggedInUserId)
    })
  })
]
