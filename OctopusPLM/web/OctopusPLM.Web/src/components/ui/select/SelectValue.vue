<script setup lang="ts">
import { inject, computed, type Ref, type ComputedRef } from 'vue'

interface SelectCtx {
  open: Ref<boolean>
  modelValue: ComputedRef<string>
  getLabel(v: string): string
}

const props = defineProps<{ placeholder?: string }>()
const ctx = inject<SelectCtx>('selectCtx')!

const display = computed(() => {
  const val = ctx.modelValue.value
  if (!val) return props.placeholder ?? ''
  return ctx.getLabel(val) || val
})
const isEmpty = computed(() => !ctx.modelValue.value)
</script>

<template>
  <span :class="isEmpty ? 'text-muted-foreground' : ''">{{ display }}</span>
</template>
