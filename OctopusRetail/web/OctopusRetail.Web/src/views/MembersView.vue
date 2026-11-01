<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  listMembers,
  removeMember,
  resetMemberPassword,
  setMemberStatus,
  type OrgMember,
} from '@/api/member'
import { tenantNoun } from '@/lib/tenant'
import { roleLabel } from '@/lib/tenant'
import { useWorkspaceStore } from '@/stores/workspace'
import OrgSwitcher from '@/components/OrgSwitcher.vue'

/**
 * 组织成员管理。
 *
 * 零售域的用户主数据在 Keycloak（不走 UMC），所以禁用/改密/移出
 * 必须有这个入口，否则只能去 Keycloak Admin Console 手工操作。
 */

const workspace = useWorkspaceStore()

const members = ref<OrgMember[]>([])
const loading = ref(false)
const busyId = ref<number | null>(null)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)

/** 只有 owner / manager 能管理成员（后端还有更细的等级校验） */
const canManage = computed(() => workspace.canInvite)

const noun = computed(() => tenantNoun(workspace.activeWorkspace?.tenantType))

const statusText: Record<string, string> = {
  active: '正常',
  suspended: '已停用',
  left: '已移出',
}
/** Keycloak 组同步状态的中文展示（内部值 unmanaged 不该暴露给用户） */
const syncText: Record<string, string> = {
  pending: '同步中',
  failed: '同步失败',
  unmanaged: '演示数据',
}

const statusClass: Record<string, string> = {
  active: 'bg-green-50 text-green-700',
  suspended: 'bg-amber-50 text-amber-700',
  left: 'bg-gray-100 text-gray-500',
}

// 重置密码：行内展开输入
const pwdFor = ref<number | null>(null)
const pwdValue = ref('')

