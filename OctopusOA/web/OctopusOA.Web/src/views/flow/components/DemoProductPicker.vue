<script setup lang="ts">
import { computed } from 'vue'

// L3 示例自定义控件：商品选择器。约定 modelValue / update:modelValue / readonly。
const props = defineProps<{
  modelValue?: unknown
  readonly?: boolean
}>()
const emit = defineEmits<{ (e: 'update:modelValue', value: unknown): void }>()

const products = [
  { id: 1, name: '面料X', unit: '米' },
  { id: 2, name: '面料Y', unit: '米' },
  { id: 3, name: '纽扣Z', unit: '个' },
]

const current = computed(() => (typeof props.modelValue === 'number' ? props.modelValue : ''))
const selected = computed(() => products.find(p => p.id === props.modelValue))

function onChange(e: Event) {
  const value = (e.target as HTMLSelectElement).value
  emit('update:modelValue', value ? Number(value) : null)
}
</script>

<template>
  <span v-if="readonly" class="flex items-center h-[var(--row-h)] text-[12px] text-foreground">
    {{ selected ? `${selected.name}（${selected.unit}）` : '—' }}
  </span>
  <select
    v-else
    :value="current"
    class="w-full h-[var(--row-h)] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
    @change="onChange"
  >
    <option value="">请选择商品</option>
    <option v-for="p in products" :key="p.id" :value="p.id">{{ p.name }}（{{ p.unit }}）</option>
  </select>
</template>
