<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { ChevronRight, Bell, Clock, ClipboardList, LogOut, User, Shield } from '@lucide/vue'
import { useUserStore } from '@/store/modules/user'
import { oidcService } from '@/services/oidcService'

const router = useRouter()
const userStore = useUserStore()

const initials = computed(() => {
  const name = userStore.userName || 'U'
  return name.slice(0, 1).toUpperCase()
})

const menuItems = [
  { icon: ClipboardList, label: '我的申请', path: '/h5/approval?tab=mine' },
  { icon: Bell, label: '公告通知', path: '/h5/notice' },
  { icon: Clock, label: '考勤记录', path: '/h5/attendance' },
]

function handleLogout() {
  if (confirm('确认退出登录？')) {
    oidcService.logout()
  }
}

function jumpToUmcProfile() {
  // Jump to UMC H5 profile for account settings
  window.location.href = 'http://localhost:5173/h5/profile'
}
</script>

<template>
  <div class="min-h-full bg-subtle">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4">
        <h1 class="text-[17px] font-semibold text-foreground">我的</h1>
      </div>
    </div>

    <!-- User card -->
    <div class="bg-background border-b border-border py-6 flex items-center gap-4 px-5">
      <div class="w-14 h-14 rounded-full bg-primary-soft flex items-center justify-center shrink-0">
        <span class="text-primary-soft-fg font-semibold text-[22px]">{{ initials }}</span>
      </div>
      <div class="flex-1 min-w-0">
        <p class="text-[16px] font-semibold text-foreground">{{ userStore.userName }}</p>
        <p class="text-[12px] text-muted-foreground mt-0.5 truncate">{{ userStore.email }}</p>
      </div>
    </div>

    <!-- Menu section -->
    <div class="mt-3">
      <p class="text-[11px] text-muted-foreground uppercase tracking-wide px-4 mb-2">功能</p>
      <div class="bg-background border-y border-border divide-y divide-border">
        <button
          v-for="item in menuItems"
          :key="item.label"
          class="w-full flex items-center gap-3 px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="router.push(item.path)"
        >
          <div class="w-8 h-8 rounded-lg bg-primary-soft flex items-center justify-center shrink-0">
            <component :is="item.icon" :size="16" class="text-[var(--primary-soft-fg)]" />
          </div>
          <span class="text-[14px] text-foreground flex-1">{{ item.label }}</span>
          <ChevronRight :size="16" class="text-muted-foreground" />
        </button>
      </div>
    </div>

    <!-- Account section -->
    <div class="mt-3">
      <p class="text-[11px] text-muted-foreground uppercase tracking-wide px-4 mb-2">账号</p>
      <div class="bg-background border-y border-border divide-y divide-border">
        <button
          class="w-full flex items-center gap-3 px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="jumpToUmcProfile"
        >
          <div class="w-8 h-8 rounded-lg bg-muted flex items-center justify-center shrink-0">
            <User :size="16" class="text-muted-foreground" />
          </div>
          <span class="text-[14px] text-foreground flex-1">账号设置</span>
          <ChevronRight :size="16" class="text-muted-foreground" />
        </button>
        <button
          class="w-full flex items-center gap-3 px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="jumpToUmcProfile"
        >
          <div class="w-8 h-8 rounded-lg bg-muted flex items-center justify-center shrink-0">
            <Shield :size="16" class="text-muted-foreground" />
          </div>
          <span class="text-[14px] text-foreground flex-1">修改密码</span>
          <ChevronRight :size="16" class="text-muted-foreground" />
        </button>
      </div>
    </div>

    <!-- Logout -->
    <div class="mt-3 px-4">
      <button
        class="w-full h-12 bg-background border border-border text-danger text-[15px] font-medium rounded-xl hover:bg-danger/5 transition-colors cursor-pointer flex items-center justify-center gap-2"
        @click="handleLogout"
      >
        <LogOut :size="18" />
        退出登录
      </button>
    </div>

    <div class="py-6 text-center text-[11px] text-muted-foreground">© 2025 OctopusERP</div>
  </div>
</template>
