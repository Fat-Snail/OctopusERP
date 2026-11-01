<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { oidcService } from '@/services/oidcService'
import { useUserStore } from '@/store/user'

const router = useRouter()
const userStore = useUserStore()

onMounted(() => {
  if (oidcService.isLoggedIn()) {
    userStore.loadFromOidc()
    router.replace('/home')
  }
})

function handleLogin() {
  oidcService.login()
}
</script>

<template>
  <div class="flex flex-col h-dvh bg-background">
    <!-- Header gradient -->
    <div class="bg-primary pt-16 pb-10 px-6 text-center flex-none">
      <div class="w-16 h-16 rounded-2xl bg-white/20 flex items-center justify-center mx-auto mb-4">
        <span class="text-white text-[28px] font-bold">OA</span>
      </div>
      <h1 class="text-white text-[24px] font-bold">OctopusOA</h1>
      <p class="text-white/70 text-[14px] mt-1">企业办公协同平台</p>
    </div>

    <!-- Login card -->
    <div class="flex-1 flex flex-col items-center justify-center px-6 -mt-6">
      <div class="w-full bg-background rounded-2xl shadow-lg border border-border p-6">
        <h2 class="text-[18px] font-semibold text-foreground mb-2">欢迎登录</h2>
        <p class="text-[13px] text-muted-foreground mb-6">使用企业账号通过统一身份中心登录</p>

        <button
          class="w-full h-12 bg-primary text-primary-foreground text-[15px] font-medium rounded-xl hover:opacity-90 transition-opacity cursor-pointer border-0"
          @click="handleLogin"
        >
          企业账号登录
        </button>

        <p class="text-[12px] text-muted-foreground text-center mt-4">
          登录即表示您同意公司相关使用条款
        </p>
      </div>
    </div>

    <div class="py-4 text-center text-[11px] text-muted-foreground flex-none">
      © 2025 OctopusERP
    </div>
  </div>
</template>
