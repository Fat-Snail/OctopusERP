<script setup lang="ts">
import { inject, onMounted, onUnmounted, ref, computed, type Ref, type ComputedRef } from 'vue'
import { Check } from 'lucide-vue-next'
import { cn } from '@/lib/utils'

interface SelectCtx {
  open: Ref<boolean>
  modelValue: ComputedRef<string>
  select(v: string): void
  registerOption(v: string, l: string): void
  unregisterOption(v: string): void
}

const props = defineProps<{ value: string; disabled?: boolean; class?: string }>()
const ctx = inject<SelectCtx>('selectCtx')!
const labelRef = ref<HTMLSpanElement>()
const isSelected = computed(() => ctx.modelValue.value === props.value)

onMounted(() => {
  if (labelRef.value) ctx.registerOption(props.value, labelRef.value.textContent ?? '')
})
onUnmounted(() => ctx.unregisterOption(props.value))

function handleClick() {
  if (!props.disabled) ctx.select(props.value)
}
</script>

<template>
  <li
    :class="cn(
      'relative flex w-full cursor-pointer select-none items-center rounded-sm py-1.5 pl-8 pr-2 text-sm outline-none hover:bg-accent hover:text-accent-foreground',
      props.disabled && 'pointer-events-none opacity-50',
      props.class,
    )"
    @click="handleClick"
  >
    <span class="absolute left-2 flex h-3.5 w-3.5 items-center justify-center">
      <Check v-if="isSelected" class="h-4 w-4" />
    </span>
    <span ref="labelRef"><slot /></span>
  </li>
</template>
