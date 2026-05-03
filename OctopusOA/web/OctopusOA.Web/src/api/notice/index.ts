import type { ApiResponse } from '../types'
import type { NoticeItem } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/notice')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function getNoticeList(params?: { type?: string; status?: number }): Promise<{ rows: NoticeItem[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: NoticeItem[]; total: number }>>('/list', { params }))
}

export async function getNoticeById(id: number): Promise<NoticeItem> {
  return unwrap(await http.get<ApiResponse<NoticeItem>>(`/${id}`))
}

export async function getUnreadCount(): Promise<number> {
  return unwrap(await http.get<ApiResponse<number>>('/unread/count'))
}

export async function markNoticeRead(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/${id}/read`, {}))
}

export async function getLatestNotices(limit = 5): Promise<NoticeItem[]> {
  return unwrap(await http.get<ApiResponse<NoticeItem[]>>('/latest', { params: { limit } }))
}
