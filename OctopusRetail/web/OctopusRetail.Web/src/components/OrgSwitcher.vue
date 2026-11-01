<script setup lang="ts">
import { roleLabel } from '@/lib/tenant'
import { useWorkspaceStore } from '@/stores/workspace'

/**
 * 页面内的组织切换器。
 *
 * 顶栏虽然有 WorkspaceSwitcher，但用户在「成员管理」「邀请管理」里
 * 很容易忘记自己当前在哪个组织，看到的人数不对就以为功能坏了
 * （实测：zhangsan 既是上海店 manager 又是子公司 owner，
 *   默认落在子公司，于是看不到上海店的店员）。
 */
const emit = defineEmits<{ (e: 'switched'): void }>()
const workspace = useWorkspaceStore()

function onChange(e: Event) {
  const id = (e.target as HTMLSelectElement).value
  if (id === workspace.activeTenantId) return
  workspace.switchTo(id)
  emit('switched')
}
</script>

<template>
  <div v-if="workspace.workspaces.length > 1" class="flex flex-wrap items-center gap-2 text-sm">
    <span class="text-gray-400">切换组织</span>
    <select
      :value="workspace.activeTenantId"
      class="rounded-md border border-gray-300 bg-white px-2 py-1 text-sm"
      @change="onChange"
    >
      <option v-for="w in workspace.workspaces" :key="w.tenantId" :value="w.tenantId">
        {{ w.tenantName }}（{{ roleLabel(w.role) }}）
      </option>
    </select>
    <span class="text-xs text-gray-400">共 {{ workspace.workspaces.length }} 个组织，人数按组织隔离</span>
  </div>
</template>
