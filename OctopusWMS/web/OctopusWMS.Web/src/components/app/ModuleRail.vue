<script setup lang="ts">
import {
  LayoutDashboard, Users, Briefcase, Package, Warehouse,
  Factory, TrendingUp, Settings
} from 'lucide-vue-next'
import { cn } from '@/lib/utils'

const props = defineProps<{
  activeModule: string
}>()

const modules = [
  { id: 'workbench', icon: LayoutDashboard, label: '工作台',  href: null,                    disabled: true },
  { id: 'user',      icon: Users,           label: '用户中心', href: 'http://localhost:5173', disabled: false },
  { id: 'oa',        icon: Briefcase,       label: 'OA 协同',  href: 'http://localhost:5174', disabled: false },
  { id: 'plm',       icon: Package,         label: 'PLM',       href: 'http://localhost:5175', disabled: false },
  { id: 'crm',       icon: TrendingUp,      label: 'CRM',       href: 'http://localhost:5176', disabled: false },
  { id: 'wms',       icon: Warehouse,       label: 'WMS',       href: null,                    disabled: false },
  { id: 'mes',       icon: Factory,         label: 'MES',       href: 'http://localhost:5178', disabled: false },
]

function handleClick(mod: typeof modules[0]) {
  if (mod.disabled) return
  if (mod.id === props.activeModule) return
  if (mod.href) window.location.href = mod.href
}

const appVersion = __APP_VERSION__
const buildDate = __BUILD_DATE__
</script>

<template>
  <aside class="w-14 shrink-0 bg-subtle border-r border-border flex flex-col items-center py-3 gap-1">
    <!-- Logo -->
    <div class="w-8 h-8 rounded-lg bg-primary flex items-center justify-center mb-2 shrink-0">
      <span class="text-primary-foreground font-bold text-[12px] font-mono-tnum">OC</span>
    </div>

    <!-- Module items -->
    <button
      v-for="mod in modules"
      :key="mod.id"
      :title="mod.label"
      :disabled="mod.disabled"
      :class="cn(
        'w-10 h-10 rounded-lg flex flex-col items-center justify-center gap-0.5 transition-colors cursor-pointer border-0 p-0',
        mod.id === activeModule
          ? 'bg-primary-soft text-primary-soft-fg'
          : 'text-muted-foreground hover:bg-muted hover:text-foreground',
        mod.disabled && 'opacity-30 cursor-not-allowed hover:bg-transparent hover:text-muted-foreground'
      )"
      @click="handleClick(mod)"
    >
      <component :is="mod.icon" :size="18" class="shrink-0" />
      <span class="text-[9px] leading-none">{{ mod.label.slice(0, 2) }}</span>
    </button>

    <div class="flex-1" />

    <!-- Settings -->
    <button
      class="w-10 h-10 flex items-center justify-center text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg transition-colors cursor-pointer border-0 p-0"
      title="设置"
    >
      <Settings :size="18" />
    </button>

    <!-- Version badge -->
    <div
      class="flex flex-col items-center gap-px pb-0.5 cursor-default select-none"
      :title="`${appVersion} build ${buildDate}`"
    >
      <span class="text-[8px] font-mono leading-none tracking-tight text-muted-foreground/60">{{ appVersion }}</span>
      <span class="text-[8px] font-mono leading-none tracking-tighter text-muted-foreground/40">{{ buildDate }}</span>
    </div>
  </aside>
</template>
