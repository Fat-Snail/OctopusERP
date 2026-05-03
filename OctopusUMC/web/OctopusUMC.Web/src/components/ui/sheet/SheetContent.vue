<script setup lang="ts">
import { inject, type ComputedRef } from 'vue'
import { X } from 'lucide-vue-next'
import { cn } from '@/lib/utils'

type Side = 'left' | 'right' | 'top' | 'bottom'
interface SheetCtx { open: ComputedRef<boolean>; close: () => void }

const props = defineProps<{ class?: string; side?: Side }>()
const ctx = inject<SheetCtx>('sheetCtx')!

const sideClass: Record<Side, string> = {
  right: 'inset-y-0 right-0 h-full w-3/4 sm:max-w-sm border-l',
  left:  'inset-y-0 left-0 h-full w-3/4 sm:max-w-sm border-r',
  top:   'inset-x-0 top-0 border-b',
  bottom: 'inset-x-0 bottom-0 border-t',
}
</script>

<template>
  <Teleport to="body">
    <template v-if="ctx.open.value">
      <div class="fixed inset-0 z-50 bg-black/80" @click="ctx.close()" />
      <div
        :class="cn(
          'fixed z-50 bg-background p-6 shadow-lg overflow-y-auto',
          sideClass[props.side ?? 'right'],
          props.class,
        )"
      >
        <slot />
        <button
          class="absolute right-4 top-4 rounded-sm opacity-70 hover:opacity-100 transition-opacity"
          type="button"
          @click="ctx.close()"
        >
          <X class="w-4 h-4 text-muted-foreground" />
          <span class="sr-only">Close</span>
        </button>
      </div>
    </template>
  </Teleport>
</template>
