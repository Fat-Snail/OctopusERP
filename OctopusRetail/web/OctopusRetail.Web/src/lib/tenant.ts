/**
 * 租户称谓。
 *
 * 业务上「邀请」不只用于门店：子公司的销售也会邀请客户、企业会邀请外部协作方。
 * 所以称谓必须由数据（tenantType）决定，不能在文案里写死"门店"。
 */
const TENANT_NOUN: Record<string, string> = {
  store: '门店',
  chain: '连锁总部',
  franchise: '加盟店',
  company: '企业',
  subsidiary: '子公司',
}

/** store → 门店；company → 企业；未知/未提供 → 组织（中性兜底） */
export function tenantNoun(type?: string | null): string {
  return TENANT_NOUN[type ?? ''] ?? '组织'
}

/** 类型本身的展示名（用于下拉/徽标） */
export function tenantTypeLabel(type?: string | null): string {
  return TENANT_NOUN[type ?? ''] ?? '其他'
}

export const TENANT_TYPES = [
  { value: 'store', label: '门店' },
  { value: 'company', label: '企业' },
  { value: 'subsidiary', label: '子公司' },
  { value: 'franchise', label: '加盟店' },
  { value: 'chain', label: '连锁总部' },
] as const

/**
 * 组织内角色称谓。
 * 同样不能写死"店长/店主"：在子公司里 owner 就是"负责人"，manager 是"管理员"。
 */
const ROLE_LABEL: Record<string, string> = {
  owner: '所有者',
  manager: '管理员',
  staff: '成员',
  cashier: '收银员',
}

export function roleLabel(role?: string | null): string {
  return ROLE_LABEL[role ?? ''] ?? (role || '成员')
}
