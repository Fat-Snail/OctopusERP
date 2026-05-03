<script setup lang="ts">
import { provide, computed, ref } from 'vue'
import { onClickOutside } from '@vueuse/core'

const props = defineProps<{ modelValue?: string | number; defaultValue?: string | number }>()
const emit = defineEmits<{ 'update:modelValue': [val: string] }>()

const open = ref(false)
const rootRef = ref<HTMLElement>()
// value → display label map populated by SelectItem on mount
const optionLabels = new Map<string, string>()

provide('selectCtx', {
  open,
  modelValue: computed(() => String(props.modelValue ?? props.defaultValue ?? '')),
  select(val: string) { emit('update:modelValue', val); open.value = false },
  registerOption(val: string, label: string) { optionLabels.set(val, label) },
  unregisterOption(val: string) { optionLabels.delete(val) },
  getLabel(val: string) { return optionLabels.get(val) ?? '' },
})

onClickOutside(rootRef, () => { open.value = false })
</script>

<template>
  <div ref="rootRef" class="relative">
    <slot />
  </div>
</template>
