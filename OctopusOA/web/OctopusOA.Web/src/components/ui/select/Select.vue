<script setup lang="ts">
import { provide, computed, ref, reactive } from 'vue'
import { onClickOutside } from '@vueuse/core'

const props = defineProps<{ modelValue?: string | number; defaultValue?: string | number }>()
const emit = defineEmits<{ 'update:modelValue': [val: string] }>()

const open = ref(false)
const rootRef = ref<HTMLElement>()
// Reactive record so SelectValue.display recomputes when items register on mount
const optionLabels = reactive<Record<string, string>>({})

provide('selectCtx', {
  open,
  modelValue: computed(() => String(props.modelValue ?? props.defaultValue ?? '')),
  select(val: string) { emit('update:modelValue', val); open.value = false },
  registerOption(val: string, label: string) { optionLabels[val] = label },
  getLabel(val: string) { return optionLabels[val] ?? '' },
})

onClickOutside(rootRef, () => { open.value = false })
</script>

<template>
  <div ref="rootRef" class="relative">
    <slot />
  </div>
</template>
