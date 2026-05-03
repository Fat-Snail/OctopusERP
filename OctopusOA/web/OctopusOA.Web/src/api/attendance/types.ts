export type CheckStatus = 'normal' | 'late' | 'early' | 'missing' | 'weekend'

export interface TodayAttendance {
  date: string
  checkInTime: string | null
  checkOutTime: string | null
  checkInStatus: CheckStatus
  checkOutStatus: CheckStatus
  canCheckIn: boolean
  canCheckOut: boolean
  ruleWorkStart: string
  ruleWorkEnd: string
}

export interface AttendanceItem {
  id: number
  umcUserId: number
  userName: string
  nickName: string
  date: string
  checkInTime: string | null
  checkOutTime: string | null
  checkInStatus: CheckStatus
  checkOutStatus: CheckStatus
  workHours: number
  isFixed: boolean
}

export interface AttendanceRule {
  id: number
  code: string
  name: string
  workStartTime: string
  workEndTime: string
  lateThresholdMin: number
  earlyLeaveThresholdMin: number
  ipWhiteList: string | null
  isDefault: boolean
  status: number
}

export interface UserShift {
  umcUserId: number
  userName: string
  nickName: string
  shiftId: number
  shiftName: string
  shiftCode: string
  workStartTime: string
  workEndTime: string
}

export interface AttendanceStats {
  umcUserId: number
  userName: string
  nickName: string
  normalDays: number
  lateCount: number
  earlyLeaveCount: number
  missingCount: number
  totalWorkHours: number
}
