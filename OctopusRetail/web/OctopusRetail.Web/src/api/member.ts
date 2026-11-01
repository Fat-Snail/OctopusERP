import { api } from './http'

export interface OrgMember {
  membershipId: number
  kcSub: string
  userName: string | null
  displayName: string | null
  phone: string | null
  roleInTenant: string
  /** active | suspended | left */
  status: string
  joinedAt: string
  leftAt: string | null
  kcSyncState: string | null
}

export const listMembers = () =>
  api.get<{ tenantId: string; items: OrgMember[]; total: number }>('/members')

export const setMemberStatus = (membershipId: number, enabled: boolean) =>
  api.post<null>(`/members/${membershipId}/status`, { enabled })

export const removeMember = (membershipId: number) =>
  api.post<null>(`/members/${membershipId}/remove`)

export const resetMemberPassword = (membershipId: number, newPassword: string) =>
  api.post<null>(`/members/${membershipId}/reset-password`, { newPassword })
