<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Home, User } from '@lucide/vue'

const route = useRoute()
const router = useRouter()

const tabs = [
  { key: 'home', label: '首页', icon: Home, path: '/home' },
  { key: 'profile', label: '我的', icon: User, path: '/profile' },
]

const activeTab = computed(() =>
  route.path.startsWith('/profile') ? 'profile' : 'home'
)
</script>

<template>
  <div class="flex flex-col h-dvh overflow-hidden" style="background:var(--subtle)">
    <div class="flex-1 overflow-y-auto">
      <router-view />
    </div>
    <nav class="bg-background border-t border-border shrink-0 safe-area-inset-bottom">
      <div class="flex">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          class="flex-1 flex flex-col items-center justify-center gap-1 py-2 border-0 bg-transparent cursor-pointer transition-colors"
          :style="activeTab === tab.key ? 'color:hsl(var(--primary))' : 'color:hsl(var(--muted-foreground))'"
          @click="router.push(tab.path)"
        >
          <component :is="tab.icon" :size="22" />
          <span class="text-[10px] font-medium">{{ tab.label }}</span>
        </button>
      </div>
    </nav>
  </div>
</template>
