<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { acceptInvite, previewInvite, registerInvite } from '@/api/invite'
import type { InvitePreview } from '@/api/types'
import { getUser, login, logout } from '@/services/oidcService'
import { useWorkspaceStore } from '@/stores/workspace'
import { tenantNoun, roleLabel } from '@/lib/tenant'

const route = useRoute()
const router = useRouter()
const workspace = useWorkspaceStore()

/**
 * 凭据来源（三选一）：
 *   /i/{code}                      短链接
 *   /invite/accept?code={code}     短码查询参数
 *   /invite/accept?token={token}   长令牌（旧链接，保持兼容）
 */
const token = computed(() =>
  String(route.params.code ?? route.query.code ?? route.query.token ?? ''),
)

const preview = ref<InvitePreview | null>(null)
const loggedIn = ref(false)
const loading = ref(true)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)

/** 注册表单：无账号的被邀请人自行设定密码 */
const form = ref({ userName: '', displayName: '', phone: '', password: '' })


onMounted(async () => {
  try {
    if (!token.value) {
      error.value = '缺少邀请令牌或短码'
      return
    }
    preview.value = await previewInvite(token.value)
    const user = await getUser()
    loggedIn.value = !!user && !user.expired
    currentName.value =
      (user?.profile?.name as string | undefined) ??
      (user?.profile?.preferred_username as string | undefined) ??
      ''
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    loading.value = false
  }
})

async function onAccept() {
  busy.value = true
  error.value = null
  try {
    const res = await acceptInvite(token.value)
    notice.value = res.alreadyMember ? '你已是该门店成员' : '已加入门店'
    await workspace.load()
    await router.push('/workspaces')
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
  } finally {
    busy.value = false
  }
}

async function onRegister() {
  busy.value = true
  error.value = null
  try {
    await registerInvite({
      token: token.value,
      userName: form.value.userName,
      displayName: form.value.displayName || form.value.userName,
      phone: form.value.phone || undefined,
      password: form.value.password,
    })
    notice.value = '账号已创建，正在跳转登录…'
    // 建号成功即已入店，登录后应直接进工作台。
    // 不要回跳本页——那条邀请（尤其 maxUses=1）此时已用尽，
    // 回跳会看到"邀请已失效"，对刚加入的人非常困惑。
    await login('/workspaces')
  } catch (e) {
    error.value = e instanceof Error ? e.message : String(e)
    busy.value = false
  }
}

/** 当前登录者的展示名（用于"以 XX 身份加入"） */
const currentName = ref('')

async function onSwitchAccount() {
  workspace.clear()
  await logout()
}

function goLogin() {
  void login(route.fullPath)
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-6">
    <div class="w-full max-w-md rounded-xl border bg-white p-6 shadow-sm">
      <h1 class="text-lg font-semibold">{{ preview ? tenantNoun(preview.tenantType) : '' }}邀请</h1>

      <p v-if="loading" class="mt-4 text-sm text-gray-400">正在校验邀请…</p>

      <template v-else-if="preview">
        <div class="mt-4 rounded-lg bg-gray-50 p-4">
          <div class="text-sm text-gray-500">你被邀请加入</div>
          <div class="mt-1 text-xl font-semibold">{{ preview.tenantName }}</div>
          <div class="mt-1 text-xs text-gray-400">
            {{ preview.tenantId }} · 角色 {{ roleLabel(preview.roleInTenant) }}
          </div>
        </div>

        <!-- 已是成员：不要显示"邀请已失效"，那对已经加入的人毫无意义 -->
        <template v-if="preview.alreadyMember">
          <div class="mt-4 rounded-md border border-green-200 bg-green-50 p-3">
            <p class="text-sm text-green-800">
              你已经加入「{{ preview.tenantName }}」，当前角色
              <b>{{ roleLabel(preview.myRole) }}</b>。
            </p>
          </div>
          <button
            class="mt-3 w-full rounded-md bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700"
            @click="router.push('/workspaces')"
          >
            进入工作台
          </button>
          <button
            class="mt-2 w-full rounded-md border border-gray-300 px-4 py-2 text-sm hover:bg-gray-50"
            @click="onSwitchAccount"
          >
            换个账号登录
          </button>
        </template>

        <p v-else-if="!preview.usable" class="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-600">
          {{ preview.reason ?? '该邀请已失效' }}
        </p>

        <template v-else>
          <!-- 方式一：以当前登录身份加入 -->
          <div v-if="loggedIn" class="mt-4 rounded-md border border-blue-200 bg-blue-50 p-3">
            <p class="text-xs text-blue-800">
              你当前已登录为 <b>{{ currentName || '当前账号' }}</b>。
              如果你想以这个身份加入，点下面；想让**新员工**加入，请用下方的注册表单，或先退出登录。
            </p>
            <button
              :disabled="busy"
              class="mt-2 w-full rounded-md bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700 disabled:opacity-50"
              @click="onAccept"
            >
              {{ busy ? '处理中…' : `以 ${currentName || '当前账号'} 身份加入该${tenantNoun(preview.tenantType)}` }}
            </button>
            <button class="mt-2 w-full text-xs text-gray-500 hover:underline" @click="onSwitchAccount">
              换个账号（先退出当前登录）
            </button>
          </div>

          <div class="mt-4 flex items-center gap-3 text-xs text-gray-400">
            <span class="h-px flex-1 bg-gray-200" />
            {{ loggedIn ? '或：注册一个新账号并加入' : '没有账号？填写以下信息即可加入' }}
            <span class="h-px flex-1 bg-gray-200" />
          </div>

          <!-- 方式二：注册新账号（始终可见，登录状态下也可用） -->
          <div class="mt-3 space-y-3">
            <input v-model="form.userName" placeholder="用户名（登录用，至少 3 位）" class="w-full rounded-md border border-gray-300 px-3 py-2 text-sm" />
            <input v-model="form.displayName" placeholder="姓名" class="w-full rounded-md border border-gray-300 px-3 py-2 text-sm" />
            <input v-model="form.phone" placeholder="手机号（定向邀请需与邀请一致）" class="w-full rounded-md border border-gray-300 px-3 py-2 text-sm" />
            <input v-model="form.password" type="password" placeholder="设置密码（至少 8 位）" class="w-full rounded-md border border-gray-300 px-3 py-2 text-sm" />

            <button
              :disabled="busy"
              class="w-full rounded-md border border-blue-600 px-4 py-2 text-sm text-blue-700 hover:bg-blue-50 disabled:opacity-50"
              @click="onRegister"
            >
              {{ busy ? '创建中…' : '创建账号并加入' }}
            </button>
          </div>

          <button
            v-if="!loggedIn"
            class="mt-3 w-full rounded-md border border-gray-300 px-4 py-2 text-sm hover:bg-gray-50"
            @click="goLogin"
          >
            已有账号，去登录
          </button>
        </template>
      </template>

      <p v-if="notice" class="mt-4 rounded-md bg-green-50 px-3 py-2 text-sm text-green-700">{{ notice }}</p>
      <p v-if="error" class="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-600">{{ error }}</p>
    </div>
  </div>
</template>
