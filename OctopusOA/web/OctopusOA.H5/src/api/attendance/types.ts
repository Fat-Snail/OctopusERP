export interface TodayAttendance {
  id: number | null
  date: string
  checkInTime: string | null
  checkOutTime: string | null
  checkInStatus: string
  checkOutStatus: string
  canCheckIn: boolean
  canCheckOut: boolean
  ruleWorkStart: string
  ruleWorkEnd: string
}

export interface AttendanceItem {
  id: number
  date: string
  checkInTime: string | null
  checkOutTime: string | null
  checkInStatus: string
  checkOutStatus: string
}

export interface AttendanceRule {
  ruleId: number
  ruleName: string
  workStartTime: string
  workEndTime: string
  lateThresholdMin: number
  earlyLeaveThresholdMin: number
  isDefault: boolean
}
