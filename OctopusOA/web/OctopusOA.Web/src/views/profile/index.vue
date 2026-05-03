<script setup lang="ts">
import { reactive } from 'vue'
import { ExternalLink } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'
import { toast } from '@/components/ui/toast'
import { useUserStore } from '@/store/modules/user'
import { formatBeijingTime } from '@/utils/datetime'
import { useTheme } from '@/composables/useTheme'

const userStore = useUserStore()
const lastSyncAt = formatBeijingTime(new Date().toISOString())
const { theme, setTheme } = useTheme()

const localSettings = reactive({
  notifyPreference: 'all',
  language: 'zh-CN',
})

function saveLocalSettings() {
  toast('本地设置已保存', 'success')
}

function goToUMC() {
  alert('请前往 OctopusUMC 系统 (http://localhost:5173) 修改个人信息\n\n（正式环境将跳转到 UMC 系统）')
}
</script>

<template>
  <div>
    <PageHeader title="个人中心" />

    <div class="p-5 grid grid-cols-[1fr_320px] gap-4">
      <!-- Left: basic info (from UMC) -->
      <div class="space-y-4">
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <div class="flex items-center justify-between mb-4">
            <h3 class="text-[13px] font-semibold text-foreground">基本信息</h3>
            <Badge variant="warning">由 UMC 统一管理</Badge>
          </div>

          <div class="flex items-center gap-4 mb-5">
            <div class="w-16 h-16 rounded-full bg-primary-soft flex items-center justify-center text-[22px] font-semibold text-primary-soft-fg">
              {{ userStore.userName?.slice(0, 1)?.toUpperCase() }}
            </div>
            <div>
              <h4 class="text-[16px] font-semibold text-foreground">{{ userStore.userName }}</h4>
              <p class="text-[12px] text-muted-foreground mt-0.5">{{ userStore.email }}</p>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-x-6 gap-y-2 text-[12px]">
            <div class="flex gap-2 py-1.5 border-b border-border">
              <span class="text-muted-foreground w-20 shrink-0">用户名</span>
              <span>{{ userStore.userName }}</span>
            </div>
            <div class="flex gap-2 py-1.5 border-b border-border">
              <span class="text-muted-foreground w-20 shrink-0">邮箱</span>
              <span>{{ userStore.email }}</span>
            </div>
            <div class="flex gap-2 py-1.5 border-b border-border">
              <span class="text-muted-foreground w-20 shrink-0">手机号</span>
              <span class="text-muted-foreground">未同步</span>
            </div>
            <div class="flex gap-2 py-1.5 border-b border-border">
              <span class="text-muted-foreground w-20 shrink-0">认证方式</span>
              <span>OctopusUMC SSO</span>
            </div>
            <div class="col-span-2 flex gap-2 py-1.5">
              <span class="text-muted-foreground w-20 shrink-0">UMC 角色</span>
              <div class="flex flex-wrap gap-1">
                <Badge v-for="r in userStore.roles" :key="r" variant="secondary">{{ r }}</Badge>
              </div>
            </div>
          </div>

          <div class="mt-4 p-3 bg-info/10 rounded-[var(--radius)] border border-info/20 text-[12px] text-info">
            以下信息由 OctopusUMC 统一管理，如需修改请前往 UMC 系统
          </div>
          <div class="flex justify-end mt-3">
            <button class="flex items-center gap-1 h-[var(--row-h)] px-4 text-[12px] border border-primary/30 text-primary rounded-[var(--radius)] hover:bg-primary/10 cursor-pointer bg-card" @click="goToUMC">
              <ExternalLink :size="12" />前往 UMC 修改
            </button>
          </div>
        </div>
      </div>

      <!-- Right: local settings + sync info -->
      <div class="space-y-4">
        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <h3 class="text-[13px] font-semibold text-foreground mb-4">OA 本地设置</h3>
          <div class="space-y-3">
            <div>
              <label class="block text-[12px] font-medium mb-1.5">通知接收偏好</label>
              <Select v-model="localSettings.notifyPreference">
                <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">接收所有通知</SelectItem>
                  <SelectItem value="important">仅重要通知</SelectItem>
                  <SelectItem value="none">不接收通知</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1.5">语言设置</label>
              <Select v-model="localSettings.language">
                <SelectTrigger class="w-full"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="zh-CN">简体中文</SelectItem>
                  <SelectItem value="en-US">English</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1.5">主题</label>
              <p class="text-[11px] text-muted-foreground mb-1.5">主题由 OctopusUMC 统一管理，在 UMC 修改后立即生效。</p>
              <div class="flex gap-4 text-[12px]">
                <label class="flex items-center gap-1.5 cursor-pointer"><input type="radio" value="light" :checked="theme === 'light'" class="accent-primary" @change="setTheme('light')" />浅色</label>
                <label class="flex items-center gap-1.5 cursor-pointer"><input type="radio" value="dark" :checked="theme === 'dark'" class="accent-primary" @change="setTheme('dark')" />深色</label>
              </div>
            </div>
            <button class="w-full h-[var(--row-h)] text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 mt-2" @click="saveLocalSettings">保存设置</button>
          </div>
        </div>

        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5">
          <h3 class="text-[13px] font-semibold text-foreground mb-3">同步信息</h3>
          <div class="space-y-2 text-[12px]">
            <div class="flex justify-between py-1.5 border-b border-border">
              <span class="text-muted-foreground">最后同步时间</span>
              <span class="font-mono-tnum">{{ lastSyncAt }}</span>
            </div>
            <div class="flex justify-between py-1.5 border-b border-border items-center">
              <span class="text-muted-foreground">OA 角色</span>
              <div class="flex gap-1">
                <Badge variant="success">oa_user</Badge>
              </div>
            </div>
            <div class="flex justify-between py-1.5 items-center">
              <span class="text-muted-foreground">同步状态</span>
              <Badge variant="success">已同步</Badge>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
