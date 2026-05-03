<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { User, Lock, Eye, EyeOff } from 'lucide-vue-next'
import { useUserStore } from '@/store/modules/user'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()
const loading = ref(false)
const showPassword = ref(false)
const rememberMe = ref(false)
const errorMsg = ref('')

const form = reactive({
  userName: 'admin',
  password: 'Admin@123',
})

async function handleLogin() {
  if (!form.userName || !form.password) {
    errorMsg.value = '请输入用户名和密码'
    return
  }
  errorMsg.value = ''
  loading.value = true
  try {
    await userStore.login({ userName: form.userName, password: form.password, rememberMe: rememberMe.value })
    const returnUrl = route.query['returnUrl'] as string | undefined
    if (returnUrl && returnUrl.startsWith('/connect/')) {
      window.location.href = `http://localhost:5001${returnUrl}`
      return
    }
    router.push('/dashboard/workbench')
  } catch {
    errorMsg.value = '用户名或密码错误'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="flex h-screen overflow-hidden">
    <!-- Left branding panel -->
    <div class="hidden lg:flex flex-1 items-center justify-center bg-gradient-to-br from-[#1a237e] via-[#283593] to-[#3949ab]">
      <div class="text-center px-10">
        <div class="w-20 h-20 rounded-2xl bg-white/20 flex items-center justify-center mx-auto mb-6">
          <span class="text-white font-bold text-3xl font-mono-tnum">OC</span>
        </div>
        <h1 class="text-white text-4xl font-bold tracking-wide mb-3">OctopusUMC</h1>
        <p class="text-white/80 text-lg mb-4">统一用户管理中心</p>
        <p class="text-white/60 text-sm max-w-xs leading-relaxed">
          集中管理企业用户、权限、组织架构，提供统一认证服务（SSO / OIDC）
        </p>
      </div>
    </div>

    <!-- Right login panel -->
    <div class="w-full lg:w-[480px] flex items-center justify-center bg-muted">
      <div class="w-full max-w-[380px] px-6">
        <div class="bg-card border border-border rounded-xl p-8 shadow-sm">
          <h2 class="text-[22px] font-semibold text-foreground text-center mb-6">欢迎登录</h2>

          <div class="space-y-4">
            <!-- Username -->
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">用户名</label>
              <div class="relative">
                <User :size="14" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  v-model="form.userName"
                  type="text"
                  placeholder="请输入用户名"
                  class="w-full h-9 pl-8 pr-3 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent"
                  @keyup.enter="handleLogin"
                />
              </div>
            </div>

            <!-- Password -->
            <div>
              <label class="block text-[12px] font-medium text-foreground mb-1.5">密码</label>
              <div class="relative">
                <Lock :size="14" class="absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
                <input
                  v-model="form.password"
                  :type="showPassword ? 'text' : 'password'"
                  placeholder="请输入密码"
                  class="w-full h-9 pl-8 pr-9 rounded-[var(--radius)] border border-input bg-background text-[13px] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent"
                  @keyup.enter="handleLogin"
                />
                <button
                  type="button"
                  class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground border-0 bg-transparent p-0 cursor-pointer"
                  @click="showPassword = !showPassword"
                >
                  <Eye v-if="!showPassword" :size="14" />
                  <EyeOff v-else :size="14" />
                </button>
              </div>
            </div>

            <!-- Remember me + hint -->
            <div class="flex items-center justify-between">
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input v-model="rememberMe" type="checkbox" class="w-3.5 h-3.5 rounded border-border" />
                <span class="text-[12px] text-muted-foreground">记住我</span>
              </label>
              <span class="text-[11px] text-muted-foreground">任意密码均可登录（Mock）</span>
            </div>

            <!-- Error message -->
            <p v-if="errorMsg" class="text-[12px] text-danger text-center">{{ errorMsg }}</p>

            <!-- Submit -->
            <button
              :disabled="loading"
              class="w-full h-9 bg-primary text-primary-foreground text-[13px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
              @click="handleLogin"
            >
              <span v-if="loading">登录中...</span>
              <span v-else>登 录</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
