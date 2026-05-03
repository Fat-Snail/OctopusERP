<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { toast } from '@/components/ui/toast'
import { getRule, updateRule } from '@/api/attendance'
import type { AttendanceRule } from '@/api/attendance/types'

const rule = ref<AttendanceRule | null>(null)
const loading = ref(false)
const submitting = ref(false)

async function load() {
  loading.value = true
  try { rule.value = await getRule() } finally { loading.value = false }
}

async function handleSave() {
  if (!rule.value) return
  submitting.value = true
  try { await updateRule(rule.value); toast('保存成功', 'success'); await load() }
  catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="考勤规则" sub="配置默认打卡规则和阈值" />

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="rule" class="max-w-[560px] bg-card border border-border rounded-[var(--radius-lg)] p-5 space-y-4">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1.5">规则名称</label>
          <input v-model="rule.name" type="text" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
        </div>
        <div class="grid grid-cols-2 gap-4">
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">上班时间</label>
            <input v-model="rule.workStartTime" type="time" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">下班时间</label>
            <input v-model="rule.workEndTime" type="time" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">迟到阈值（分钟）</label>
            <input v-model.number="rule.lateThresholdMin" type="number" min="0" max="120" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
            <p class="text-[11px] text-muted-foreground mt-0.5">超过上班时间 {{ rule.lateThresholdMin }} 分钟记为迟到</p>
          </div>
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">早退阈值（分钟）</label>
            <input v-model.number="rule.earlyLeaveThresholdMin" type="number" min="0" max="120" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
            <p class="text-[11px] text-muted-foreground mt-0.5">早于下班时间 {{ rule.earlyLeaveThresholdMin }} 分钟记为早退</p>
          </div>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1.5">IP 白名单</label>
          <textarea v-model="rule.ipWhiteList!" rows="2" placeholder="逗号分隔，留空则不限制" class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring" />
        </div>
        <div class="flex gap-2 pt-2">
          <button :disabled="submitting" class="h-[var(--row-h)] px-5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleSave">
            {{ submitting ? '保存中...' : '保存' }}
          </button>
          <button class="h-[var(--row-h)] px-4 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer" @click="load">重置</button>
        </div>
      </div>
    </div>
  </div>
</template>
