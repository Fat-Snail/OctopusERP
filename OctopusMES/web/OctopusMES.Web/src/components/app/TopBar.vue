<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Search, Bell, LogOut } from 'lucide-vue-next'
import AvatarVue from '@/components/ui/avatar.vue'
import { useUserStore } from '@/store/modules/user'
import { oidcService } from '@/services/oidcService'

const route = useRoute()
const userStore = useUserStore()

const breadcrumbs = computed(() => {
  const title = route.meta['title'] as string | undefined
  if (!title) return ['MES 生产采购']
  return ['MES 生产采购', title]
})

const userInitials = computed(() => {
  const name = userStore.userName || userStore.email || '?'
  return name.slice(0, 2).toUpperCase()
})

function handleLogout() {
  oidcService.logout()
}
</script>

<template>
  <header class="h-12 shrink-0 bg-card border-b border-border flex items-center px-4 gap-3">
    <!-- Breadcrumb -->
    <div class="flex items-center gap-1.5 text-[12px] text-muted-foreground">
      <template v-for="(crumb, i) in breadcrumbs" :key="i">
        <span :class="i === breadcrumbs.length - 1 ? 'text-foreground font-medium' : ''">{{ crumb }}</span>
        <span v-if="i < breadcrumbs.length - 1" class="text-[10px]">›</span>
      </template>
    </div>

    <div class="flex-1" />

    <!-- Search -->
    <div class="flex items-center gap-1.5 h-7 px-2.5 bg-muted rounded-[var(--radius)] w-[240px] text-[12px] text-muted-foreground cursor-text">
      <Search :size="13" class="shrink-0" />
      <span class="flex-1">搜索...</span>
      <kbd class="font-mono text-[10px] px-1 py-0 rounded border border-border bg-subtle text-muted-foreground">⌘K</kbd>
    </div>

    <!-- Notification bell -->
    <button
      class="relative w-8 h-8 flex items-center justify-center text-muted-foreground hover:text-foreground hover:bg-muted rounded-[var(--radius)] transition-colors cursor-pointer border-0"
      title="通知"
    >
      <Bell :size="16" />
    </button>

    <!-- User avatar + logout -->
    <div class="flex items-center gap-1.5">
      <AvatarVue :fallback="userInitials" size="sm" class="cursor-pointer" />
      <button
        class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-danger transition-colors cursor-pointer border-0 bg-transparent p-1 rounded"
        title="退出登录"
        @click="handleLogout"
      >
        <LogOut :size="14" />
      </button>
    </div>
  </header>
</template>
