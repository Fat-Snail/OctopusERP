<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { AUTHORITY, login } from '@/services/oidcService'

const route = useRoute()
const router = useRouter()

const busy = ref(false)
const message = computed(() => String(route.query.msg ?? '发生未知错误'))
const target = computed(() => String(route.query.target ?? '/workspaces'))

async function retry() {
  busy.value = true
  try {
    await login(target.value)
  } catch (e) {
    message.value
    // 仍然失败：留在本页，并把新的错误挂到提示上
    const text = e instanceof Error ? e.message : String(e)
    await router.replace({ name: 'error', query: { msg: text, target: target.value } })
    busy.value = false
  }
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-6">
    <div class="w-full max-w-lg rounded-xl border bg-white p-6 shadow-sm">
      <h1 class="text-lg font-semibold text-red-600">无法连接身份服务</h1>

      <p class="mt-3 text-sm text-gray-700">{{ message }}</p>

      <div class="mt-4 rounded-md bg-gray-50 p-3 text-xs text-gray-500">
        <div>身份服务地址</div>
        <code class="mt-1 block break-all text-gray-700">{{ AUTHORITY }}</code>
      </div>

      <p class="mt-4 text-sm text-gray-500">
        常见原因：Keycloak 未启动、端口不对、或 realm 尚未导入。
        可先在终端确认：
      </p>
      <pre class="mt-2 overflow-x-auto rounded-md bg-gray-900 p-3 text-xs text-gray-100">curl -s {{ AUTHORITY }}/.well-known/openid-configuration | head -c 200</pre>

      <p class="mt-3 text-xs text-gray-400">
        该命令返回 JSON 说明身份服务正常，此时点「重试」即可；返回连接错误说明 Keycloak 没起来。
      </p>

      <div class="mt-5 flex gap-3">
        <button
          :disabled="busy"
          class="rounded-md bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700 disabled:opacity-50"
          @click="retry"
        >
          {{ busy ? '重试中…' : '重试' }}
        </button>
        <button
          class="rounded-md border border-gray-300 px-4 py-2 text-sm hover:bg-gray-50"
          @click="router.replace('/workspaces')"
        >
          返回首页
        </button>
      </div>
    </div>
  </div>
</template>
