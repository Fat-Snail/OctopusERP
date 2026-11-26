<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Lock, Unlock } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import { getPeriods, closePeriod, reopenPeriod, type Period } from '@/api'

const rows = ref<Period[]>([])
const loading = ref(false)
const message = ref('')
const statusLabels: Record<string, string> = { open: '已开启', closed: '已关账' }

async function load() {
  loading.value = true
  try { rows.value = await getPeriods() } catch { rows.value = [] } finally { loading.value = false }
}

async function doClose(p: Period) {
  if (!confirm(`确认对 ${p.periodCode} 关账？关账期间不能再录入凭证。`)) return
  try { await closePeriod(p.periodId); message.value = `${p.periodCode} 已关账`; await load() }
  catch (e) { message.value = (e as Error).message }
}

async function doReopen(p: Period) {
  if (!confirm(`确认重新打开 ${p.periodCode}？`)) return
  try { await reopenPeriod(p.periodId); message.value = `${p.periodCode} 已重新打开`; await load() }
  catch (e) { message.value = (e as Error).message }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="会计期间" sub="月度期间的开启、关账与反结账" />
    <div class="p-5">
      <p v-if="message" class="mb-4 text-[12px] text-primary">{{ message }}</p>
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">期间</th><th class="text-left px-4 py-2.5 font-medium">年</th>
            <th class="text-left px-4 py-2.5 font-medium">月</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-left px-4 py-2.5 font-medium">关账时间</th><th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="6" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="6" class="text-center py-8 text-muted-foreground">暂无期间</td></tr>
            <tr v-for="r in rows" :key="r.periodId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum font-medium">{{ r.periodCode }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.year }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.month }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.closedAt?.slice(0, 16) ?? '—' }}</td>
              <td class="px-4 py-2.5 text-right">
                <button v-if="r.status === 'open'" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="doClose(r)"><Lock :size="12" /> 关账</button>
                <button v-else class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="doReopen(r)"><Unlock :size="12" /> 反结账</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>
