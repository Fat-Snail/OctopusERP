<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter, RouterView } from 'vue-router'
import { Home, MessageSquareText, FileText, User } from '@lucide/vue'

const route = useRoute()
const router = useRouter()

const tabs = [
  { path: '/home', label: '首页', icon: Home },
  { path: '/inquiries', label: '询盘', icon: MessageSquareText },
  { path: '/contracts', label: '合同', icon: FileText },
  { path: '/profile', label: '我的', icon: User },
]

const activeTab = computed(() => {
  const p = route.path
  if (p.startsWith('/inquiries')) return '/inquiries'
  if (p.startsWith('/contracts')) return '/contracts'
  if (p.startsWith('/profile')) return '/profile'
  return '/home'
})

const showTabBar = computed(() => {
  const p = route.path
  return !p.startsWith('/inquiries/new') &&
    !p.startsWith('/contracts/') &&
    !p.startsWith('/logistics') &&
    !p.startsWith('/statements') &&
    !p.startsWith('/support') &&
    !p.startsWith('/documents')
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
