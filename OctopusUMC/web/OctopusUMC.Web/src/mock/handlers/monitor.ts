import { http, HttpResponse } from 'msw'
import { onlineUsersData, operlogsData, loginInfosData, serverInfoData } from '../data/monitor'
import type { OnlineUserResponse, OperlogResponse, LoginfoResponse } from '@/api/monitor/types'

let onlineUsers = JSON.parse(JSON.stringify(onlineUsersData)) as OnlineUserResponse[]
let operlogs = JSON.parse(JSON.stringify(operlogsData)) as OperlogResponse[]
let loginInfos = JSON.parse(JSON.stringify(loginInfosData)) as LoginfoResponse[]

export const monitorHandlers = [
  http.get('/api/monitor/online/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const userName = url.searchParams.get('userName') ?? ''
    const ipaddr = url.searchParams.get('ipaddr') ?? ''

    let filtered = onlineUsers
    if (userName) filtered = filtered.filter(u => u.userName.includes(userName))
    if (ipaddr) filtered = filtered.filter(u => u.ipaddr.includes(ipaddr))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.delete('/api/monitor/online/:tokenId', ({ params }) => {
    onlineUsers = onlineUsers.filter(u => u.tokenId !== params['tokenId'])
    return HttpResponse.json({ code: 200, msg: '强制下线成功', data: null })
  }),

  http.get('/api/monitor/server', () => {
    return HttpResponse.json({ code: 200, msg: 'success', data: serverInfoData })
  }),

  http.get('/api/monitor/operlog/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const operName = url.searchParams.get('operName') ?? ''
    const title = url.searchParams.get('title') ?? ''
    const status = url.searchParams.get('status')

    let filtered = operlogs
    if (operName) filtered = filtered.filter(l => l.operName.includes(operName))
    if (title) filtered = filtered.filter(l => l.title.includes(title))
    if (status !== null && status !== '') filtered = filtered.filter(l => l.status === Number(status))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.delete('/api/monitor/operlog/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    operlogs = operlogs.filter(l => !ids.includes(l.operId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  http.delete('/api/monitor/operlog/clean', () => {
    operlogs = []
    return HttpResponse.json({ code: 200, msg: '清空成功', data: null })
  }),

  http.get('/api/monitor/logininfor/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const userName = url.searchParams.get('userName') ?? ''
    const ipaddr = url.searchParams.get('ipaddr') ?? ''
    const status = url.searchParams.get('status')

    let filtered = loginInfos
    if (userName) filtered = filtered.filter(l => l.userName.includes(userName))
    if (ipaddr) filtered = filtered.filter(l => l.ipaddr.includes(ipaddr))
    if (status !== null && status !== '') filtered = filtered.filter(l => l.status === Number(status))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.delete('/api/monitor/logininfor/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    loginInfos = loginInfos.filter(l => !ids.includes(l.infoId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  http.delete('/api/monitor/logininfor/clean', () => {
    loginInfos = []
    return HttpResponse.json({ code: 200, msg: '清空成功', data: null })
  })
]
