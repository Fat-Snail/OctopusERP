<script setup lang="ts">
import { inject, type ComputedRef } from 'vue'
interface DialogCtx { open: ComputedRef<boolean>; close: () => void }
const ctx = inject<DialogCtx>('dialogCtx')
const props = defineProps<{ class?: string }>()
</script>

<template>
  <button type="button" :class="props.class" @click="ctx?.close()">
    <slot />
  </button>
</template>
