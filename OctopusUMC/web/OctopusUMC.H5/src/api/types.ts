export interface ApiResponse<T = unknown> {
  code: number
  msg: string
  data: T
}

export interface LoginRequest {
  userName: string
  password: string
}

export interface LoginResponse {
  userId: number
  userName: string
  nickName: string
  email: string
  phoneNumber: string | null
  avatar: string | null
  roles: string[]
  permissions: string[]
}
