import { http, unwrap } from '@/utils/http'
import type { PagedResult } from '@/api/types'
import type { NoticeItem } from './types'

export async function getNoticeList(params?: { type?: string }): Promise<PagedResult<NoticeItem>> {
  const res = await http.get<{ code: number; msg: string; data: PagedResult<NoticeItem> }>('/notice/list', { params })
  return unwrap(res)
}

export async function getNoticeById(id: number): Promise<NoticeItem> {
  const res = await http.get<{ code: number; msg: string; data: NoticeItem }>(`/notice/${id}`)
  return unwrap(res)
}

export async function getUnreadCount(): Promise<number> {
  const res = await http.get<{ code: number; msg: string; data: number }>('/notice/unread/count')
  return unwrap(res)
}
