<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { createInvite, listInvites, revokeInvite, type CreateInvitePayload } from '@/api/invite'
import type { CreateInviteResponse, Invite } from '@/api/types'
import { useWorkspaceStore } from '@/stores/workspace'
import OrgSwitcher from '@/components/OrgSwitcher.vue'

const workspace = useWorkspaceStore()

const invites = ref<Invite[]>([])
const loading = ref(false)
const message = ref<string | null>(null)
const error = ref<string | null>(null)

const form = ref<CreateInvitePayload>({
  phone: '',
  inviteCode: '',
  roleInTenant: 'staff',
  maxUses: 1,
  validHours: 72,
})

/** 明文令牌只在创建响应中出现一次 */
const created = ref<CreateInviteResponse | null>(null)
const copied = ref(false)

const stateText: Record<string, string> = {
  pending: '待接受',
  accepted: '已接受',
  expired: '已过期',
  revoked: '已撤销',
}
const stateClass: Record<string, string> = {
  pending: 'bg-amber-50 text-amber-700',
  accepted: 'bg-green-50 text-green-700',
  expired: 'bg-gray-100 text-gray-500',
  revoked: 'bg-red-50 text-red-600',
}

async function load() {
  loading.value = true
  error.value = null
  try {
    invites.value = (await listInvites()).items
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
}

async function submit() {
  message.value = null
  error.value = null
  created.value = null
  copied.value = false

  if (!form.value.phone && !form.value.inviteCode) {
    error.value = '手机号与通用邀请码至少填写一项'
    return
  }

  try {
    created.value = await createInvite({
      phone: form.value.phone || undefined,
      inviteCode: form.value.inviteCode || undefined,
      roleInTenant: form.value.roleInTenant,
      maxUses: Number(form.value.maxUses) || 1,
      validHours: Number(form.value.validHours) || 72,
    })
    message.value = '邀请已创建'
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
}

async function revoke(id: number) {
  error.value = null
  try {
    await revokeInvite(id)
    await load()
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  }
}

/**
 * 用「当前浏览器地址」重建链接。
 *
 * 后端按 Retail:PublicBaseUrl 生成，但那可能与用户实际访问方式不一致
 * （实测：Safari 自动把 http://localhost:5186 升级成 https，而 dev server 不支持 TLS，
 *  直接报"无法与服务器建立安全连接"）。用 window.location.origin 重建可保证协议/主机一致。
 */
function absoluteLink(path: string): string {
  return `${window.location.origin}${path}`
}

const shortLink = computed(() =>
  created.value?.shortCode ? absoluteLink(`/i/${created.value.shortCode}`) : (created.value?.inviteUrl ?? ''),
)
const joinEntryUrl = computed(() => absoluteLink('/join'))
const longLink = computed(() =>
  created.value?.token ? absoluteLink(`/invite/accept?token=${encodeURIComponent(created.value.token)}`) : '',
)

async function copyLink() {
  if (!created.value) return
  try {
    await navigator.clipboard.writeText(shortLink.value)
    copied.value = true
  } catch {
    copied.value = false
  }
}

function fmt(value: string) {
  return new Date(value).toLocaleString('zh-CN', { hour12: false })
}

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-6 p-6">
    <div>
      <h1 class="text-lg font-semibold">邀请管理</h1>
      <p class="mt-1 text-sm text-gray-500">
        当前组织：{{ workspace.activeWorkspace?.tenantName ?? '未选择' }}
        （{{ workspace.activeTenantId }}）
      </p>
      <OrgSwitcher class="mt-2" @switched="load" />
    </div>

    <p v-if="!workspace.canInvite" class="rounded-lg bg-amber-50 px-4 py-3 text-sm text-amber-700">
      你的角色无权邀请成员，仅管理员（owner / manager）可以发起邀请。
    </p>

    <!-- 创建邀请 -->
    <section v-if="workspace.canInvite" class="rounded-lg border bg-white p-5">
      <h2 class="text-sm font-medium">发起邀请</h2>

      <div class="mt-4 grid grid-cols-1 gap-4 md:grid-cols-3">
        <label class="flex flex-col gap-1">
          <span class="text-xs text-gray-500">手机号（定向邀请）</span>
          <input v-model="form.phone" class="rounded-md border border-gray-300 px-3 py-1.5 text-sm" placeholder="13900001111" />
        </label>

        <label class="flex flex-col gap-1">
          <span class="text-xs text-gray-500">或通用邀请码</span>
          <input v-model="form.inviteCode" class="rounded-md border border-gray-300 px-3 py-1.5 text-sm" placeholder="SH001-PARTTIME" />
        </label>

        <label class="flex flex-col gap-1">
          <span class="text-xs text-gray-500">入店角色</span>
          <select v-model="form.roleInTenant" class="rounded-md border border-gray-300 px-3 py-1.5 text-sm">
            <option value="staff">店员</option>
            <option value="cashier">收银</option>
            <option value="manager">店长</option>
          </select>
        </label>

        <label class="flex flex-col gap-1">
          <span class="text-xs text-gray-500">可接受次数</span>
          <input v-model.number="form.maxUses" type="number" min="1" class="rounded-md border border-gray-300 px-3 py-1.5 text-sm" />
        </label>

        <label class="flex flex-col gap-1">
          <span class="text-xs text-gray-500">有效期（小时）</span>
          <input v-model.number="form.validHours" type="number" min="1" class="rounded-md border border-gray-300 px-3 py-1.5 text-sm" />
        </label>

        <div class="flex items-end">
          <button class="rounded-md bg-blue-600 px-4 py-1.5 text-sm text-white hover:bg-blue-700" @click="submit">
            生成邀请链接
          </button>
        </div>
      </div>

      <p v-if="message" class="mt-3 text-sm text-green-600">{{ message }}</p>
      <p v-if="error" class="mt-3 text-sm text-red-600">{{ error }}</p>

      <!-- 明文令牌仅此一次展示 -->
      <div v-if="created" class="mt-4 rounded-md border border-blue-200 bg-blue-50 p-4">
        <p class="text-xs text-blue-800">
          邀请链接仅此一次完整展示（服务端只保存哈希，无法再次查看明文）。请立即复制并发送给被邀请人。
        </p>
        <div class="mt-2 flex items-center gap-2">
          <input :value="shortLink" readonly class="flex-1 rounded border border-blue-200 bg-white px-3 py-1.5 text-xs" />
          <button class="rounded-md border border-blue-300 bg-white px-3 py-1.5 text-xs text-blue-700" @click="copyLink">
            {{ copied ? '已复制' : '复制' }}
          </button>
        </div>

        <div class="mt-2 flex flex-wrap items-center gap-2 text-xs text-blue-800">
          <span>短码</span>
          <code class="rounded bg-white px-2 py-0.5 font-mono text-sm tracking-wider">{{ created.shortCode }}</code>
        </div>
        <p class="mt-1 text-[11px] leading-relaxed text-blue-700">
          对方也可以直接打开
          <code class="rounded bg-white px-1">{{ joinEntryUrl }}</code>
          输入上面的短码——这条路径不受聊天工具/浏览器改写协议影响。
        </p>

        <details class="mt-2">
          <summary class="cursor-pointer text-[11px] text-blue-700">兼容用：长令牌链接</summary>
          <input :value="longLink" readonly class="mt-1 w-full rounded border border-blue-200 bg-white px-3 py-1.5 text-[11px]" />
        </details>
      </div>
    </section>

    <!-- 邀请列表 -->
    <section class="rounded-lg border bg-white">
      <div class="flex items-center justify-between border-b px-5 py-3">
        <h2 class="text-sm font-medium">邀请记录</h2>
        <button class="text-xs text-gray-500 hover:text-gray-800" @click="load">刷新</button>
      </div>

      <p v-if="loading" class="p-5 text-sm text-gray-400">加载中…</p>
      <p v-else-if="invites.length === 0" class="p-5 text-sm text-gray-400">暂无邀请记录</p>

      <table v-else class="w-full text-sm">
        <thead class="text-left text-xs text-gray-500">
          <tr class="border-b">
            <th class="px-5 py-2 font-normal">对象</th>
            <th class="px-5 py-2 font-normal">角色</th>
            <th class="px-5 py-2 font-normal">使用</th>
            <th class="px-5 py-2 font-normal">状态</th>
            <th class="px-5 py-2 font-normal">过期时间</th>
            <th class="px-5 py-2 font-normal">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="i in invites" :key="i.inviteId" class="border-b last:border-0">
            <td class="px-5 py-2">{{ i.phone ?? i.inviteCode ?? '—' }}</td>
            <td class="px-5 py-2">{{ i.roleInTenant }}</td>
            <td class="px-5 py-2">{{ i.usedCount }} / {{ i.maxUses }}</td>
            <td class="px-5 py-2">
              <span class="rounded px-2 py-0.5 text-xs" :class="stateClass[i.state]">
                {{ stateText[i.state] ?? i.state }}
              </span>
            </td>
            <td class="px-5 py-2 text-xs text-gray-500">{{ fmt(i.expiresAt) }}</td>
            <td class="px-5 py-2">
              <button
                v-if="i.state === 'pending' && workspace.canInvite"
                class="text-xs text-red-600 hover:underline"
                @click="revoke(i.inviteId)"
              >
                撤销
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </section>
  </div>
</template>
