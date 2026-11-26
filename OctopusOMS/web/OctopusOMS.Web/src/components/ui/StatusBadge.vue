<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ value: string; labels?: Record<string, string> }>()

const tone = computed(() => {
  const v = props.value?.toLowerCase() ?? ''
  if (['active', 'open', 'posted', 'approved', 'paid', 'completed', 'shipped', 'normal', 'live'].includes(v)) return 'success'
  if (['draft', 'pending', 'created', 'processing', 'in_progress', 'simulated', 'sandbox'].includes(v)) return 'warning'
  if (['void', 'cancelled', 'rejected', 'inactive', 'closed', 'resigned', 'refunded'].includes(v)) return 'muted'
  return 'info'
})

const toneClass = computed(() => ({
  success: 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]',
  warning: 'bg-[color-mix(in_oklch,var(--warning)_16%,transparent)] text-[var(--warning)]',
  muted: 'bg-muted text-muted-foreground',
  info: 'bg-primary-soft text-[var(--primary-soft-fg)]',
}[tone.value]))

const label = computed(() => props.labels?.[props.value] ?? props.value)
</script>

<template>
  <span :class="['inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium whitespace-nowrap', toneClass]">{{ label }}</span>
</template>
