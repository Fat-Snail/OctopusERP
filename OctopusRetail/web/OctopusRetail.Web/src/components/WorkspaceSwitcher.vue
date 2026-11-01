<script setup lang="ts">
import { roleLabel } from '@/lib/tenant'
import { computed, ref } from 'vue'
import { useWorkspaceStore } from '@/stores/workspace'

const workspace = useWorkspaceStore()
const open = ref(false)

const label = computed(() => workspace.activeWorkspace?.tenantName ?? '未选择组织')


function pick(tenantId: string) {
  workspace.switchTo(tenantId)
  open.value = false
}
</script>

<template>
  <div class="relative">
    <button
      class="flex items-center gap-2 rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm hover:bg-gray-50"
      @click="open = !open"
    >
      <span class="text-gray-400">组织</span>
      <span class="font-medium">{{ label }}</span>
      <span class="text-gray-400">▾</span>
    </button>

    <div
      v-if="open"
      class="absolute right-0 z-20 mt-1 w-80 rounded-md border border-gray-200 bg-white py-1 shadow-lg"
    >
      <div class="px-3 py-1.5 text-xs text-gray-400">
        切换工作空间（{{ workspace.workspaces.length }} 个组织）
      </div>

      <button
        v-for="w in workspace.workspaces"
        :key="w.tenantId"
        class="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-gray-50"
        :class="w.tenantId === workspace.activeTenantId ? 'bg-gray-50 font-medium' : ''"
        @click="pick(w.tenantId)"
      >
        <span class="flex flex-col">
          <span>{{ w.tenantName }}</span>
          <span class="text-xs text-gray-400">{{ w.tenantId }}</span>
        </span>
        <span class="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
          {{ roleLabel(w.role) }}
        </span>
      </button>

      <div class="border-t border-gray-100 px-3 py-2">
        <router-link to="/workspaces" class="text-xs text-blue-600 hover:underline" @click="open = false">
          查看全部工作空间
        </router-link>
      </div>
    </div>
  </div>
</template>
