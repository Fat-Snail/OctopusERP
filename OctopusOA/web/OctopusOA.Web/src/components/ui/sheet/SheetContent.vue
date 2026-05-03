<script setup lang="ts">
import { inject, type ComputedRef } from 'vue'
import { X } from 'lucide-vue-next'
import { cn } from '@/lib/utils'

type Side = 'left' | 'right' | 'top' | 'bottom'
interface SheetCtx { open: ComputedRef<boolean>; close: () => void }

const props = defineProps<{ class?: string; side?: Side }>()
const ctx = inject<SheetCtx>('sheetCtx')!

// Default widths — consumer's class="w-[N]" will override via tailwind-merge
const sideClass: Record<Side, string> = {
  right:  'inset-y-0 right-0 h-full w-[480px] border-l',
  left:   'inset-y-0 left-0 h-full w-[480px] border-r',
  top:    'inset-x-0 top-0 w-full border-b',
  bottom: 'inset-x-0 bottom-0 w-full border-t',
}
</script>

<template>
  <Teleport to="body">
    <template v-if="ctx.open.value">
      <!-- Backdrop -->
      <div class="fixed inset-0 z-50 bg-black/60" @click="ctx.close()" />

      <!-- Panel -->
      <div
        :class="cn(
          'fixed z-50 bg-background shadow-xl flex flex-col',
          sideClass[props.side ?? 'right'],
          props.class,
        )"
      >
        <!-- Close button row (never scrolls) -->
        <div class="shrink-0 flex items-center justify-end px-4 pt-3 pb-2">
          <button
            class="rounded-md p-1 opacity-60 hover:opacity-100 hover:bg-muted transition-all"
            type="button"
            @click="ctx.close()"
          >
            <X class="w-4 h-4 text-foreground" />
            <span class="sr-only">关闭</span>
          </button>
        </div>

        <!-- Scrollable content -->
        <div class="flex-1 overflow-y-auto px-6 pb-6">
          <slot />
        </div>
      </div>
    </template>
  </Teleport>
</template>
