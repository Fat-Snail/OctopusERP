<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter, RouterView } from 'vue-router'
import { Home, ClipboardList, Clock, User } from '@lucide/vue'

const route = useRoute()
const router = useRouter()

const tabs = [
  { path: '/home', label: '首页', icon: Home },
  { path: '/approval', label: '审批', icon: ClipboardList },
  { path: '/attendance', label: '考勤', icon: Clock },
  { path: '/profile', label: '我的', icon: User },
]

const activeTab = computed(() => {
  const p = route.path
  if (p.startsWith('/approval')) return '/approval'
  if (p.startsWith('/attendance')) return '/attendance'
  if (p.startsWith('/profile')) return '/profile'
  return '/home'
})

const showTabBar = computed(() => {
  const p = route.path
  return !p.startsWith('/approval/detail') &&
    !p.startsWith('/notice/') &&
    p !== '/contact' &&
    p !== '/meeting'
})
</script>

<template>
  <div class="flex flex-col h-dvh bg-subtle overflow-hidden">
    <!-- Scrollable content -->
    <div class="flex-1 overflow-y-auto overscroll-none bg-background">
      <RouterView />
    </div>

    <!-- Bottom tab bar -->
    <nav
      v-if="showTabBar"
      class="flex-none bg-background border-t border-border safe-area-inset-bottom"
    >
      <div class="flex h-14">
        <button
          v-for="tab in tabs"
          :key="tab.path"
          :class="[
            'flex-1 flex flex-col items-center justify-center gap-0.5 border-0 cursor-pointer transition-colors',
            activeTab === tab.path
              ? 'text-primary bg-transparent'
              : 'text-muted-foreground bg-transparent hover:text-foreground',
          ]"
          @click="router.push(tab.path)"
        >
          <component :is="tab.icon" :size="20" />
          <span class="text-[10px]">{{ tab.label }}</span>
        </button>
      </div>
    </nav>
  </div>
</template>
