<script setup lang="ts">
import { cn } from '@/lib/utils'
const props = defineProps<{ class?: string }>()
</script>
<template>
  <li role="separator" :class="cn('-mx-1 my-1 h-px bg-muted', props.class)" />
</template>
