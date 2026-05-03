<script setup lang="ts">
import ModuleRail from './ModuleRail.vue'
import SubMenu from './SubMenu.vue'
import TopBar from './TopBar.vue'
import TabBar from './TabBar.vue'

defineProps<{
  pendingCount?: number
  unreadCount?: number
}>()
</script>

<template>
  <div class="flex h-full bg-background overflow-hidden">
    <!-- 56px module rail -->
    <ModuleRail active-module="oa" />

    <!-- 200px sub menu -->
    <SubMenu :pending-count="pendingCount" :unread-notice-count="unreadCount" />

    <!-- Main content area -->
    <div class="flex-1 min-w-0 flex flex-col overflow-hidden">
      <TopBar :unread-count="unreadCount" />
      <TabBar />
      <main class="flex-1 overflow-y-auto bg-[var(--subtle)]">
        <slot />
      </main>
    </div>
  </div>
</template>
