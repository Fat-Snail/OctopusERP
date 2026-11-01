<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useUserStore } from '@/store/user'

const router = useRouter()
const store = useUserStore()

const form = ref({ userName: '', password: '' })
const loading = ref(false)
const error = ref('')

async function handleLogin() {
  if (!form.value.userName || !form.value.password) {
    error.value = '请输入用户名和密码'
    return
  }
  loading.value = true
  error.value = ''
  try {
    await store.login(form.value)
    router.replace('/home')
  } catch (e: unknown) {
    error.value = (e as Error).message || '登录失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-dvh flex flex-col bg-subtle">
    <!-- Header -->
    <div class="bg-primary px-6 pt-16 pb-10">
      <div class="w-14 h-14 rounded-2xl bg-white/20 flex items-center justify-center mb-4 text-3xl">🐙</div>
      <h1 class="text-white text-[24px] font-bold">章鱼 ERP</h1>
      <p class="text-white/70 text-[14px] mt-1">统一用户中心</p>
    </div>

    <!-- Form -->
    <div class="flex-1 px-6 pt-8">
      <div class="space-y-4">
        <div>
          <label class="text-[13px] text-muted-foreground block mb-1">用户名</label>
          <input
            v-model="form.userName"
            type="text"
            autocomplete="username"
            placeholder="请输入用户名"
            class="w-full h-12 px-4 rounded-xl border border-border bg-background text-[15px] outline-none focus:border-[hsl(var(--primary))]"
            @keyup.enter="handleLogin"
          />
        </div>
        <div>
          <label class="text-[13px] text-muted-foreground block mb-1">密码</label>
          <input
            v-model="form.password"
            type="password"
            autocomplete="current-password"
            placeholder="请输入密码"
            class="w-full h-12 px-4 rounded-xl border border-border bg-background text-[15px] outline-none focus:border-[hsl(var(--primary))]"
            @keyup.enter="handleLogin"
          />
        </div>
        <p v-if="error" class="text-[13px] text-danger text-center">{{ error }}</p>
        <button
          class="w-full h-12 rounded-xl bg-[hsl(var(--primary))] text-white text-[16px] font-semibold disabled:opacity-60"
          :disabled="loading"
          @click="handleLogin"
        >
          {{ loading ? '登录中...' : '登 录' }}
        </button>
      </div>
    </div>
  </div>
</template>
