import { http, HttpResponse } from 'msw'
import { dictTypesData, dictDataItems } from '../../data/dicts'
import type { DictTypeResponse, DictDataResponse, CreateDictTypeRequest, CreateDictDataRequest } from '@/api/system/types'

let dictTypes = JSON.parse(JSON.stringify(dictTypesData)) as DictTypeResponse[]
let dictData = JSON.parse(JSON.stringify(dictDataItems)) as DictDataResponse[]
let nextTypeId = dictTypes.length + 1
let nextDataCode = dictData.length + 1

export const dictHandlers = [
  http.get('/api/system/dict/type/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const dictName = url.searchParams.get('dictName') ?? ''
    const dictType = url.searchParams.get('dictType') ?? ''
    const status = url.searchParams.get('status')

    let filtered = dictTypes
    if (dictName) filtered = filtered.filter(d => d.dictName.includes(dictName))
    if (dictType) filtered = filtered.filter(d => d.dictType.includes(dictType))
    if (status !== null && status !== '') filtered = filtered.filter(d => d.status === Number(status))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.get('/api/system/dict/type/:id', ({ params }) => {
    const id = params['id']
    const dt = isNaN(Number(id))
      ? dictTypes.find(d => d.dictType === id)
      : dictTypes.find(d => d.dictId === Number(id))
    if (!dt) return HttpResponse.json({ code: 404, msg: '字典不存在', data: null })
    return HttpResponse.json({ code: 200, msg: 'success', data: dt })
  }),

  http.post('/api/system/dict/type', async ({ request }) => {
    const body = await request.json() as CreateDictTypeRequest
    const newDt: DictTypeResponse = {
      dictId: nextTypeId++,
      dictName: body.dictName,
      dictType: body.dictType,
      status: body.status,
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
      remark: body.remark ?? null
    }
    dictTypes.push(newDt)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newDt })
  }),

  http.delete('/api/system/dict/type/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    dictTypes = dictTypes.filter(d => !ids.includes(d.dictId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  }),

  http.get('/api/system/dict/data/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const dictType = url.searchParams.get('dictType') ?? ''

    let filtered = dictData
    if (dictType) filtered = filtered.filter(d => d.dictType === dictType)

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.post('/api/system/dict/data', async ({ request }) => {
    const body = await request.json() as CreateDictDataRequest
    const newData: DictDataResponse = {
      dictCode: nextDataCode++,
      dictType: body.dictType,
      dictLabel: body.dictLabel,
      dictValue: body.dictValue,
      dictSort: body.dictSort,
      status: body.status,
      isDefault: body.isDefault,
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
      remark: body.remark ?? null
    }
    dictData.push(newData)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newData })
  }),

  http.delete('/api/system/dict/data/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    dictData = dictData.filter(d => !ids.includes(d.dictCode))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  })
]
