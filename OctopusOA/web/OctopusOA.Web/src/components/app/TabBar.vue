<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { X, XCircle } from 'lucide-vue-next'
import { useTabsStore } from '@/store/modules/tabs'

const router = useRouter()
const tabsStore = useTabsStore()

// ── context menu ──
const ctxVisible = ref(false)
const ctxX = ref(0)
const ctxY = ref(0)
const ctxPath = ref('')

function showCtx(e: MouseEvent, path: string) {
  e.preventDefault()
  ctxPath.value = path
  ctxX.value = e.clientX
  ctxY.value = e.clientY
  ctxVisible.value = true
}
function hideCtx() { ctxVisible.value = false }

function ctxClose() {
  const to = tabsStore.closeTab(ctxPath.value)
  router.push(to)
  hideCtx()
}
function ctxCloseOthers() {
  tabsStore.closeOthers(ctxPath.value)
  router.push(ctxPath.value)
  hideCtx()
}
function ctxCloseAll() {
  const to = tabsStore.closeAll()
  router.push(to)
  hideCtx()
}

// ── tab actions ──
function switchTab(path: string) {
  tabsStore.activeTab = path
  router.push(path)
}

function closeTab(e: MouseEvent, path: string) {
  e.stopPropagation()
  const to = tabsStore.closeTab(path)
  router.push(to)
}
</script>

<template>
  <!-- Tab strip -->
  <div
    class="flex items-end h-[34px] bg-subtle border-b border-border overflow-x-auto overflow-y-hidden shrink-0"
    style="scrollbar-width:none;"
  >
    <div
      v-for="tab in tabsStore.tabs"
      :key="tab.path"
      :class="[
        'relative inline-flex items-center gap-1.5 h-[30px] px-3 text-[12px] shrink-0',
        'cursor-pointer select-none rounded-t-[var(--radius)] border border-transparent border-b-0',
        'transition-colors mr-0.5 top-px',
        tab.path === tabsStore.activeTab
          ? 'bg-card border-border text-foreground font-medium'
          : 'text-muted-foreground hover:bg-muted hover:text-foreground'
      ]"
      :style="tab.path === tabsStore.activeTab ? 'border-bottom: 1px solid var(--card)' : ''"
      @click="switchTab(tab.path)"
      @contextmenu="showCtx($event, tab.path)"
    >
      <!-- active indicator bar -->
      <span
        v-if="tab.path === tabsStore.activeTab"
        class="absolute left-0 right-0 h-[2px] bg-primary rounded-t"
        style="bottom:-1px;"
      />
      <span class="max-w-[120px] truncate">{{ tab.title }}</span>
      <button
        v-if="tab.closable"
        class="w-4 h-4 flex items-center justify-center rounded-full text-muted-foreground hover:bg-red-100 hover:text-red-500 transition-colors shrink-0 border-0 bg-transparent cursor-pointer"
        @click="closeTab($event, tab.path)"
      >
        <X :size="10" />
      </button>
    </div>

    <!-- right: close-all button -->
    <button
      class="ml-auto mr-1 shrink-0 self-center w-6 h-6 flex items-center justify-center rounded text-muted-foreground hover:bg-muted hover:text-foreground transition-colors border-0 bg-transparent cursor-pointer"
      title="关闭全部标签"
      @click="ctxCloseAll"
    >
      <XCircle :size="14" />
    </button>
  </div>

  <!-- Context Menu -->
  <Teleport to="body">
    <div
      v-if="ctxVisible"
      class="fixed z-[9999] bg-card border border-border rounded-[var(--radius)] shadow-lg py-1 min-w-[140px]"
      :style="{ left: ctxX + 'px', top: ctxY + 'px' }"
      @mouseleave="hideCtx"
    >
      <button class="w-full text-left px-3 py-1.5 text-[12px] hover:bg-muted text-foreground cursor-pointer border-0 bg-transparent" @click="ctxClose">关闭标签</button>
      <button class="w-full text-left px-3 py-1.5 text-[12px] hover:bg-muted text-foreground cursor-pointer border-0 bg-transparent" @click="ctxCloseOthers">关闭其他标签</button>
      <div class="my-1 border-t border-border" />
      <button class="w-full text-left px-3 py-1.5 text-[12px] hover:bg-red-50 text-red-500 cursor-pointer border-0 bg-transparent" @click="ctxCloseAll">关闭全部标签</button>
    </div>
    <div v-if="ctxVisible" class="fixed inset-0 z-[9998]" @click="hideCtx" />
  </Teleport>
</template>
