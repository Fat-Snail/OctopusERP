import { http, unwrap } from '@/utils/http'
import type { MeetingRoom, MeetingBooking, CreateBookingRequest } from './types'

export async function getRoomList(): Promise<MeetingRoom[]> {
  const res = await http.get<{ code: number; msg: string; data: MeetingRoom[] }>('/meeting/room/list')
  return unwrap(res)
}

export async function getMineBookings(): Promise<{ rows: MeetingBooking[]; total: number }> {
  const res = await http.get<{ code: number; msg: string; data: { rows: MeetingBooking[]; total: number } }>('/meeting/booking/mine')
  return unwrap(res)
}

export async function book(data: CreateBookingRequest): Promise<void> {
  const res = await http.post<{ code: number; msg: string; data: null }>('/meeting/booking', data, { validateStatus: () => true })
  if (res.data.code !== 200) throw new Error(res.data.msg || '预订失败')
}

export async function cancelBooking(id: number): Promise<void> {
  const res = await http.put<{ code: number; msg: string; data: null }>(`/meeting/booking/${id}/cancel`, {}, { validateStatus: () => true })
  if (res.data.code !== 200) throw new Error(res.data.msg || '取消失败')
}
