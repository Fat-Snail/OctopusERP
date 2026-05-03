<script setup lang="ts">
import { inject, type Ref, type ComputedRef } from 'vue'
import { ChevronDown } from 'lucide-vue-next'
import { cn } from '@/lib/utils'

interface SelectCtx {
  open: Ref<boolean>
  modelValue: ComputedRef<string>
  select(v: string): void
  registerOption(v: string, l: string): void
  unregisterOption(v: string): void
  getLabel(v: string): string
}

const props = defineProps<{ class?: string; disabled?: boolean }>()
const ctx = inject<SelectCtx>('selectCtx')!
</script>

<template>
  <button
    type="button"
    :disabled="props.disabled"
    :class="cn(
      'flex h-10 w-full items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 text-start',
      props.class,
    )"
    @click.stop="ctx.open.value = !ctx.open.value"
  >
    <span class="flex-1 truncate"><slot /></span>
    <ChevronDown class="w-4 h-4 opacity-50 shrink-0 ml-2" />
  </button>
</template>
