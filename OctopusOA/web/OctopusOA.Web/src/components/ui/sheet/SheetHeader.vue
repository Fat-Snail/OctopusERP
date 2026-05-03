<script setup lang="ts">
import type { HTMLAttributes } from 'vue'
import { cn } from '@/lib/utils'

const props = defineProps<{ class?: HTMLAttributes['class'] }>()
</script>

<template>
  <div :class="cn('flex flex-col gap-y-1 pb-3 mb-4 border-b border-border', props.class)">
    <slot />
  </div>
</template>
