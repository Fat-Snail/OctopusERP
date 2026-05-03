<script setup lang="ts">
import type { ButtonHTMLAttributes } from 'vue'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const buttonVariants = cva(
  'inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-[var(--radius)] text-[12px] font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 cursor-pointer select-none',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:brightness-105',
        outline: 'border border-border-strong bg-card text-foreground hover:bg-muted',
        ghost: 'text-foreground hover:bg-muted',
        destructive: 'bg-destructive text-destructive-foreground hover:brightness-105',
        secondary: 'bg-secondary text-secondary-foreground hover:brightness-95',
        link: 'text-primary underline-offset-4 hover:underline p-0 h-auto',
      },
      size: {
        default: 'h-[var(--row-h)] px-3',
        sm: 'h-6 px-2 text-[11px] rounded-[var(--radius-sm)]',
        lg: 'h-9 px-4 text-[13px]',
        icon: 'h-[var(--row-h)] w-[var(--row-h)] p-0',
        'icon-sm': 'h-6 w-6 p-0',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  }
)

type ButtonVariants = VariantProps<typeof buttonVariants>

interface Props extends /* @vue-ignore */ ButtonHTMLAttributes {
  variant?: ButtonVariants['variant']
  size?: ButtonVariants['size']
  class?: string
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'default',
  size: 'default',
})
</script>

<template>
  <button
    :class="cn(buttonVariants({ variant, size }), props.class)"
    v-bind="$attrs"
  >
    <slot />
  </button>
</template>
