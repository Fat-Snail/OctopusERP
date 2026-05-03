<script setup lang="ts">
import { inject, type ComputedRef } from 'vue'
import { X } from 'lucide-vue-next'
import { cn } from '@/lib/utils'

interface DialogCtx { open: ComputedRef<boolean>; close: () => void }

const props = defineProps<{ class?: string }>()
const ctx = inject<DialogCtx>('dialogCtx')!
</script>

<template>
  <Teleport to="body">
    <template v-if="ctx.open.value">
      <div class="fixed inset-0 z-50 bg-black/80" @click="ctx.close()" />
      <div
        :class="cn(
          'fixed left-1/2 top-1/2 z-50 -translate-x-1/2 -translate-y-1/2 grid w-full max-w-lg gap-4 border border-border bg-background p-6 shadow-lg sm:rounded-lg',
          props.class,
        )"
      >
        <slot />
        <button
          class="absolute right-3 top-3 p-0.5 rounded-md hover:bg-secondary transition-colors"
          type="button"
          @click="ctx.close()"
        >
          <X class="w-4 h-4" />
          <span class="sr-only">Close</span>
        </button>
      </div>
    </template>
  </Teleport>
</template>
