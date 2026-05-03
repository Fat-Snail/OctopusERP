<script setup lang="ts">
import { inject, type Ref } from 'vue'
import { cn } from '@/lib/utils'

interface SelectCtx { open: Ref<boolean> }

const props = defineProps<{ class?: string; position?: string }>()
const ctx = inject<SelectCtx>('selectCtx')!
</script>

<template>
  <div
    v-show="ctx.open.value"
    :class="cn(
      'absolute top-full left-0 z-50 w-full min-w-[8rem] mt-1 overflow-hidden rounded-md border border-border bg-popover text-popover-foreground shadow-md',
      props.class,
    )"
  >
    <ul class="p-1 max-h-60 overflow-y-auto">
      <slot />
    </ul>
  </div>
</template>
