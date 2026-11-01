import { http, unwrap } from '@/utils/http'
import type { PagedResult } from '@/api/types'
import type { TodayAttendance, AttendanceItem } from './types'

export async function checkIn(): Promise<void> {
  const res = await http.post<{ code: number; msg: string; data: null }>('/attendance/check-in', {})
  unwrap(res)
}

export async function checkOut(): Promise<void> {
  const res = await http.post<{ code: number; msg: string; data: null }>('/attendance/check-out', {})
  unwrap(res)
}

export async function getToday(): Promise<TodayAttendance> {
  const res = await http.get<{ code: number; msg: string; data: TodayAttendance }>('/attendance/today')
  return unwrap(res)
}

export async function getMine(month: string): Promise<PagedResult<AttendanceItem>> {
  const res = await http.get<{ code: number; msg: string; data: PagedResult<AttendanceItem> }>('/attendance/mine', { params: { month } })
  return unwrap(res)
}
