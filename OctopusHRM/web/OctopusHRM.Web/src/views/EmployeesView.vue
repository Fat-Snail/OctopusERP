<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus, Search, UserMinus } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getEmployees, createEmployee, resignEmployee, type Employee } from '@/api'

const rows = ref<Employee[]>([])
const loading = ref(false)
const keyword = ref('')
const statusFilter = ref('')
const open = ref(false)
const error = ref('')
const form = ref({ employeeNo: '', fullName: '', department: '', position: '', employmentType: 'full_time', phone: '', email: '', baseSalary: 0, hiredAt: '' })

const statusLabels: Record<string, string> = { active: '在职', probation: '试用', resigned: '已离职' }
const typeLabels: Record<string, string> = { full_time: '全职', part_time: '兼职', intern: '实习', contractor: '外包' }

async function load() {
  loading.value = true
  try { rows.value = await getEmployees({ keyword: keyword.value || undefined, status: statusFilter.value || undefined }) }
  catch { rows.value = [] } finally { loading.value = false }
}

async function submit() {
  error.value = ''
  if (!form.value.employeeNo || !form.value.fullName) { error.value = '请填写工号和姓名'; return }
  try {
    await createEmployee({
      employeeNo: form.value.employeeNo,
      fullName: form.value.fullName,
      department: form.value.department || null,
      position: form.value.position || null,
      employmentType: form.value.employmentType,
      phone: form.value.phone || null,
      email: form.value.email || null,
      baseSalary: Number(form.value.baseSalary) || null,
      hiredAt: form.value.hiredAt ? new Date(form.value.hiredAt).toISOString() : null,
    })
    open.value = false
    form.value = { employeeNo: '', fullName: '', department: '', position: '', employmentType: 'full_time', phone: '', email: '', baseSalary: 0, hiredAt: '' }
    await load()
  } catch (e) { error.value = (e as Error).message }
}

async function doResign(r: Employee) {
  if (!confirm(`确认为 ${r.fullName} 办理离职？`)) return
  try { await resignEmployee(r.employeeId, new Date().toISOString()); await load() } catch { /* ignore */ }
}

function fmt(v: number | null) { return v != null ? Number(v).toLocaleString('zh-CN') : '—' }
onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="员工档案" sub="员工基本信息、入离职与岗位管理">
      <template #actions>
        <button class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium border-0 cursor-pointer" @click="open = true"><Plus :size="14" /> 新增员工</button>
      </template>
    </PageHeader>
    <div class="p-5">
      <div class="flex items-center gap-3 mb-4">
        <input v-model="keyword" placeholder="姓名 / 工号" class="h-8 px-3 border border-border rounded bg-card text-[12px] w-56" @keyup.enter="load" />
        <select v-model="statusFilter" class="h-8 px-2 border border-border rounded bg-card text-[12px]" @change="load">
          <option value="">全部状态</option><option value="active">在职</option><option value="probation">试用</option><option value="resigned">已离职</option>
        </select>
        <button class="flex items-center gap-1 h-8 px-3 border border-border rounded bg-card text-[12px] cursor-pointer" @click="load"><Search :size="13" /> 查询</button>
        <span class="ml-auto text-[11px] text-muted-foreground">共 {{ rows.length }} 人</span>
      </div>
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">工号</th><th class="text-left px-4 py-2.5 font-medium">姓名</th>
            <th class="text-left px-4 py-2.5 font-medium">部门</th><th class="text-left px-4 py-2.5 font-medium">岗位</th>
            <th class="text-left px-4 py-2.5 font-medium">用工类型</th><th class="text-right px-4 py-2.5 font-medium">基本工资</th>
            <th class="text-left px-4 py-2.5 font-medium">入职日期</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
            <th class="text-right px-4 py-2.5 font-medium">操作</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="9" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="9" class="text-center py-8 text-muted-foreground">暂无员工</td></tr>
            <tr v-for="r in rows" :key="r.employeeId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.employeeNo }}</td>
              <td class="px-4 py-2.5 font-medium">{{ r.fullName }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.department || '—' }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.position || '—' }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ typeLabels[r.employmentType] ?? r.employmentType }}</td>
              <td class="px-4 py-2.5 text-right font-mono-tnum">{{ fmt(r.baseSalary) }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.hiredAt?.slice(0, 10) || '—' }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
              <td class="px-4 py-2.5 text-right"><button v-if="r.status !== 'resigned'" class="inline-flex items-center gap-1 px-2 py-1 text-[11px] border border-border rounded bg-card cursor-pointer" @click="doResign(r)"><UserMinus :size="12" /> 离职</button><span v-else class="text-muted-foreground">—</span></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="open" title="新增员工" @close="open = false">
      <div class="space-y-3">
        <div class="grid grid-cols-2 gap-3">
          <label class="block text-[12px]">工号<input v-model="form.employeeNo" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">姓名<input v-model="form.fullName" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">部门<input v-model="form.department" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">岗位<input v-model="form.position" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">用工类型<select v-model="form.employmentType" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option v-for="(v, k) in typeLabels" :key="k" :value="k">{{ v }}</option></select></label>
          <label class="block text-[12px]">入职日期<input v-model="form.hiredAt" type="date" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">基本工资<input v-model.number="form.baseSalary" type="number" min="0" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
          <label class="block text-[12px]">手机<input v-model="form.phone" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        </div>
        <label class="block text-[12px]">邮箱<input v-model="form.email" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <p v-if="error" class="text-[12px] text-danger">{{ error }}</p>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="open = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submit">保存</button>
      </template>
    </Modal>
  </div>
</template>
