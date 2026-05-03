import type { Status } from '../types'

export interface SyncUserResponse {
  id: number
  umcUserId: number
  userName: string
  nickName: string
  email: string
  phoneNumber: string | null
  avatar: string | null
  status: Status
  lastSyncAt: string
  oaRoles: string[]
}

export interface OidcUserClaims {
  sub: string
  name: string
  email: string
  roles: string[]
  iat: number
  exp: number
}
