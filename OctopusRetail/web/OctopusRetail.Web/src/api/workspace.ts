import { api } from './http'
import type { IdentityContext, Member, WorkspaceList } from './types'

/** 跨租户：我是谁 / 可进入哪些租户 / 当前租户 */
export const getMe = () => api.get<IdentityContext>('/me')

/** 跨租户：我的全部 workspace */
export const getWorkspaces = () => api.get<WorkspaceList>('/workspaces')

/** 租户内：本租户成员 */
export const getMembers = () => api.get<{ tenantId: string; items: Member[]; total: number }>('/members')

/** 租户内：本租户成员档案 */
export const getProfiles = () =>
  api.get<{
    items: { profileId: number; tenantId: string; kcSub: string; displayName: string; phone: string | null }[]
    total: number
  }>('/profiles')
