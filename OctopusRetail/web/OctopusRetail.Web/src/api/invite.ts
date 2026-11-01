import { api } from './http'
import type { CreateInviteResponse, Invite, InvitePreview } from './types'

export interface CreateInvitePayload {
  phone?: string
  inviteCode?: string
  roleInTenant: string
  maxUses: number
  validHours: number
}

/** 租户内：创建邀请（需店长/店主）。返回的 token 仅此一次可见 */
export const createInvite = (payload: CreateInvitePayload) =>
  api.post<CreateInviteResponse>('/invites', payload)

/** 租户内：邀请列表 */
export const listInvites = () =>
  api.get<{ tenantId: string; items: Invite[]; total: number }>('/invites')

/** 租户内：撤销邀请 */
export const revokeInvite = (inviteId: number) =>
  api.post<null>(`/invites/${inviteId}/revoke`)

/** 跨租户：预览邀请（接受前展示将加入的门店） */
export const previewInvite = (token: string) =>
  api.get<InvitePreview>('/invites/preview', { token })

/** 跨租户：接受邀请 */
export const acceptInvite = (token: string) =>
  api.post<{ tenantId: string; roleInTenant: string; alreadyMember: boolean }>(
    '/invites/accept',
    { token },
  )

export interface RegisterInvitePayload {
  token: string
  userName: string
  displayName: string
  phone?: string
  password: string
  email?: string
}

/**
 * 匿名：受控自助建号。
 *
 * 授权凭据就是邀请令牌本身；建号由服务端用 retail-invite-bot 完成，
 * 不需要（也不应该）打开 Keycloak 的公开注册。
 */
export const registerInvite = (payload: RegisterInvitePayload) =>
  api.post<{ userName: string; tenantId: string }>('/invites/register', payload)
