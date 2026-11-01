<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { User, Lock, Eye, EyeOff } from '@lucide/vue'
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

/**
 * 把登录失败的真实原因说出来。
 *
 * 以前这里一律显示「账号或密码错误」，把网络不通、限流 429、服务 500
 * 全都伪装成密码问题，排查时会一直怀疑密码（实测踩过）。
 */
function describeLoginError(e: unknown): string {
  const err = e as {
    message?: string
    response?: { status?: number; data?: { msg?: string } }
  }
  const status = err.response?.status
  const serverMsg = err.response?.data?.msg

  // 完全没有 response = 请求没到达服务端（后端没起 / 代理挂了 / 跨域失败）
  if (status === undefined) {
    return '无法连接到服务，请确认后端已启动（http://localhost:5001）'
  }
  if (status === 429) return '尝试过于频繁，请等一分钟后重试'
  if (status === 403 || serverMsg?.includes('禁用')) return '账号已被禁用，请联系管理员'
  if (status === 401) return '用户名或密码错误'
  return serverMsg ?? `登录失败（HTTP ${status}）`
}

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
  } catch (e: unknown) {
    errorMsg.value = describeLoginError(e)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <!-- ── Mobile H5 layout (< lg) ── -->
  <div class="flex flex-col min-h-screen bg-background lg:hidden">
    <div class="flex flex-col items-center justify-center flex-1 px-6 py-8">
      <!-- Logo -->
      <div class="w-16 h-16 rounded-2xl bg-primary flex items-center justify-center mb-4">
        <span class="text-white font-bold text-2xl font-mono-tnum">OC</span>
      </div>
      <h1 class="text-[20px] font-semibold text-foreground mb-1">OctopusERP</h1>
      <p class="text-[13px] text-muted-foreground mb-8">企业数字化平台</p>

      <div class="w-full max-w-[400px] space-y-4">
        <!-- Username -->
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1.5">用户名</label>
          <div class="relative">
            <User :size="16" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="form.userName"
              type="text"
              autocomplete="username"
              placeholder="请输入用户名"
              class="w-full h-12 pl-10 pr-3 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent"
              @keyup.enter="handleLogin"
            />
          </div>
        </div>

        <!-- Password -->
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1.5">密码</label>
          <div class="relative">
            <Lock :size="16" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="form.password"
              :type="showPassword ? 'text' : 'password'"
              autocomplete="current-password"
              placeholder="请输入密码"
              class="w-full h-12 pl-10 pr-11 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent"
              @keyup.enter="handleLogin"
            />
            <button
              type="button"
              class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground border-0 bg-transparent p-0 cursor-pointer"
              @click="showPassword = !showPassword"
            >
              <Eye v-if="!showPassword" :size="18" />
              <EyeOff v-else :size="18" />
            </button>
          </div>
        </div>

        <!-- Error -->
        <p v-if="errorMsg" class="text-[13px] text-danger text-center">{{ errorMsg }}</p>

        <!-- Submit -->
        <button
          :disabled="loading"
          class="w-full h-12 bg-primary text-primary-foreground text-[15px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
          @click="handleLogin"
        >
          <span v-if="loading" class="flex items-center justify-center gap-2">
            <span class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
            登录中...
          </span>
          <span v-else>登 录</span>
        </button>

        <!-- Forgot password -->
        <p class="text-center">
          <a href="/h5/forgot-password" class="text-[14px] text-primary no-underline">忘记密码？</a>
        </p>
      </div>
    </div>

    <!-- Copyright -->
    <p class="text-center text-[11px] text-muted-foreground pb-6">© 2025 OctopusERP</p>
  </div>

  <!-- ── Desktop layout (lg+) ── -->
  <div class="hidden lg:flex h-screen overflow-hidden">
    <!-- Left branding panel -->
    <div class="flex-1 flex items-center justify-center bg-gradient-to-br from-[#1a237e] via-[#283593] to-[#3949ab]">
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
    <div class="w-[480px] flex items-center justify-center bg-muted">
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

            <!-- Remember me -->
            <div class="flex items-center justify-between">
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input v-model="rememberMe" type="checkbox" class="w-3.5 h-3.5 rounded border-border" />
                <span class="text-[12px] text-muted-foreground">记住我</span>
              </label>
              <a href="/h5/forgot-password" class="text-[12px] text-primary no-underline">忘记密码？</a>
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
