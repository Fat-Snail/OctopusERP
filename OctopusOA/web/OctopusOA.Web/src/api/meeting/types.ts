export interface MeetingRoom {
  id: number
  name: string
  capacity: number
  location: string | null
  equipment: string[]
  description: string | null
  imageUrl: string | null
  status: number
  createTime: string
}

export interface MeetingBooking {
  id: number
  roomId: number
  roomName: string
  title: string
  umcUserId: number
  userName: string
  startTime: string
  endTime: string
  description: string | null
  attendees: number[]
  attendeeNames: string[]
  status: 'confirmed' | 'cancelled'
  createTime: string
}

export interface CreateBookingRequest {
  roomId: number
  title: string
  startTime: string  // ISO
  endTime: string
  description?: string
  attendees: number[]
}
