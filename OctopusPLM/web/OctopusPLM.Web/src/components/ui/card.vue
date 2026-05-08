<script setup lang="ts">
import { cn } from '@/lib/utils'

defineProps<{ class?: string }>()
</script>

<template>
  <div :class="cn('bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden', $props.class)">
    <slot />
  </div>
</template>
