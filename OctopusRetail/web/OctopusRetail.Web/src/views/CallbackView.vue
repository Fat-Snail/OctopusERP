<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { handleCallback } from '@/services/oidcService'
import { useWorkspaceStore } from '@/stores/workspace'

/**
 * OIDC 回调页。
 *
 * 之前这里只有一句「正在完成登录…」，任何一步 promise 不 settle 用户都看不出卡在哪
 * （排查时实测遇到过「令牌已换到、API 也调通了，但页面就是不动」）。
 * 现在拆成可观测的三步，每步带独立超时，卡住能立刻定位到具体阶段。
 */

type Step = 'exchange' | 'workspace' | 'redirect' | 'done' | 'failed'

const STEPS: { key: Step; text: string }[] = [
  { key: 'exchange', text: '换取登录令牌' },
  { key: 'workspace', text: '加载我的门店' },
  { key: 'redirect', text: '进入工作台' },
]

const router = useRouter()
const workspace = useWorkspaceStore()

const step = ref<Step>('exchange')
const error = ref<string | null>(null)
const detail = ref<string>('')

const currentIndex = computed(() => STEPS.findIndex((s) => s.key === step.value))

function dotClass(i: number): string {
  if (step.value === 'done') return 'bg-green-500'
  if (step.value === 'failed') return i < currentIndex.value ? 'bg-green-500' : 'bg-gray-300'
  if (i < currentIndex.value) return 'bg-green-500'
  if (i === currentIndex.value) return 'bg-blue-500 animate-pulse'
  return 'bg-gray-300'
}

function withTimeout<T>(p: Promise<T>, ms: number, label: string): Promise<T> {
  return Promise.race([
    p,
    new Promise<T>((_, reject) =>
      setTimeout(() => reject(new Error(`${label}超时（${ms / 1000} 秒未完成）`)), ms),
    ),
  ])
}

onMounted(async () => {
  try {
    step.value = 'exchange'
    const returnUrl = await withTimeout(handleCallback(), 20000, '换取令牌')
    detail.value = `令牌已获取 · 目标 ${returnUrl}`

    step.value = 'workspace'
    await withTimeout(workspace.load(), 20000, '加载门店')
    detail.value += ` · 门店 ${workspace.workspaces.length} 个`

    step.value = 'redirect'
    // 防御：returnUrl 绝不能再指回 /callback，否则会原地打转
    const target =
      returnUrl.startsWith('/') && !returnUrl.startsWith('/callback') ? returnUrl : '/workspaces'
    detail.value += ` · 前往 ${target}`

    await withTimeout(router.replace(target), 10000, '页面跳转')
    step.value = 'done'
  } catch (e) {
    step.value = 'failed'
    error.value = e instanceof Error ? e.message : String(e)
    console.error('[callback] 登录回调失败', e)
  }
})
</script>

<template>
  <div class="flex min-h-[60vh] items-center justify-center p-6">
    <div class="w-full max-w-md rounded-lg border bg-white p-5">
      <p v-if="step !== 'failed'" class="text-center text-sm text-gray-600">
        {{ step === 'done' ? '登录完成' : `正在${STEPS[currentIndex]?.text ?? '处理'}…` }}
      </p>
      <p v-else class="text-center text-sm font-medium text-red-600">登录失败</p>

      <p v-if="step === 'failed' && error" class="mt-2 break-words text-sm text-gray-700">
        {{ error }}
      </p>

      <ol class="mt-4 space-y-1.5 text-xs">
        <li v-for="(s, i) in STEPS" :key="s.key" class="flex items-center gap-2">
          <span class="inline-block h-1.5 w-1.5 shrink-0 rounded-full" :class="dotClass(i)" />
          <span :class="i === currentIndex ? 'text-gray-800' : 'text-gray-400'">{{ s.text }}</span>
        </li>
      </ol>

      <p v-if="detail" class="mt-3 break-words rounded bg-gray-50 px-2 py-1.5 text-[11px] text-gray-500">
        {{ detail }}
      </p>

      <div v-if="step === 'failed'" class="mt-4 flex justify-center gap-2">
        <button
          class="rounded-md bg-blue-600 px-4 py-1.5 text-sm text-white hover:bg-blue-700"
          @click="router.replace('/workspaces')"
        >
          进入工作台
        </button>
        <button
          class="rounded-md border border-gray-300 px-4 py-1.5 text-sm hover:bg-gray-50"
          @click="router.replace('/')"
        >
          重新登录
        </button>
      </div>
    </div>
  </div>
</template>
