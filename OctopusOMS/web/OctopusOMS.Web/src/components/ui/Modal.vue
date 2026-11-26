<script setup lang="ts">
defineProps<{ open: boolean; title: string; width?: string }>()
const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @click.self="emit('close')">
      <div :class="['bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-full max-h-[85vh] overflow-y-auto', width ?? 'max-w-[520px]']">
        <div class="flex items-center justify-between px-5 py-3.5 border-b border-border sticky top-0 bg-card z-10">
          <h3 class="text-[14px] font-semibold text-foreground">{{ title }}</h3>
          <button class="text-muted-foreground hover:text-foreground text-[20px] leading-none border-0 bg-transparent cursor-pointer" @click="emit('close')">×</button>
        </div>
        <div class="px-5 py-4">
          <slot />
        </div>
        <div v-if="$slots.footer" class="flex justify-end gap-2 px-5 py-3 border-t border-border sticky bottom-0 bg-card">
          <slot name="footer" />
        </div>
      </div>
    </div>
  </Teleport>
</template>
