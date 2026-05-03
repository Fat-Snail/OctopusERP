<script setup lang="ts">
defineProps<{
  title?: string
  sub?: string
  noBorder?: boolean
}>()
</script>

<template>
  <div :class="['px-5 pt-4 pb-0 bg-card shrink-0', !noBorder && 'border-b border-border']">
    <div class="flex items-start justify-between gap-4 mb-3">
      <div class="min-w-0">
        <h1 class="text-[18px] font-semibold tracking-[-0.01em] text-foreground leading-tight">
          {{ title }}
        </h1>
        <p v-if="sub" class="text-[12px] text-muted-foreground mt-0.5">{{ sub }}</p>
      </div>
      <div class="flex items-center gap-2 shrink-0 pt-0.5">
        <slot name="actions" />
      </div>
    </div>
    <!-- Optional tabs slot -->
    <slot name="tabs" />
  </div>
</template>
