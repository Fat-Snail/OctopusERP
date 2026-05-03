export type NoticeType = '1' | '2' | '3'  // 1=通知 2=公告 3=紧急

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
