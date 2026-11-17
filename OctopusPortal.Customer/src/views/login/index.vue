<script setup lang="ts">
import { useRouter } from 'vue-router'
import { Container } from '@lucide/vue'
import { oidcService } from '@/services/oidcService'

const router = useRouter()

async function onLogin() {
  await oidcService.login()
  // 登录成功后 UMC 会 302 到 /callback；失败时停留在登录页
  setTimeout(() => {
    if (oidcService.isLoggedIn()) router.replace('/home')
  }, 800)
}

function onBrowse() {
  router.replace('/home')
}
</script>

<template>
  <div class="min-h-dvh bg-background flex flex-col">
    <div class="flex-1 flex flex-col items-center justify-center px-8 gap-6">
      <div class="w-20 h-20 rounded-3xl bg-primary flex items-center justify-center">
        <Container :size="40" class="text-primary-foreground" />
      </div>
      <div class="text-center">
        <h1 class="text-xl font-semibold">Octopus 客户门户</h1>
        <p class="text-muted-foreground mt-2 text-sm leading-relaxed">
          询盘 · 报价 · 合同 · 物流 · 对账<br />一站掌握您的合作动态
        </p>
      </div>
    </div>

    <div class="flex-none px-8 pb-12 safe-area-inset-bottom space-y-3">
      <button
        class="w-full h-12 rounded-xl bg-primary text-primary-foreground font-medium cursor-pointer border-0 active:opacity-80 transition-opacity"
        @click="onLogin"
      >
        使用 UMC 账号登录
      </button>
      <button
        class="w-full h-12 rounded-xl bg-muted text-foreground font-medium cursor-pointer border-0 active:opacity-80 transition-opacity"
        @click="onBrowse"
      >
        先逛逛（Mock 数据）
      </button>
      <p class="text-center text-xs text-muted-foreground mt-2">登录后可查看与您关联的真实数据</p>
    </div>
  </div>
</template>
