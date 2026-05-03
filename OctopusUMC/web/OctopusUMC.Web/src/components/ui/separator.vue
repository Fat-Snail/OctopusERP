<script setup lang="ts">
import { cn } from '@/lib/utils'

const props = withDefaults(defineProps<{ orientation?: 'horizontal' | 'vertical'; class?: string }>(), {
  orientation: 'horizontal',
})
</script>

<template>
  <div
    :class="cn(
      'bg-border shrink-0',
      props.orientation === 'horizontal' ? 'h-px w-full' : 'h-full w-px',
      props.class
    )"
  />
</template>
