<script setup lang="ts">
import Badge from '@/components/ui/badge.vue'
import { toast } from '@/components/ui/toast'
import type { ContactUser } from '@/api/contact/types'

defineProps<{ user: ContactUser | null }>()

function copy(text: string) {
  navigator.clipboard.writeText(text)
  toast('已复制', 'success')
}
</script>

<template>
  <div v-if="user" class="p-2">
    <div class="flex items-center gap-4 mb-5">
      <div class="w-16 h-16 rounded-full bg-primary-soft flex items-center justify-center text-[22px] font-semibold text-primary-soft-fg">
        {{ user.nickName.slice(0, 1) }}
      </div>
      <div>
        <h3 class="text-[18px] font-semibold text-foreground">{{ user.nickName }}</h3>
        <p class="text-[12px] text-muted-foreground mt-0.5">@{{ user.userName }}</p>
      </div>
    </div>

    <table class="w-full text-[12px] border border-border rounded-[var(--radius)] overflow-hidden mb-5">
      <tbody class="divide-y divide-border">
        <tr class="hover:bg-subtle">
          <td class="px-3 py-2 font-medium text-muted-foreground bg-subtle w-28">手机号</td>
          <td class="px-3 py-2">
            <span v-if="user.phoneNumber" class="flex items-center gap-2">
              <a :href="`tel:${user.phoneNumber}`" class="text-primary hover:underline">{{ user.phoneNumber }}</a>
              <button class="text-[11px] text-muted-foreground hover:text-primary cursor-pointer border-0 bg-transparent" @click="copy(user.phoneNumber!)">复制</button>
            </span>
            <span v-else class="text-muted-foreground">-</span>
          </td>
        </tr>
        <tr class="hover:bg-subtle">
          <td class="px-3 py-2 font-medium text-muted-foreground bg-subtle">邮箱</td>
          <td class="px-3 py-2">
            <span v-if="user.email" class="flex items-center gap-2">
              <a :href="`mailto:${user.email}`" class="text-primary hover:underline">{{ user.email }}</a>
              <button class="text-[11px] text-muted-foreground hover:text-primary cursor-pointer border-0 bg-transparent" @click="copy(user.email)">复制</button>
            </span>
            <span v-else class="text-muted-foreground">-</span>
          </td>
        </tr>
        <tr class="hover:bg-subtle">
          <td class="px-3 py-2 font-medium text-muted-foreground bg-subtle">状态</td>
          <td class="px-3 py-2">
            <Badge :variant="user.status === 1 ? 'success' : 'danger'">{{ user.status === 1 ? '在职' : '停用' }}</Badge>
          </td>
        </tr>
      </tbody>
    </table>

    <h4 class="text-[12px] font-semibold text-foreground mb-2">所在部门</h4>
    <div class="border border-border rounded-[var(--radius)] overflow-hidden">
      <table class="w-full text-[12px]">
        <thead class="bg-subtle">
          <tr>
            <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">部门</th>
            <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-24">职位</th>
            <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-16">类型</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-border">
          <tr v-for="d in user.depts" :key="d.deptId" class="hover:bg-subtle">
            <td class="px-3 py-1.5">{{ d.deptName }}</td>
            <td class="px-3 py-1.5 text-muted-foreground">{{ d.postName || '-' }}</td>
            <td class="px-3 py-1.5">
              <Badge :variant="d.isPrimary ? 'success' : 'secondary'">{{ d.isPrimary ? '主' : '兼' }}</Badge>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
