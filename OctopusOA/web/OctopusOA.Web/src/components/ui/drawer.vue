<script setup lang="ts">
import { X } from 'lucide-vue-next'
import { watch } from 'vue'

const props = defineProps<{
  modelValue: boolean
  title?: string
  width?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [v: boolean]
}>()

function close() { emit('update:modelValue', false) }

watch(() => props.modelValue, (v) => {
  document.body.style.overflow = v ? 'hidden' : ''
})
</script>

<template>
  <Teleport to="body">
    <Transition name="drawer">
      <div v-if="modelValue" class="fixed inset-0 z-50 flex justify-end">
        <!-- Backdrop -->
        <div class="absolute inset-0 bg-black/40" @click="close" />
        <!-- Panel -->
        <div
          :style="{ width: width || '560px' }"
          class="relative z-10 bg-card h-full flex flex-col shadow-xl"
        >
          <!-- Header -->
          <div class="flex items-center justify-between px-5 py-4 border-b border-border shrink-0">
            <span class="text-[14px] font-semibold text-foreground">{{ title }}</span>
            <button
              class="w-7 h-7 flex items-center justify-center text-muted-foreground hover:text-foreground hover:bg-muted rounded transition-colors cursor-pointer border-0 bg-transparent"
              @click="close"
            >
              <X :size="16" />
            </button>
          </div>
          <!-- Content -->
          <div class="flex-1 overflow-y-auto p-5">
            <slot />
          </div>
          <!-- Footer slot (optional) -->
          <div v-if="$slots.footer" class="border-t border-border px-5 py-4 shrink-0 flex justify-end gap-2">
            <slot name="footer" />
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.drawer-enter-active, .drawer-leave-active {
  transition: opacity 0.2s ease;
}
.drawer-enter-active .relative, .drawer-leave-active .relative {
  transition: transform 0.25s ease;
}
.drawer-enter-from { opacity: 0; }
.drawer-enter-from .relative { transform: translateX(100%); }
.drawer-leave-to { opacity: 0; }
.drawer-leave-to .relative { transform: translateX(100%); }
</style>
