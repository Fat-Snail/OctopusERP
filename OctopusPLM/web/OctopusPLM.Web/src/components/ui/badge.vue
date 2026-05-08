<script setup lang="ts">
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const badgeVariants = cva(
  'inline-flex items-center gap-1 rounded-full text-[10.5px] font-medium border whitespace-nowrap',
  {
    variants: {
      variant: {
        default: 'bg-primary-soft text-primary-soft-fg border-transparent px-2 py-0.5',
        secondary: 'bg-secondary text-secondary-foreground border-transparent px-2 py-0.5',
        outline: 'border-border text-foreground bg-transparent px-2 py-0.5',
        success: 'bg-success/15 text-success border-transparent px-2 py-0.5',
        warning: 'bg-warning/20 text-warning border-transparent px-2 py-0.5',
        danger: 'bg-danger/15 text-danger border-transparent px-2 py-0.5',
        info: 'bg-info/15 text-info border-transparent px-2 py-0.5',
        dot: 'w-1.5 h-1.5 p-0 rounded-full border-0',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
)

type BadgeVariants = VariantProps<typeof badgeVariants>

interface Props {
  variant?: BadgeVariants['variant']
  class?: string
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'default',
})
</script>

<template>
  <span :class="cn(badgeVariants({ variant }), props.class)">
    <slot />
  </span>
</template>
