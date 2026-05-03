<script setup lang="ts">
import { computed } from 'vue'
import { Building2, Phone, Mail } from 'lucide-vue-next'
import type { ContactUser } from '@/api/contact/types'

const props = defineProps<{ user: ContactUser }>()
defineEmits<{ (e: 'click'): void }>()

const primaryDept = computed(() => props.user.depts.find(d => d.isPrimary) || props.user.depts[0])
</script>

<template>
  <button
    class="w-full text-left flex gap-3 p-3 bg-card border border-border rounded-[var(--radius-lg)] cursor-pointer hover:border-primary/50 hover:shadow-sm transition-all"
    @click="$emit('click')"
  >
    <div class="w-10 h-10 rounded-full bg-primary-soft flex items-center justify-center text-[14px] font-semibold text-primary-soft-fg shrink-0">
      {{ user.nickName.slice(0, 1) }}
    </div>
    <div class="flex-1 min-w-0">
      <div class="flex items-center gap-1.5 mb-0.5">
        <span class="text-[13px] font-semibold text-foreground">{{ user.nickName }}</span>
        <span class="text-[11px] text-muted-foreground">@{{ user.userName }}</span>
      </div>
      <div v-if="primaryDept" class="flex items-center gap-1 text-[11px] text-muted-foreground mb-0.5">
        <Building2 :size="10" />
        <span>{{ primaryDept.deptName }}</span>
        <span v-if="primaryDept.postName" class="text-muted-foreground/60"> · {{ primaryDept.postName }}</span>
      </div>
      <div class="flex flex-wrap gap-3 text-[11px] text-muted-foreground">
        <span v-if="user.phoneNumber" class="flex items-center gap-1"><Phone :size="10" />{{ user.phoneNumber }}</span>
        <span v-if="user.email" class="flex items-center gap-1"><Mail :size="10" />{{ user.email }}</span>
      </div>
    </div>
  </button>
</template>
