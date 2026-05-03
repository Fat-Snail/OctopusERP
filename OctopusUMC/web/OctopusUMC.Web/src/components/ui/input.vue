<script setup lang="ts">
import type { InputHTMLAttributes } from 'vue'
import { cn } from '@/lib/utils'

interface Props extends /* @vue-ignore */ InputHTMLAttributes {
  class?: string
  modelValue?: string | number
}

const props = defineProps<Props>()
const emit = defineEmits<{ 'update:modelValue': [v: string] }>()
</script>

<template>
  <input
    :class="cn(
      'flex w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50',
      props.class
    )"
    :value="modelValue"
    v-bind="$attrs"
    @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
  />
</template>
