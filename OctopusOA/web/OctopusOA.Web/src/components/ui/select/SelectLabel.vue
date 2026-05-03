<script setup lang="ts">
import { cn } from '@/lib/utils'
const props = defineProps<{ class?: string }>()
</script>
<template>
  <li :class="cn('py-1.5 pl-8 pr-2 text-sm font-semibold text-muted-foreground', props.class)"><slot /></li>
</template>
