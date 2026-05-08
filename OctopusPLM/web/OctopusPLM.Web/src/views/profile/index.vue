<script setup lang="ts">
import { ExternalLink } from 'lucide-vue-next'
import BadgeVue from '@/components/ui/badge.vue'
import { usePlmUserStore } from '@/store/modules/plmUser'

const userStore = usePlmUserStore()

function goToUMC() {
  window.open('http://localhost:5173', '_blank')
}
</script>

<template>
  <div class="max-w-2xl">
    <!-- Basic info card -->
    <div class="bg-card border border-border rounded-[var(--radius)] p-6 mb-4">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-[13px] font-semibold text-foreground">基本信息</h3>
        <BadgeVue variant="warning">由 UMC 统一管理</BadgeVue>
      </div>

      <div class="flex items-center gap-4 mb-5">
        <div class="w-16 h-16 rounded-full bg-primary-soft flex items-center justify-center text-[22px] font-semibold text-primary-soft-fg">
          {{ (userStore.userName || userStore.email || '?').slice(0, 1).toUpperCase() }}
        </div>
        <div>
          <h4 class="text-[16px] font-semibold text-foreground">{{ userStore.userName || '未知用户' }}</h4>
          <p class="text-[12px] text-muted-foreground mt-0.5">{{ userStore.email }}</p>
        </div>
      </div>

      <div class="grid grid-cols-2 gap-x-6 gap-y-0 text-[12px]">
        <div class="flex gap-2 py-1.5 border-b border-border">
          <span class="text-muted-foreground w-20 shrink-0">用户名</span>
          <span>{{ userStore.userName || '-' }}</span>
        </div>
        <div class="flex gap-2 py-1.5 border-b border-border">
          <span class="text-muted-foreground w-20 shrink-0">邮箱</span>
          <span>{{ userStore.email || '-' }}</span>
        </div>
        <div class="flex gap-2 py-1.5 border-b border-border">
          <span class="text-muted-foreground w-20 shrink-0">认证方式</span>
          <span>OctopusUMC SSO</span>
        </div>
        <div class="flex gap-2 py-1.5 border-b border-border">
          <span class="text-muted-foreground w-20 shrink-0">系统</span>
          <span>PLM 商品管理</span>
        </div>
        <div class="col-span-2 flex gap-2 py-1.5">
          <span class="text-muted-foreground w-20 shrink-0">UMC 角色</span>
          <div class="flex flex-wrap gap-1">
            <BadgeVue v-for="r in userStore.roles" :key="r" variant="secondary">{{ r }}</BadgeVue>
            <span v-if="!userStore.roles.length" class="text-muted-foreground">无</span>
          </div>
        </div>
      </div>

      <div class="mt-4 p-3 bg-[color-mix(in_oklch,var(--info)_12%,transparent)] rounded-[var(--radius)] border border-[color-mix(in_oklch,var(--info)_25%,transparent)] text-[12px] text-[var(--info)]">
        个人信息由 OctopusUMC 统一管理，如需修改请前往 UMC 系统
      </div>
      <div class="flex justify-end mt-3">
        <button
          class="flex items-center gap-1 h-[var(--row-h)] px-4 text-[12px] border border-primary/30 text-primary rounded-[var(--radius)] hover:bg-primary/10 cursor-pointer bg-card"
          @click="goToUMC"
        >
          <ExternalLink :size="12" />前往 UMC 修改
        </button>
      </div>
    </div>
  </div>
</template>
