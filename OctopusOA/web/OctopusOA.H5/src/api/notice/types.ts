export type NoticeType = '1' | '2' | '3'

export interface NoticeItem {
  noticeId: number
  title: string
  content: string
  noticeType: NoticeType
  priority: number
  publisher: string
  publishTime: string
  source: 'umc' | 'oa'
  status: number
  isRead: boolean
}
