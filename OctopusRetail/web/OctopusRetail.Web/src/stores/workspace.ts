import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { getMe, getWorkspaces } from '@/api/workspace'
import type { IdentityContext, Workspace } from '@/api/types'
import { activeTenantId, setActiveTenant } from '@/api/tenantContext'

/** 可在本租户内邀请成员的角色 */
const INVITE_ROLES = ['owner', 'manager']

export const useWorkspaceStore = defineStore('workspace', () => {
  const workspaces = ref<Workspace[]>([])
  const identity = ref<IdentityContext | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const activeWorkspace = computed(
    () => workspaces.value.find((w) => w.tenantId === activeTenantId.value) ?? null,
  )

  const hasWorkspaces = computed(() => workspaces.value.length > 0)

  /** 当前身份是否可邀请成员（前端仅用于隐藏入口，真正的判定在服务端） */
  const canInvite = computed(() =>
    activeWorkspace.value ? INVITE_ROLES.includes(activeWorkspace.value.role) : false,
  )

  async function load(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      identity.value = await getMe()
      const res = await getWorkspaces()
      workspaces.value = res.items

      // 预选规则：本地记录若已不在可进入清单中（被调店/离职），回退到第一个
      const stillValid = workspaces.value.some((w) => w.tenantId === activeTenantId.value)
      if (!stillValid) {
        setActiveTenant(workspaces.value[0]?.tenantId ?? null)
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : String(e)
    } finally {
      loading.value = false
    }
  }

  /**
   * 切换 workspace。
   *
   * 只改本地值 + 后续请求头。服务端每次请求都会拿 token 的 groups 校验
   * X-Tenant-Id，因此这里不存在「越权切换」的可能——最多拿到 403。
   */
  function switchTo(tenantId: string): void {
    if (!workspaces.value.some((w) => w.tenantId === tenantId)) return
    setActiveTenant(tenantId)
  }

  function clear(): void {
    workspaces.value = []
    identity.value = null
    setActiveTenant(null)
  }

  return {
    workspaces,
    identity,
    loading,
    error,
    activeTenantId,
    activeWorkspace,
    hasWorkspaces,
    canInvite,
    load,
    switchTo,
    clear,
  }
})
