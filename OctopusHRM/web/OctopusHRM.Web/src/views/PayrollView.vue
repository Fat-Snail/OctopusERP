<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Play, Calculator, BadgeCheck, Eye } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getPayrollRuns, getPayrollRun, startPayroll, calculatePayroll, approvePayroll, type PayrollRun, type PayrollRunDetail } from '@/api'

const rows = ref<PayrollRun[]>([])
const loading = ref(false)
const detail = ref<PayrollRunDetail | null>(null)
const detailOpen = ref(false)
const startOpen = ref(false)
const now = new Date()
const startForm = ref({ year: now.getFullYear(), month: now.getMonth() })
const message = ref('')

const statusLabels: Record<string, string> = { draft: '草稿', calculating: '计算中', calculated: '已计算', approved: '已发薪' }

async function load() { loading.value = true; try { rows.value = await getPayrollRuns() } catch { rows.value = [] } finally { loading.value = false } }

async function openDetail(id: number) { try { detail.value = await getPayrollRun(id); detailOpen.value = true } catch { /* ignore */ } }

async function doStart() {
  try { await startPayroll(startForm.value.year, startForm.value.month + 1); startOpen.value = false; message.value = '算薪批次已启动'; await load() }
  catch (e) { message.value = (e as Error).message }
}
async function doCalculate(r: PayrollRun) { try { await calculatePayroll(r.payrollRunId); await load() } catch (e) { message.value = (e as Error).message } }
async function doApprove(r: PayrollRun) {
  if (!confirm(`确认核准 ${r.periodCode} 发薪？将生成财务凭证。`)) return
  try { await approvePayroll(r.payrollRunId); await load() } catch (e) { message.value = (e as Error).message }
}
function fmt(v: number) { return Number(v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2 }) }
onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="薪资核算" sub="月度算薪批次的计算、核准与发薪">
      <template #actions>
        <button class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium border-0 cursor-pointer" @click="startOpen = true"><Play :size="14" /> 启动算薪</button>
      </template>
    </PageHeader>
    <div class="p-5">
      <p v-if="message" class="mb-4 text-[12px] text-primary">{{ message }}</p>
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">期间</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-right px-4 py-2.5 font-medium">人数</th><th class="text-right px-4 py-2.5 font-medium">应发合计</th>
            <th class="text-right px-4 py-2.5 font-medium">实发合计</th><th class="text-left px-4 py-2.5 font-medium">发薪时间</th>
            <th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="7" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="7" class="text-center py-8 text-muted-foreground">暂无算薪批次</td></tr>
            <tr v-for="r in rows" :key="r.payrollRunId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum font-medium">{{ r.periodCode }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ r.employeeCount }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.totalGrossPay) }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.totalNetPay) }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.paidAt?.slice(0, 16) ?? '—' }}</td>
              <td class="px-4 py-2.5">
                <div class="flex items-center justify-end gap-1">
                  <button title="查看明细" class="p-1 rounded text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer border-0 bg-transparent" @click="openDetail(r.payrollRunId)"><Eye :size="14" /></button>
                  <button v-if="r.status === 'draft'" title="计算薪资" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="doCalculate(r)"><Calculator :size="12" /> 计算</button>
                  <button v-if="r.status === 'calculated'" title="核准发薪" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] bg-primary text-primary-foreground rounded border-0 cursor-pointer" @click="doApprove(r)"><BadgeCheck :size="12" /> 核准发薪</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="startOpen" title="启动算薪" @close="startOpen = false">
      <div class="grid grid-cols-2 gap-3">
        <label class="block text-[12px]">年份<input v-model.number="startForm.year" type="number" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">月份<select v-model.number="startForm.month" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option v-for="m in 12" :key="m" :value="m - 1">{{ m }} 月</option></select></label>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="startOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="doStart">启动</button>
      </template>
    </Modal>

    <Modal :open="detailOpen" :title="`薪资明细 ${detail?.periodCode ?? ''}`" width="max-w-[760px]" @close="detailOpen = false">
      <div v-if="detail" class="space-y-3">
        <div class="grid grid-cols-3 gap-3 text-[12px]">
          <div><span class="text-muted-foreground">状态：</span><StatusBadge :value="detail.status" :labels="statusLabels" /></div>
          <div><span class="text-muted-foreground">人数：</span>{{ detail.employeeCount }}</div>
          <div><span class="text-muted-foreground">实发：</span>¥ {{ fmt(detail.totalNetPay) }}</div>
        </div>
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr><th class="text-left px-2 py-1.5">工号</th><th class="text-left px-2 py-1.5">姓名</th><th class="text-right px-2 py-1.5">出勤</th><th class="text-right px-2 py-1.5">请假</th><th class="text-right px-2 py-1.5">应发</th><th class="text-right px-2 py-1.5">扣除</th><th class="text-right px-2 py-1.5">实发</th></tr></thead>
          <tbody>
            <tr v-for="d in detail.details ?? []" :key="d.detailId" class="border-t border-border">
              <td class="px-2 py-1.5 font-mono-tnum">{{ d.employeeNo }}</td><td class="px-2 py-1.5">{{ d.employeeName }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum">{{ d.attendanceDays }}</td><td class="px-2 py-1.5 text-right font-mono-tnum">{{ d.leaveDays }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum">{{ fmt(d.grossPay) }}</td><td class="px-2 py-1.5 text-right font-mono-tnum">{{ fmt(d.deductions) }}</td>
              <td class="px-2 py-1.5 text-right font-mono-tnum font-medium">{{ fmt(d.netPay) }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="!detail.details?.length" class="text-[12px] text-muted-foreground text-center py-4">暂无明细，请先执行计算。</p>
      </div>
    </Modal>
  </div>
</template>
