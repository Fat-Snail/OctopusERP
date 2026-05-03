import type { ApiResponse } from '../types'
import type { MeetingRoom, MeetingBooking, CreateBookingRequest } from './types'
import { createAuthedHttp } from '@/utils/http'

const http = createAuthedHttp('/api/meeting')

function unwrap<T>(res: { data: ApiResponse<T> }): T {
  if (res.data.code !== 200) throw new Error(res.data.msg || '请求失败')
  return res.data.data
}

// ── 会议室 ──

export async function getRoomList(): Promise<MeetingRoom[]> {
  return unwrap(await http.get<ApiResponse<MeetingRoom[]>>('/room/list'))
}

export async function getRoom(id: number): Promise<MeetingRoom> {
  return unwrap(await http.get<ApiResponse<MeetingRoom>>(`/room/${id}`))
}

export async function createRoom(data: Partial<MeetingRoom>): Promise<MeetingRoom> {
  return unwrap(await http.post<ApiResponse<MeetingRoom>>('/room', data))
}

export async function updateRoom(data: MeetingRoom): Promise<void> {
  unwrap(await http.put<ApiResponse<null>>('/room', data))
}

export async function deleteRoom(id: number): Promise<void> {
  unwrap(await http.delete<ApiResponse<null>>(`/room/${id}`))
}

export async function getRoomCalendar(id: number, date: string, range: 'day' | 'week' = 'week'): Promise<MeetingBooking[]> {
  return unwrap(await http.get<ApiResponse<MeetingBooking[]>>(`/room/${id}/calendar`, { params: { date, range } }))
}

// ── 预订 ──

export async function book(data: CreateBookingRequest): Promise<MeetingBooking> {
  // 注意：axios 会跳过 transform response（这里允许 200/409）。我们用 raw 响应判断
  const resp = await http.post<ApiResponse<MeetingBooking>>('/booking', data, { validateStatus: () => true })
  if (resp.data.code !== 200) throw new Error(resp.data.msg || '预订失败')
  return resp.data.data
}

export async function cancelBooking(id: number): Promise<void> {
  const resp = await http.put<ApiResponse<null>>(`/booking/${id}/cancel`, {}, { validateStatus: () => true })
  if (resp.data.code !== 200) throw new Error(resp.data.msg || '取消失败')
}

export async function getMineBookings(): Promise<{ rows: MeetingBooking[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: MeetingBooking[]; total: number }>>('/booking/mine'))
}

export async function getTodayBookings(): Promise<{ rows: MeetingBooking[]; total: number }> {
  return unwrap(await http.get<ApiResponse<{ rows: MeetingBooking[]; total: number }>>('/booking/today'))
}

export async function getAllCalendar(date: string, range: 'day' | 'week' = 'week'): Promise<MeetingBooking[]> {
  return unwrap(await http.get<ApiResponse<MeetingBooking[]>>('/calendar', { params: { date, range } }))
}
