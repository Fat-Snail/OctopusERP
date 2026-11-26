<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { LogOut, ChevronRight } from '@lucide/vue'
import { cn } from '@/lib/utils'
import AvatarVue from '@/components/ui/avatar.vue'
import { useUserStore } from '@/store/modules/user'
import { oidcService } from '@/services/oidcService'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const groups: { group: string; items: { label: string; path: string }[] }[] = [
  { group: '总览', items: [
    { label: '人力概览', path: '/home' }
  ] },
  { group: '组织', items: [
    { label: '员工档案', path: '/employees' }
  ] },
  { group: '薪酬绩效', items: [
    { label: '薪资核算', path: '/payroll' },
    { label: '假期与绩效', path: '/leave-kpi' }
  ] }
]

const breadcrumb = computed(() => route.meta['title'] as string | undefined)
const userInitials = computed(() => (userStore.userName || userStore.email || '?').slice(0, 2).toUpperCase())
const appVersion = __APP_VERSION__

function isActive(path: string) { return route.path === path || route.path.startsWith(path + '/') }
function handleLogout() { oidcService.logout() }
</script>

<template>
  <div class="flex h-full bg-background overflow-hidden">
    <aside class="w-[200px] shrink-0 bg-card border-r border-border flex flex-col">
      <div class="px-3.5 pt-3.5 pb-2.5 border-b border-border shrink-0">
        <div class="text-[13px] font-semibold text-foreground">OctopusHRM</div>
        <div class="text-[10.5px] text-muted-foreground font-mono tracking-[0.04em] mt-0.5">HRM</div>
      </div>
      <nav class="flex-1 overflow-y-auto py-2 px-2 scrollbar-thin">
        <div v-for="grp in groups" :key="grp.group" class="mb-3">
          <div class="text-[10px] uppercase tracking-[0.06em] font-medium text-muted-foreground px-2 py-1.5">{ grp.group }</div>
          <button
            v-for="item in grp.items" :key="item.path"
            :class="cn('w-full flex items-center px-2 h-[var(--row-h)] rounded-[var(--radius)] text-[12px] transition-colors cursor-pointer border-0 text-left',
              isActive(item.path) ? 'bg-primary-soft text-primary-soft-fg font-medium' : 'text-muted-foreground hover:bg-muted hover:text-foreground')"
            @click="router.push(item.path)"
          >
            <span class="flex-1 truncate">{ item.label }</span>
          </button>
        </div>
      </nav>
      <div class="px-3.5 py-2 border-t border-border text-[10px] text-muted-foreground font-mono">{ appVersion }</div>
    </aside>

    <div class="flex-1 min-w-0 flex flex-col overflow-hidden">
      <header class="h-12 shrink-0 bg-card border-b border-border flex items-center px-4 gap-3">
        <div class="flex items-center gap-1.5 text-[12px] text-muted-foreground">
          <span>OctopusHRM</span>
          <ChevronRight :size="12" v-if="breadcrumb" />
          <span v-if="breadcrumb" class="text-foreground font-medium">{ breadcrumb }</span>
        </div>
        <div class="flex-1" />
        <div class="flex items-center gap-2">
          <AvatarVue :fallback="userInitials" size="sm" />
          <span class="text-[12px] text-muted-foreground">{ userStore.userName || '用户' }</span>
          <button class="flex items-center justify-center text-muted-foreground hover:text-danger transition-colors cursor-pointer border-0 bg-transparent p-1 rounded" title="退出登录" @click="handleLogout">
            <LogOut :size="14" />
          </button>
        </div>
      </header>
      <main class="flex-1 overflow-y-auto bg-[var(--subtle)]">
        <router-view />
      </main>
    </div>
  </div>
</template>
