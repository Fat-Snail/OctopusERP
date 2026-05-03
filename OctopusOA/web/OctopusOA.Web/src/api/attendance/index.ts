import type { ApiResponse } from '../types'
import type { TodayAttendance, AttendanceItem, AttendanceRule, AttendanceStats, UserShift } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/attendance')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

export async function checkIn(): Promise<void> {
  unwrap(await http.post<ApiResponse<null>>('/check-in', {}))
}

export async function checkOut(): Promise<void> {
  unwrap(await http.post<ApiResponse<null>>('/check-out', {}))
}

export async function getToday(): Promise<TodayAttendance> {
  return unwrap(await http.get<ApiResponse<TodayAttendance>>('/today'))
}

export async function getMine(month?: string): Promise<{ rows: AttendanceItem[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: AttendanceItem[]; total: number }>>('/mine', { params: month ? { month } : {} }))
}

export async function getRule(): Promise<AttendanceRule> {
  return unwrap(await http.get<ApiResponse<AttendanceRule>>('/rule'))
}

export async function updateRule(data: AttendanceRule): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>('/rule', data))
}

export async function getStats(month?: string): Promise<{ rows: AttendanceStats[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: AttendanceStats[]; total: number }>>('/stats', { params: month ? { month } : {} }))
}

export async function getAbnormal(month?: string): Promise<{ rows: AttendanceItem[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: AttendanceItem[]; total: number }>>('/abnormal', { params: month ? { month } : {} }))
}

// ── 班次管理 ──

export async function getShiftList(): Promise<AttendanceRule[]> {
  return unwrap(await http.get<ApiResponse<AttendanceRule[]>>('/shift/list'))
}

export async function createShift(data: Partial<AttendanceRule>): Promise<AttendanceRule> {
  return unwrap(await http.post<ApiResponse<AttendanceRule>>('/shift', data))
}

export async function updateShift(data: AttendanceRule): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>('/shift', data))
}

export async function deleteShift(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/shift/${id}`))
}

export async function setDefaultShift(id: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>(`/shift/${id}/default`, {}))
}

export async function getUserShifts(): Promise<{ rows: UserShift[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: UserShift[]; total: number }>>('/user-shift/list'))
}

export async function assignUserShift(umcUserId: number, shiftId: number): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>('/user-shift', { umcUserId, shiftId }))
}
