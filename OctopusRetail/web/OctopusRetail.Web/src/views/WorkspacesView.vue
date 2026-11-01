<script setup lang="ts">
import { useWorkspaceStore } from '@/stores/workspace'
import { roleLabel, tenantNoun, tenantTypeLabel } from '@/lib/tenant'

const workspace = useWorkspaceStore()


</script>

<template>
  <div class="mx-auto max-w-4xl p-6">
    <h1 class="text-lg font-semibold">我的工作空间</h1>
    <p class="mt-1 text-sm text-gray-500">
      一个账号可以同时属于多个组织。切换后，所有后续请求都会携带该组织（租户）的上下文。
    </p>

    <!-- 身份信息：被邀请加入的人登录后先看清"我是谁、我属于哪些组织" -->
    <div
      v-if="workspace.identity"
      class="mt-4 flex flex-wrap items-center gap-x-6 gap-y-1 rounded-lg border bg-white px-4 py-3 text-sm"
    >
      <div>
        <span class="text-gray-400">当前账号</span>
        <span class="ml-2 font-medium">{{ workspace.identity.displayName || workspace.identity.userName }}</span>
      </div>
      <div>
        <span class="text-gray-400">用户名</span>
        <span class="ml-2">{{ workspace.identity.userName }}</span>
      </div>
      <div>
        <span class="text-gray-400">可进入组织</span>
        <span class="ml-2">{{ workspace.identity.availableTenants?.length ?? 0 }} 个</span>
      </div>
    </div>

    <p v-if="workspace.loading" class="mt-6 text-sm text-gray-400">加载中…</p>
    <p v-else-if="workspace.error" class="mt-6 text-sm text-red-600">{{ workspace.error }}</p>

    <div v-else-if="!workspace.hasWorkspaces" class="mt-6 rounded-lg border border-dashed border-gray-300 p-8 text-center">
      <p class="text-sm text-gray-500">你还没有加入任何组织。</p>
      <p class="mt-1 text-xs text-gray-400">请让管理员向你发送邀请链接。</p>
    </div>

    <ul v-else class="mt-6 space-y-3">
      <li
        v-for="w in workspace.workspaces"
        :key="w.tenantId"
        class="flex items-center justify-between rounded-lg border bg-white px-4 py-3"
        :class="w.tenantId === workspace.activeTenantId ? 'border-blue-400' : 'border-gray-200'"
      >
        <div>
          <div class="flex items-center gap-2">
            <span class="font-medium">{{ w.tenantName }}</span>
            <span class="rounded bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
              {{ tenantTypeLabel(w.tenantType) }}
            </span>
            <span
              v-if="w.tenantId === workspace.activeTenantId"
              class="rounded bg-blue-50 px-2 py-0.5 text-xs text-blue-600"
            >
              当前
            </span>
          </div>
          <div class="mt-1 text-xs text-gray-400">
            {{ w.tenantId }} · 角色 {{ roleLabel(w.role) }}
          </div>
        </div>

        <button
          v-if="w.tenantId !== workspace.activeTenantId"
          class="rounded-md border border-gray-300 px-3 py-1.5 text-sm hover:bg-gray-50"
          @click="workspace.switchTo(w.tenantId)"
        >
          切换到此{{ tenantNoun(w.tenantType) }}
        </button>
      </li>
    </ul>
  </div>
</template>