async function load() {
  loading.value = true
  error.value = null
  try {
    members.value = (await listMembers()).items
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function toggle(m: OrgMember) {
  busyId.value = m.membershipId
  error.value = null
  notice.value = null
  try {
    const enable = m.status !== 'active'
    await setMemberStatus(m.membershipId, enable)
    notice.value = enable ? `已启用 ${name(m)}` : `已停用 ${name(m)}`
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    busyId.value = null
  }
}

async function remove(m: OrgMember) {
  if (!confirm(`确定把 ${name(m)} 移出该${noun.value}？账号会保留，但不再属于这里。`)) return
  busyId.value = m.membershipId
  error.value = null
  try {
    await removeMember(m.membershipId)
    notice.value = `已移出 ${name(m)}`
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    busyId.value = null
  }
}

function openPwd(m: OrgMember) {
  pwdFor.value = m.membershipId
  pwdValue.value = ''
  error.value = null
}

async function submitPwd(m: OrgMember) {
  if (pwdValue.value.length < 8) {
    error.value = '密码至少 8 位'
    return
  }
  busyId.value = m.membershipId
  try {
    await resetMemberPassword(m.membershipId, pwdValue.value)
    notice.value = `已重置 ${name(m)} 的密码`
    pwdFor.value = null
    pwdValue.value = ''
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    busyId.value = null
  }
}

function name(m: OrgMember) {
  return m.displayName || m.userName || m.kcSub.slice(0, 8)
}

function fmt(v: string) {
  return new Date(v).toLocaleString('zh-CN', { hour12: false })
}

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-4 p-6">
    <div>
      <h1 class="text-lg font-semibold">{{ noun }}成员</h1>
      <p class="mt-1 text-sm text-gray-500">
        当前{{ noun }}：{{ workspace.activeWorkspace?.tenantName ?? '未选择' }}（{{ workspace.activeTenantId }}）
      </p>
      <OrgSwitcher class="mt-2" @switched="load" />
    </div>

    <p v-if="!canManage" class="rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-700">
      你的角色无权管理成员，仅管理员或所有者可以操作。
    </p>

    <p v-if="notice" class="rounded-md bg-green-50 px-3 py-2 text-sm text-green-700">{{ notice }}</p>
    <p v-if="error" class="rounded-md bg-red-50 px-3 py-2 text-sm text-red-600">{{ error }}</p>

    <div class="rounded-lg border bg-white">
      <div class="flex items-center justify-between border-b px-5 py-3">
        <h2 class="text-sm font-medium">共 {{ members.length }} 人</h2>
        <button class="text-xs text-gray-500 hover:text-gray-800" @click="load">刷新</button>
      </div>

      <p v-if="loading" class="p-5 text-sm text-gray-400">加载中…</p>
      <p v-else-if="members.length === 0" class="p-5 text-sm text-gray-400">暂无成员</p>

      <table v-else class="w-full text-sm">
        <thead class="text-left text-xs text-gray-500">
          <tr class="border-b">
            <th class="px-5 py-2 font-normal">成员</th>
            <th class="px-5 py-2 font-normal">手机号</th>
            <th class="px-5 py-2 font-normal">角色</th>
            <th class="px-5 py-2 font-normal">状态</th>
            <th class="px-5 py-2 font-normal">加入时间</th>
            <th class="px-5 py-2 font-normal">操作</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="m in members" :key="m.membershipId">
            <tr class="border-b last:border-0 align-top">
              <td class="px-5 py-2">
                <div class="font-medium">{{ name(m) }}</div>
                <div class="text-xs text-gray-400">
                  {{ m.userName ?? `sub ${m.kcSub.slice(0, 12)}…` }}
                  <span v-if="m.phone" class="ml-1">· {{ m.phone }}</span>
                </div>
              </td>
              <td class="px-5 py-2 font-mono text-xs">{{ m.phone || '—' }}</td>
              <td class="px-5 py-2">{{ roleLabel(m.roleInTenant) }}</td>
              <td class="px-5 py-2">
                <span class="rounded px-2 py-0.5 text-xs" :class="statusClass[m.status]">
                  {{ statusText[m.status] ?? m.status }}
                </span>
                <span
                  v-if="m.kcSyncState && syncText[m.kcSyncState]"
                  class="ml-1 rounded bg-gray-100 px-1.5 py-0.5 text-[10px] text-gray-500"
                  title="Keycloak 组同步状态"
                >
                  {{ syncText[m.kcSyncState] }}
                </span>
              </td>
              <td class="px-5 py-2 text-xs text-gray-500">{{ fmt(m.joinedAt) }}</td>
              <td class="px-5 py-2">
                <div v-if="canManage && m.status !== 'left'" class="flex flex-wrap gap-2 text-xs">
                  <button
                    :disabled="busyId === m.membershipId"
                    class="text-blue-600 hover:underline disabled:opacity-50"
                    @click="toggle(m)"
                  >
                    {{ m.status === 'active' ? '停用' : '启用' }}
                  </button>
                  <button
                    :disabled="busyId === m.membershipId"
                    class="text-gray-600 hover:underline disabled:opacity-50"
                    @click="openPwd(m)"
                  >
                    重置密码
                  </button>
                  <button
                    :disabled="busyId === m.membershipId"
                    class="text-red-600 hover:underline disabled:opacity-50"
                    @click="remove(m)"
                  >
                    移出
                  </button>
                </div>
              </td>
            </tr>

            <!-- 行内展开重置密码 -->
            <tr v-if="pwdFor === m.membershipId" class="border-b bg-gray-50">
              <td colspan="5" class="px-5 py-3">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="text-xs text-gray-500">为 {{ name(m) }} 设置新密码</span>
                  <input
                    v-model="pwdValue"
                    type="password"
                    placeholder="至少 8 位"
                    class="w-48 rounded border border-gray-300 px-2 py-1 text-sm"
                    @keyup.enter="submitPwd(m)"
                  />
                  <button
                    :disabled="busyId === m.membershipId"
                    class="rounded bg-blue-600 px-3 py-1 text-xs text-white disabled:opacity-50"
                    @click="submitPwd(m)"
                  >
                    确认重置
                  </button>
                  <button class="text-xs text-gray-500 hover:underline" @click="pwdFor = null">取消</button>
                </div>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>

    <p class="text-[11px] leading-relaxed text-gray-400">
      「停用」会同时禁用 Keycloak 账号（无法再登录）并移出该{{ noun }}组；
      「移出」只解除成员关系，账号保留。「重置密码」直接改 Keycloak 密码。
    </p>
  </div>
</template>
