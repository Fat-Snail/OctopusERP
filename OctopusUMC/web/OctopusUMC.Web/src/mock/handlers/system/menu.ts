import { http, HttpResponse } from 'msw'
import { menusData, flattenMenus } from '../../data/menus'
import type { MenuResponse, CreateMenuRequest, UpdateMenuRequest } from '@/api/system/types'

let menuTree = JSON.parse(JSON.stringify(menusData)) as MenuResponse[]
let nextId = Math.max(...flattenMenus(menuTree).map(m => m.menuId)) + 1

function getFlat() {
  return flattenMenus(menuTree)
}

export const menuHandlers = [
  http.get('/api/system/menu', ({ request }) => {
    const url = new URL(request.url)
    const menuName = url.searchParams.get('menuName') ?? ''
    const status = url.searchParams.get('status')
    let result = getFlat()
    if (menuName) result = result.filter(m => m.menuName.includes(menuName))
    if (status !== null && status !== '') result = result.filter(m => m.status === Number(status))
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows: result, total: result.length } })
  }),

  http.get('/api/system/menu/tree', () => {
    return HttpResponse.json({ code: 200, msg: 'success', data: menuTree })
  }),

  http.get('/api/system/menu/:id', ({ params }) => {
    const menu = getFlat().find(m => m.menuId === Number(params['id']))
    if (!menu) return HttpResponse.json({ code: 404, msg: '菜单不存在', data: null })
    return HttpResponse.json({ code: 200, msg: 'success', data: menu })
  }),

  http.post('/api/system/menu', async ({ request }) => {
    const body = await request.json() as CreateMenuRequest
    const newMenu: MenuResponse = {
      menuId: nextId++,
      parentId: body.parentId,
      menuName: body.menuName,
      menuType: body.menuType,
      path: body.path,
      component: body.component ?? null,
      permission: body.permission ?? null,
      icon: body.icon ?? null,
      orderNum: body.orderNum,
      status: body.status,
      isCache: body.isCache,
      isFrame: body.isFrame,
      visible: body.visible,
      children: []
    }
    const parent = getFlat().find(m => m.menuId === body.parentId)
    if (parent) parent.children.push(newMenu)
    else menuTree.push(newMenu)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newMenu })
  }),

  http.put('/api/system/menu', async ({ request }) => {
    const body = await request.json() as UpdateMenuRequest
    const menu = getFlat().find(m => m.menuId === body.menuId)
    if (!menu) return HttpResponse.json({ code: 404, msg: '菜单不存在', data: null })
    Object.assign(menu, body)
    return HttpResponse.json({ code: 200, msg: '修改成功', data: menu })
  }),

  http.delete('/api/system/menu/:id', ({ params }) => {
    const id = Number(params['id'])
    function removeFromTree(list: MenuResponse[]): boolean {
      const idx = list.findIndex(m => m.menuId === id)
      if (idx !== -1) { list.splice(idx, 1); return true }
      return list.some(m => removeFromTree(m.children))
    }
    removeFromTree(menuTree)
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  })
]
