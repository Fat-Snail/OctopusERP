<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { LogOut, ShieldCheck } from '@lucide/vue'
import { useUserStore } from '@/store/user'

const router = useRouter()
const userStore = useUserStore()

onMounted(() => {
  if (!userStore.userName) userStore.loadFromOidc()
})

function onLogout() {
  userStore.logout()
  router.replace('/login')
}
</script>

<template>
  <div class="pb-4">
    <header class="px-5 pt-8 pb-6 bg-primary-soft">
      <div class="flex items-center gap-4">
        <div class="w-14 h-14 rounded-2xl bg-primary flex items-center justify-center flex-none">
          <span class="text-primary-foreground text-lg font-semibold">{{ userStore.initials }}</span>
        </div>
        <div class="min-w-0">
          <p class="font-semibold text-base truncate">{{ userStore.userName || '未登录' }}</p>
          <p class="text-xs text-muted-foreground mt-0.5 truncate">{{ userStore.email || '—' }}</p>
        </div>
      </div>
    </header>

    <div class="px-5 mt-5 space-y-2.5">
      <div class="bg-card rounded-xl p-4 shadow-sm">
        <div class="flex items-center justify-between text-sm">
          <span class="text-muted-foreground">我的角色</span>
          <div class="flex gap-1.5">
            <span
              v-if="userStore.roles.length === 0"
              class="text-[11px] px-2 py-0.5 rounded-full bg-muted text-muted-foreground"
            >customer（Mock）</span>
            <span
              v-for="r in userStore.roles"
              :key="r"
              class="text-[11px] px-2 py-0.5 rounded-full bg-primary-soft text-primary-soft-fg"
            >{{ r }}</span>
          </div>
        </div>
        <div class="flex items-center justify-between text-sm mt-3">
          <span class="text-muted-foreground">数据范围</span>
          <span class="flex items-center gap-1 text-success">
            <ShieldCheck :size="14" /> 仅本人数据
          </span>
        </div>
      </div>

      <div class="bg-card rounded-xl p-4 shadow-sm text-sm space-y-3">
        <div class="flex justify-between">
          <span class="text-muted-foreground">身份认证</span>
          <span>UMC OIDC（PKCE）</span>
        </div>
        <div class="flex justify-between">
          <span class="text-muted-foreground">客户端</span>
          <span class="text-xs text-muted-foreground">octopus-customer-portal</span>
        </div>
      </div>

      <button
        class="w-full h-12 rounded-xl bg-card text-danger font-medium cursor-pointer border-0 shadow-sm active:opacity-80 flex items-center justify-center gap-2"
        @click="onLogout"
      >
        <LogOut :size="17" /> 退出登录
      </button>

      <p class="text-center text-[11px] text-muted-foreground pt-2">Octopus 客户门户 v1.0.0 · Mock 数据版</p>
    </div>
  </div>
</template>
