<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Search, Plus, ClipboardCheck } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getEmployees, getLeaveBalance, adjustLeaveBalance, getKpis, createKpi, submitKpi, type Employee, type LeaveBalance, type Kpi } from '@/api'

const tab = ref<'leave' | 'kpi'>('leave')
const employees = ref<Employee[]>([])
const selectedEmp = ref<number | null>(null)
const keyword = ref('')
const leaveRows = ref<LeaveBalance[]>([])
const kpiRows = ref<Kpi[]>([])
const loadingLeave = ref(false)
const loadingKpi = ref(false)

const adjustOpen = ref(false)
const adjustForm = ref({ employeeId: 0, leaveType: 'annual', year: new Date().getFullYear(), totalDays: 5 })
const kpiOpen = ref(false)
const kpiForm = ref({ employeeId: 0, period: `${new Date().getFullYear()}-Q1`, title: '', score: 0, comment: '' })
const submitOpen = ref(false)
const submitForm = ref({ id: 0, score: 0, comment: '' })
const message = ref('')

const filteredEmployees = computed(() => {
  const kw = keyword.value.trim().toLowerCase()
  if (!kw) return employees.value
  return employees.value.filter(e => e.fullName.toLowerCase().includes(kw) || e.employeeNo.toLowerCase().includes(kw))
})

async function loadEmployees() { try { employees.value = await getEmployees(); if (!selectedEmp.value && employees.value.length) selectedEmp.value = employees.value[0].employeeId } catch { employees.value = [] } }

async function loadLeave() {
  if (!selectedEmp.value) { leaveRows.value = []; return }
  loadingLeave.value = true
  try { leaveRows.value = await getLeaveBalance(selectedEmp.value) } catch { leaveRows.value = [] } finally { loadingLeave.value = false }
}

async function loadKpi() {
  if (!selectedEmp.value) { kpiRows.value = []; return }
  loadingKpi.value = true
  try { kpiRows.value = await getKpis(selectedEmp.value) } catch { kpiRows.value = [] } finally { loadingKpi.value = false }
}

async function submitAdjust() {
  try { await adjustLeaveBalance(adjustForm.value); adjustOpen.value = false; await loadLeave() }
  catch (e) { message.value = (e as Error).message }
}
async function submitKpiCreate() {
  try { await createKpi({ employeeId: kpiForm.value.employeeId, period: kpiForm.value.period, title: kpiForm.value.title }); kpiOpen.value = false; await loadKpi() }
  catch (e) { message.value = (e as Error).message }
}
async function submitScore() {
  try { await submitKpi(submitForm.value.id, Number(submitForm.value.score), submitForm.value.comment); submitOpen.value = false; await loadKpi() }
  catch (e) { message.value = (e as Error).message }
}
function openSubmit(k: Kpi) { submitForm.value = { id: k.kpiId, score: Number(k.score ?? 0), comment: k.comment ?? '' }; submitOpen.value = true }
const kpiStatus: Record<string, string> = { draft: '草稿', submitted: '已提交', graded: '已评分' }
const leaveType: Record<string, string> = { annual: '年假', sick: '病假', personal: '事假', marriage: '婚假', maternity: '产假' }

onMounted(async () => { await loadEmployees(); await Promise.all([loadLeave(), loadKpi()]) })
</script>

