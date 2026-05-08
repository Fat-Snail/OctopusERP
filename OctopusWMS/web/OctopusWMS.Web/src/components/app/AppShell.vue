<script setup lang="ts">
import ModuleRail from './ModuleRail.vue'
import SubMenu from './SubMenu.vue'
import TopBar from './TopBar.vue'
</script>

<template>
  <div class="flex h-full bg-background overflow-hidden">
    <!-- 56px module rail -->
    <ModuleRail active-module="wms" />

    <!-- 200px sub menu -->
    <SubMenu />

    <!-- Main content area -->
    <div class="flex-1 min-w-0 flex flex-col overflow-hidden">
      <TopBar />
      <main class="flex-1 overflow-y-auto bg-[var(--subtle)]">
        <slot />
      </main>
    </div>
  </div>
</template>
