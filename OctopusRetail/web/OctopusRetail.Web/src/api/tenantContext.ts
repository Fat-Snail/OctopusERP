import { ref } from 'vue'

/**
 * 当前 workspace（active tenant）的客户端持有者。
 *
 * ⚠️ 重要：这里保存的只是「请求意图」，不是授权依据。
 * 服务端会用 token 中不可伪造的 groups claim 校验 X-Tenant-Id，
 * 客户端改这个值最多导致 403，不可能越过租户边界。
 *
 * 单独放在一个模块（而不是 Pinia store）是为了避免 http ↔ store 的循环依赖。
 */
const STORAGE_KEY = 'retail.activeTenantId'

export const activeTenantId = ref<string | null>(localStorage.getItem(STORAGE_KEY))

export function getActiveTenant(): string | null {
  return activeTenantId.value
}

export function setActiveTenant(tenantId: string | null): void {
  activeTenantId.value = tenantId
  if (tenantId) localStorage.setItem(STORAGE_KEY, tenantId)
  else localStorage.removeItem(STORAGE_KEY)
}
