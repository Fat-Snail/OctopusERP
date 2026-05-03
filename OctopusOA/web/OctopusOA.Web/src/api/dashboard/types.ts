export interface TodayAttendanceSnapshot {
  checkedIn: boolean
  checkedOut: boolean
  checkInStatus: string
  checkOutStatus: string
  checkInTime: string | null
  checkOutTime: string | null
}

export interface DashboardSummary {
  pendingApprovals: number
  unreadNotices: number
  todayAttendance: TodayAttendanceSnapshot | null
  todayMeetings: number
  myApprovalsCount: number
}

export interface TodoItem {
  type: 'approval' | 'notice' | 'attendance' | 'meeting'
  id: number
  title: string
  subtitle: string
  time: string
  link: string
  tag: string | null
  tagType: 'success' | 'warning' | 'danger' | 'info' | null
}
