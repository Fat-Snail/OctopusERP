import { get, post, put, del } from '@/utils/http'
import type { PagedResult, PageQuery } from '../types'
import type { PostResponse, CreatePostRequest, UpdatePostRequest } from './types'

interface PostQuery extends PageQuery {
  postName?: string
  postCode?: string
  status?: 0 | 1
}

/** GET /api/system/post/list — 职位分页列表 */
export function getPostList(params: PostQuery) {
  return get<PagedResult<PostResponse>>('/system/post/list', params)
}

/** GET /api/system/post/{id} — 职位详情 */
export function getPostById(id: number) {
  return get<PostResponse>(`/system/post/${id}`)
}

/** POST /api/system/post — 新增职位 */
export function createPost(data: CreatePostRequest) {
  return post<PostResponse>('/system/post', data)
}

/** PUT /api/system/post — 修改职位 */
export function updatePost(data: UpdatePostRequest) {
  return put<PostResponse>('/system/post', data)
}

/** DELETE /api/system/post/{ids} — 删除职位（批量，逗号分隔） */
export function deletePost(ids: string) {
  return del<null>(`/system/post/${ids}`)
}
