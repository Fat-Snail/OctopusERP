<script setup lang="ts">
import { cn } from '@/lib/utils'

interface Props {
  src?: string
  alt?: string
  fallback?: string
  size?: 'sm' | 'md' | 'lg'
  class?: string
}

const props = withDefaults(defineProps<Props>(), {
  size: 'md',
})

const sizeClass = {
  sm: 'w-6 h-6 text-[10.5px]',
  md: 'w-8 h-8 text-[12px]',
  lg: 'w-10 h-10 text-[14px]',
}
</script>

<template>
  <div
    :class="cn(
      'inline-flex items-center justify-center rounded-full bg-muted text-muted-foreground font-semibold border border-border shrink-0 select-none overflow-hidden',
      sizeClass[size],
      props.class
    )"
  >
    <img v-if="src" :src="src" :alt="alt" class="w-full h-full object-cover" />
    <span v-else>{{ fallback }}</span>
  </div>
</template>