<template>
  <div>
    <PageHeader title="假期与绩效" sub="员工假期余额与 KPI 考核记录">
      <template #tabs>
        <div class="flex gap-1 -mb-px">
          <button :class="['px-4 py-2 text-[12px] border-b-2 cursor-pointer', tab === 'leave' ? 'border-primary text-primary font-medium' : 'border-transparent text-muted-foreground']" @click="tab = 'leave'; loadLeave()">假期余额</button>
          <button :class="['px-4 py-2 text-[12px] border-b-2 cursor-pointer', tab === 'kpi' ? 'border-primary text-primary font-medium' : 'border-transparent text-muted-foreground']" @click="tab = 'kpi'; loadKpi()">KPI 考核</button>
        </div>
      </template>
    </PageHeader>
    <div class="p-5">
      <p v-if="message" class="mb-3 text-[12px] text-primary">{{ message }}</p>
      <div class="flex items-center gap-3 mb-4">
        <input v-model="keyword" placeholder="搜索员工姓名/工号" class="h-8 px-3 border border-border rounded bg-card text-[12px] w-56" />
        <select v-model.number="selectedEmp" class="h-8 px-2 border border-border rounded bg-card text-[12px] min-w-[180px]" @change="tab === 'leave' ? loadLeave() : loadKpi()">
          <option :value="null">选择员工</option>
          <option v-for="e in filteredEmployees" :key="e.employeeId" :value="e.employeeId">{{ e.fullName }}（{{ e.employeeNo }}）</option>
        </select>
        <button v-if="tab === 'leave'" class="ml-auto inline-flex items-center gap-1 h-8 px-3 bg-primary text-primary-foreground rounded text-[12px] border-0 cursor-pointer" @click="adjustForm.employeeId = selectedEmp ?? 0; adjustOpen = true"><Plus :size="13" /> 调整余额</button>
        <button v-else class="ml-auto inline-flex items-center gap-1 h-8 px-3 bg-primary text-primary-foreground rounded text-[12px] border-0 cursor-pointer" @click="kpiForm.employeeId = selectedEmp ?? 0; kpiOpen = true"><Plus :size="13" /> 新建 KPI</button>
      </div>

      <div v-if="!selectedEmp" class="bg-card border border-border rounded-[var(--radius-lg)] p-8 text-center text-[12px] text-muted-foreground">请先选择一名员工</div>

      <div v-else-if="tab === 'leave'" class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">假期类型</th><th class="text-left px-4 py-2.5 font-medium">年度</th>
            <th class="text-right px-4 py-2.5 font-medium">总额(天)</th><th class="text-right px-4 py-2.5 font-medium">已用</th>
            <th class="text-right px-4 py-2.5 font-medium">剩余</th>
          </tr></thead>
          <tbody>
            <tr v-if="loadingLeave"><td colspan="5" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="leaveRows.length === 0"><td colspan="5" class="text-center py-8 text-muted-foreground">暂无假期余额</td></tr>
            <tr v-for="r in leaveRows" :key="r.balanceId" class="border-t border-border">
              <td class="px-4 py-2.5">{{ leaveType[r.leaveType] ?? r.leaveType }}</td><td class="px-4 py-2.5 text-muted-foreground">{{ r.year }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ r.totalDays }}</td><td class="px-4 py-2.5 text-right font-mono-tnum">{{ r.usedDays }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum font-medium">{{ r.remainingDays }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-else class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">考核周期</th><th class="text-left px-4 py-2.5 font-medium">标题</th>
            <th class="text-right px-4 py-2.5 font-medium">分数</th><th class="text-left px-4 py-2.5 font-medium">等级</th>
            <th class="text-left px-4 py-2.5 font-medium">状态</th><th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loadingKpi"><td colspan="6" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="kpiRows.length === 0"><td colspan="6" class="text-center py-8 text-muted-foreground">暂无 KPI 记录</td></tr>
            <tr v-for="k in kpiRows" :key="k.kpiId" class="border-t border-border">
              <td class="px-4 py-2.5 font-mono-tnum">{{ k.period }}</td><td class="px-4 py-2.5">{{ k.title }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ k.score ?? '—' }}</td><td class="px-4 py-2.5">{{ k.grade ?? '—' }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="k.status" :labels="kpiStatus" /></td>
              <td class="px-4 py-2.5 text-right"><button class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="openSubmit(k)"><ClipboardCheck :size="12" /> 评分</button></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="adjustOpen" title="调整假期余额" @close="adjustOpen = false">
      <div class="grid grid-cols-2 gap-3">
        <label class="block text-[12px]">假期类型<select v-model="adjustForm.leaveType" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option v-for="(v, k) in leaveType" :key="k" :value="k">{{ v }}</option></select></label>
        <label class="block text-[12px]">年度<input v-model.number="adjustForm.year" type="number" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      </div>
      <label class="block text-[12px] mt-3">总天数<input v-model.number="adjustForm.totalDays" type="number" min="0" step="0.5" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="adjustOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submitAdjust">保存</button>
      </template>
    </Modal>

    <Modal :open="kpiOpen" title="新建 KPI" @close="kpiOpen = false">
      <div class="space-y-3">
        <label class="block text-[12px]">考核周期<input v-model="kpiForm.period" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" placeholder="如 2026-Q1" /></label>
        <label class="block text-[12px]">标题<input v-model="kpiForm.title" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="kpiOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submitKpiCreate">保存</button>
      </template>
    </Modal>

    <Modal :open="submitOpen" title="提交 KPI 评分" @close="submitOpen = false">
      <div class="space-y-3">
        <label class="block text-[12px]">分数（0-100）<input v-model.number="submitForm.score" type="number" min="0" max="100" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">评语<textarea v-model="submitForm.comment" rows="3" class="mt-1 w-full px-2 py-1.5 border border-border rounded bg-card" /></label>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="submitOpen = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submitScore">提交</button>
      </template>
    </Modal>
  </div>
</template>
