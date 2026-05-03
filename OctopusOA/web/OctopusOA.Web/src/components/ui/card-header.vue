<script setup lang="ts">
import { cn } from '@/lib/utils'

defineProps<{ class?: string }>()
</script>

<template>
  <div :class="cn('flex items-center justify-between gap-2 px-4 py-3 border-b border-border', $props.class)">
    <slot />
  </div>
</template>
