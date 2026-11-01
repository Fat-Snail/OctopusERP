<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Home, ClipboardList, Clock, User, LayoutGrid } from '@lucide/vue'

const route = useRoute()
const router = useRouter()

const tabs = [
  { key: 'home', label: '首页', icon: Home, path: '/h5/home' },
  { key: 'approval', label: '审批', icon: ClipboardList, path: '/h5/approval' },
  { key: 'attendance', label: '考勤', icon: Clock, path: '/h5/attendance' },
  { key: 'profile', label: '我的', icon: User, path: '/h5/profile' },
]

const activeTab = computed(() => {
  const p = route.path
  if (p.startsWith('/h5/approval')) return 'approval'
  if (p.startsWith('/h5/attendance')) return 'attendance'
  if (p.startsWith('/h5/profile')) return 'profile'
  return 'home'
})

function navigate(path: string) {
  router.push(path)
}

function goToHub() {
  window.location.href = 'http://localhost:5173/h5/home'
}
</script>

<template>
  <div class="flex flex-col h-screen bg-subtle overflow-hidden">
    <!-- Content area -->
    <div class="flex-1 overflow-y-auto">
      <router-view />
    </div>

    <!-- Bottom TabBar -->
    <nav class="bg-background border-t border-border safe-area-inset-bottom shrink-0">
      <div class="flex">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          class="flex-1 flex flex-col items-center justify-center gap-1 py-2 border-0 bg-transparent cursor-pointer"
          :class="activeTab === tab.key ? 'text-primary' : 'text-muted-foreground'"
          @click="navigate(tab.path)"
        >
          <component :is="tab.icon" :size="22" />
          <span class="text-[10px] font-medium">{{ tab.label }}</span>
        </button>
        <!-- Switch to UMC hub -->
        <button
          class="flex-1 flex flex-col items-center justify-center gap-1 py-2 border-0 bg-transparent cursor-pointer text-muted-foreground"
          @click="goToHub"
        >
          <LayoutGrid :size="22" />
          <span class="text-[10px] font-medium">切换</span>
        </button>
      </div>
    </nav>
  </div>
</template>
