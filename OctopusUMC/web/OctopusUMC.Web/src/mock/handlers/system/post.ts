import { http, HttpResponse } from 'msw'
import { postsData } from '../../data/posts'
import type { PostResponse, CreatePostRequest, UpdatePostRequest } from '@/api/system/types'

let posts = JSON.parse(JSON.stringify(postsData)) as PostResponse[]
let nextId = posts.length + 1

export const postHandlers = [
  http.get('/api/system/post/list', ({ request }) => {
    const url = new URL(request.url)
    const pageNum = Number(url.searchParams.get('pageNum') ?? 1)
    const pageSize = Number(url.searchParams.get('pageSize') ?? 10)
    const postName = url.searchParams.get('postName') ?? ''
    const postCode = url.searchParams.get('postCode') ?? ''
    const status = url.searchParams.get('status')

    let filtered = posts
    if (postName) filtered = filtered.filter(p => p.postName.includes(postName))
    if (postCode) filtered = filtered.filter(p => p.postCode.includes(postCode))
    if (status !== null && status !== '') filtered = filtered.filter(p => p.status === Number(status))

    const total = filtered.length
    const rows = filtered.slice((pageNum - 1) * pageSize, pageNum * pageSize)
    return HttpResponse.json({ code: 200, msg: 'success', data: { rows, total } })
  }),

  http.get('/api/system/post/:id', ({ params }) => {
    const post = posts.find(p => p.postId === Number(params['id']))
    if (!post) return HttpResponse.json({ code: 404, msg: '职位不存在', data: null })
    return HttpResponse.json({ code: 200, msg: 'success', data: post })
  }),

  http.post('/api/system/post', async ({ request }) => {
    const body = await request.json() as CreatePostRequest
    const newPost: PostResponse = {
      postId: nextId++,
      postName: body.postName,
      postCode: body.postCode,
      postSort: body.postSort,
      status: body.status,
      createTime: new Date().toISOString().replace('T', ' ').slice(0, 19),
      remark: body.remark ?? null
    }
    posts.push(newPost)
    return HttpResponse.json({ code: 200, msg: '新增成功', data: newPost })
  }),

  http.put('/api/system/post', async ({ request }) => {
    const body = await request.json() as UpdatePostRequest
    const idx = posts.findIndex(p => p.postId === body.postId)
    if (idx === -1) return HttpResponse.json({ code: 404, msg: '职位不存在', data: null })
    posts[idx] = { ...posts[idx], ...body }
    return HttpResponse.json({ code: 200, msg: '修改成功', data: posts[idx] })
  }),

  http.delete('/api/system/post/:ids', ({ params }) => {
    const ids = String(params['ids']).split(',').map(Number)
    posts = posts.filter(p => !ids.includes(p.postId))
    return HttpResponse.json({ code: 200, msg: '删除成功', data: null })
  })
]
