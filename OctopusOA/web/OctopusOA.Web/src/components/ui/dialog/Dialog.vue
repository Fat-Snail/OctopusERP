<script setup lang="ts">
import { provide, computed } from 'vue'

const props = defineProps<{ open?: boolean }>()
const emit = defineEmits<{ 'update:open': [val: boolean] }>()

provide('dialogCtx', {
  open: computed(() => props.open ?? false),
  close: () => emit('update:open', false),
})
</script>

<template>
  <slot />
</template>
