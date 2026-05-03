import { get, post, put, del } from '@/utils/http'
import type { PagedResult, PageQuery } from '../types'
import type { NoticeResponse } from './types'

interface NoticeQuery extends PageQuery {
  noticeTitle?: string
  noticeType?: '1' | '2'
}

interface CreateNoticeRequest {
  noticeTitle: string
  noticeType: '1' | '2'
  noticeContent: string
  status: 0 | 1
  remark?: string
}

interface UpdateNoticeRequest extends CreateNoticeRequest {
  noticeId: number
}

/** GET /api/system/notice/list — 公告分页列表 */
export function getNoticeList(params: NoticeQuery) {
  return get<PagedResult<NoticeResponse>>('/system/notice/list', params)
}

/** GET /api/system/notice/{id} — 公告详情 */
export function getNoticeById(id: number) {
  return get<NoticeResponse>(`/system/notice/${id}`)
}

/** POST /api/system/notice — 发布公告 */
export function createNotice(data: CreateNoticeRequest) {
  return post<NoticeResponse>('/system/notice', data)
}

/** PUT /api/system/notice — 修改公告 */
export function updateNotice(data: UpdateNoticeRequest) {
  return put<NoticeResponse>('/system/notice', data)
}

/** DELETE /api/system/notice/{ids} — 删除公告（批量，逗号分隔） */
export function deleteNotice(ids: string) {
  return del<null>(`/system/notice/${ids}`)
}
