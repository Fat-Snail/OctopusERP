<script setup lang="ts">
import { onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import WorkspaceSwitcher from '@/components/WorkspaceSwitcher.vue'
import { useWorkspaceStore } from '@/stores/workspace'
import { logout } from '@/services/oidcService'

const route = useRoute()
const router = useRouter()
const workspace = useWorkspaceStore()

const isPublicPage = () => route.meta.public === true

async function bootstrap() {
  if (isPublicPage()) return
  // 已登录但尚未加载 workspace（例如刷新页面）时补一次
  if (!workspace.hasWorkspaces) {
    await workspace.load()
    if (!workspace.activeWorkspace && workspace.hasWorkspaces) {
      await router.replace('/workspaces')
    }
  }
}

onMounted(bootstrap)
watch(() => route.fullPath, bootstrap)

async function onLogout() {
  workspace.clear()
  await logout()
}
</script>

<template>
  <div class="min-h-screen">
    <header
      v-if="!isPublicPage()"
      class="flex items-center justify-between gap-4 border-b border-gray-200 bg-white px-5 py-3"
    >
      <div class="flex items-center gap-3">
        <span class="text-base font-semibold">OctopusRetail</span>
        <span class="text-xs text-gray-400">工作台</span>
      </div>

      <div class="flex items-center gap-3">
        <WorkspaceSwitcher v-if="workspace.hasWorkspaces" />
        <template v-if="workspace.canInvite">
          <router-link
            to="/members"
            class="rounded-md border border-gray-300 px-3 py-1.5 text-sm hover:bg-gray-50"
          >
            成员管理
          </router-link>
          <router-link
            to="/invites"
            class="rounded-md border border-gray-300 px-3 py-1.5 text-sm hover:bg-gray-50"
          >
            邀请管理
          </router-link>
        </template>
        <button
          class="rounded-md px-3 py-1.5 text-sm text-gray-500 hover:bg-gray-100"
          @click="onLogout"
        >
          退出
        </button>
      </div>
    </header>

    <main>
      <router-view />
    </main>
  </div>
</template>
