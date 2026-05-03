import { http, HttpResponse } from 'msw'
import { deptsData, flattenDepts } from '../../data/depts'
import type { DeptResponse, CreateDeptRequest, UpdateDeptRequest } from '@/api/system/types'

let deptTree = JSON.parse(JSON.stringify(deptsData)) as DeptResponse[]
let flatList = flattenDepts(deptTree)
let nextId = Math.max(...flatList.map(d => d.deptId)) + 1

function rebuildFlat() {
  flatList = flattenDepts(deptTree)
}

export const deptHandlers = [
  http.get('/api/system/dept', ({ request }) => {
    const url = new URL(request.url)
    const deptName = url.searchParams.get('deptName') ?? ''
    const status = url.searchParams.get('status')
    rebuildFlat()
    let result = flatList
    if (deptName) result = result.filter(d => d.deptName.includes(deptName))
    if (status !== null && status !== '') result = result.filter(d => d.status === Number(status))
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows: result, total: result.length } })
  }),

  http.get('/api/system/dept/tree', () => {
    return HttpResponse.json({ code: 200, msg: 'success', data: deptTree })
  }),

  http.get('/api/system/dept/:id', ({ params }) => {
    rebuildFlat()
    const dept = flatList.find(d => d.deptId === Number(params['id']))
    if (!dept) return HttpResponse.json({ code: 404, msg: '部门不存在', data: null })
    return HttpResponse.json({ code: 200, msg: 'success', data: dept })
  }),

  http.post('/api/system/dept', async ({ request }) => {
    const body = await request.json() as CreateDeptRequest
    const newDept: DeptResponse = {
      deptId: nextId++,
      parentId: body.parentId,
      deptName: body.deptName,
      orderNum: body.orderNum,
      status: body.status,
      children: [],
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19)
    }
    rebuildFlat()
    const parent = flatList.find(d => d.deptId === body.parentId)
    if (parent) parent.children.push(newDept)
    else deptTree.push(newDept)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newDept })
  }),

  http.put('/api/system/dept', async ({ request }) => {
    const body = await request.json() as UpdateDeptRequest
    rebuildFlat()
    const dept = flatList.find(d => d.deptId === body.deptId)
    if (!dept) return HttpResponse.json({ code: 404, msg: '部门不存在', data: null })
    dept.deptName = body.deptName
    dept.orderNum = body.orderNum
    dept.status = body.status
    return HttpResponse.json({ code: 200, msg: '修改成功', data: dept })
  }),

  http.delete('/api/system/dept/:id', ({ params }) => {
    const id = Number(params['id'])
    rebuildFlat()
    function removeFromTree(list: DeptResponse[]): boolean {
      const idx = list.findIndex(d => d.deptId === id)
      if (idx !== -1) { list.splice(idx, 1); return true }
      return list.some(d => removeFromTree(d.children))
    }
    removeFromTree(deptTree)
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  })
]
