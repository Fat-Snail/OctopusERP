export interface ApiResponse<T = unknown> {
  code: number
  msg: string
  data: T
}

export interface Workspace {
  tenantId: string
  tenantName: string
  tenantType: string
  role: string
  joinedAt: string
}

export interface WorkspaceList {
  items: Workspace[]
  total: number
}

export interface IdentityContext {
  sub: string
  userName: string
  displayName: string
  availableTenants: string[]
  activeTenant: string | null
}

export interface Member {
  kcSub: string
  roleInTenant: string
  joinedAt: string
}

export interface Invite {
  inviteId: number
  tenantId: string
  phone: string | null
  inviteCode: string | null
  roleInTenant: string
  maxUses: number
  usedCount: number
  state: 'pending' | 'accepted' | 'expired' | 'revoked'
  expiresAt: string
  createdAt: string
  acceptedBySub: string | null
}

export interface CreateInviteResponse {
  inviteId: number
  tenantId: string
  roleInTenant: string
  maxUses: number
  expiresAt: string
  /** 明文令牌，仅创建时返回一次 */
  token: string
  /** 长令牌落地页链接（兼容用） */
  inviteUrl: string
  /** 短码明文，仅创建时返回一次，形如 B1V2d9xK */
  shortCode: string
  /** 短链接，形如 http://localhost:5186/i/B1V2d9xK —— 推荐发给别人 */
  shortUrl: string
}

export interface InvitePreview {
  tenantId: string
  tenantName: string
  /** store / chain / franchise / company / subsidiary */
  tenantType: string
  roleInTenant: string
  expiresAt: string
  usable: boolean
  reason: string | null
  /** 当前登录者是否已是该组织成员（已加入时不该显示"邀请已失效"） */
  alreadyMember: boolean
  /** 已是成员时我在该组织内的角色 */
  myRole: string | null
}

export type TenantRole = 'owner' | 'manager' | 'staff' | 'cashier'
