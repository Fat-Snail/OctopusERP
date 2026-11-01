export interface DashboardSummary {
  pendingApprovals: number
  unreadNotices: number
  myApprovalsCount: number
  todayMeetings: number
  todayCheckedIn: boolean
}

export interface TodoItem {
  id: number
  type: string
  title: string
  subtitle: string
  time: string
  tag: string | null
  tagType: string | null
}
