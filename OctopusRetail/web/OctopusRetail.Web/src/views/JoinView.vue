<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

/**
 * 输入邀请码页（匿名可达）。
 *
 * 为什么需要它：邀请链接是 http://localhost:5186/i/{code}，但用户从聊天工具
 * 粘贴、或浏览器（如 Safari 的 HTTPS-First）会自动把协议改成 https，而 Vite
 * dev server 不支持 TLS，于是直接打不开。
 * 这里让人只需打开一个短地址、手动输入 8 位短码，彻底绕开协议/富文本链接问题。
 */

const router = useRouter()
const code = ref('')
const error = ref<string | null>(null)

/** 归一化：去空格、去掉误粘贴的整条 URL */
const normalized = computed(() => {
  const raw = code.value.trim()
  const fromUrl = raw.match(/\/i\/([A-Za-z0-9]+)/)
  return (fromUrl ? fromUrl[1] : raw).replace(/\s+/g, '')
})

const valid = computed(() => /^[A-Za-z0-9]{4,64}$/.test(normalized.value))

function submit() {
  error.value = null
  if (!normalized.value) {
    error.value = '请输入邀请码'
    return
  }
  if (!valid.value) {
    error.value = '邀请码格式不正确（通常是 8 位字母数字）'
    return
  }
  void router.push(`/i/${normalized.value}`)
}
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 p-6">
    <div class="w-full max-w-sm rounded-xl border bg-white p-6 shadow-sm">
      <h1 class="text-lg font-semibold">输入邀请码</h1>
      <p class="mt-1 text-sm text-gray-500">
        输入管理员发给你的邀请码（8 位，如 <code class="font-mono">B1V2d9xK</code>）。
      </p>

      <input
        v-model="code"
        class="mt-4 w-full rounded-md border border-gray-300 px-3 py-2 text-center font-mono text-lg tracking-widest"
        placeholder="B1V2d9xK"
        autocomplete="off"
        autocapitalize="off"
        spellcheck="false"
        @keyup.enter="submit"
      />

      <button
        :disabled="!normalized"
        class="mt-3 w-full rounded-md bg-blue-600 px-4 py-2 text-sm text-white hover:bg-blue-700 disabled:opacity-50"
        @click="submit"
      >
        继续
      </button>

      <p v-if="error" class="mt-3 rounded-md bg-red-50 px-3 py-2 text-xs text-red-600">{{ error }}</p>

      <p class="mt-4 text-[11px] leading-relaxed text-gray-400">
        也可以直接打开完整邀请链接。若浏览器把地址自动改成 https 打不开，
        在地址栏手动把 https 改成 http 即可。
      </p>
    </div>
  </div>
</template>
