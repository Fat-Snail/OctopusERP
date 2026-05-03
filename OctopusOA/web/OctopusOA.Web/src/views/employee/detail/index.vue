<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ChevronLeft, Link } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { toast, confirm } from '@/components/ui/toast'
import { getEmployeeById, confirmEmployee, rejectEmployee, resignEmployee } from '@/api/employee'
import type { EmployeeResponse } from '@/api/employee/types'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const emp = ref<EmployeeResponse | null>(null)

async function loadData() {
  loading.value = true
  try { emp.value = await getEmployeeById(Number(route.params['id'])) } finally { loading.value = false }
}

function copyH5Link() {
  if (!emp.value) return
  const url = emp.value.h5Url || `${window.location.origin}/h5/onboard/${emp.value.h5Token}`
  navigator.clipboard.writeText(url); toast('H5 链接已复制', 'success')
}

async function handleConfirm() {
  try {
    await confirm('确认该候选人入职？将自动创建系统账号。')
    await confirmEmployee(emp.value!.employeeId); toast('入职成功', 'success'); loadData()
  } catch { /* cancelled or error */ }
}

async function handleReject() {
  try {
    await confirm('确认拒绝该候选人？')
    await rejectEmployee(emp.value!.employeeId); toast('已拒绝', 'info'); loadData()
  } catch { /* cancelled */ }
}

async function handleResign() {
  try {
    await confirm('确认办理离职？')
    await resignEmployee(emp.value!.employeeId); toast('已办理离职', 'success'); loadData()
  } catch { /* cancelled */ }
}

type BadgeVariant = 'secondary' | 'warning' | 'success' | 'danger'
function statusVariant(s: string): BadgeVariant {
  return ({ temp: 'secondary', pending: 'warning', active: 'success', rejected: 'danger', resigned: 'secondary' } as Record<string, BadgeVariant>)[s] ?? 'secondary'
}
function statusLabel(s: string) { return { temp: '临时', pending: '待入职', active: '在职', rejected: '已拒绝', resigned: '已离职' }[s] ?? s }

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader :title="emp ? `${emp.name} 的档案` : '档案详情'">
      <template #actions>
        <div class="flex items-center gap-2">
          <Badge v-if="emp" :variant="statusVariant(emp.status)">{{ statusLabel(emp.status) }}</Badge>
          <button class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent" @click="router.push('/employee/list')">
            <ChevronLeft :size="14" />返回列表
          </button>
        </div>
      </template>
    </PageHeader>

    <div v-if="loading" class="p-5 py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
    <div v-else-if="emp" class="p-5 space-y-4">
      <!-- Action buttons -->
      <div class="flex gap-2">
        <button v-if="emp.status === 'temp'" class="flex items-center gap-1 h-[var(--row-h)] px-3 text-[12px] border border-info/50 text-info rounded-[var(--radius)] hover:bg-info/10 cursor-pointer bg-card" @click="copyH5Link">
          <Link :size="12" />复制 H5 链接
        </button>
        <button v-if="emp.status === 'pending'" class="h-[var(--row-h)] px-4 text-[12px] bg-success text-white rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0" @click="handleConfirm">确认入职</button>
        <button v-if="emp.status === 'pending' || emp.status === 'temp'" class="h-[var(--row-h)] px-4 text-[12px] bg-danger text-white rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0" @click="handleReject">拒绝</button>
        <button v-if="emp.status === 'active'" class="h-[var(--row-h)] px-4 text-[12px] border border-warning/50 text-warning rounded-[var(--radius)] hover:bg-warning/10 cursor-pointer bg-card" @click="handleResign">办理离职</button>
      </div>

      <!-- HR info section -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <h3 class="text-[13px] font-semibold text-foreground mb-3">HR 填写（简历信息）</h3>
        <div class="grid grid-cols-3 gap-x-6 gap-y-2 text-[12px]">
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">姓名</span><span>{{ emp.name }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">性别</span><span>{{ emp.gender === 'male' ? '男' : '女' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">手机号</span><span class="font-mono-tnum">{{ emp.phone }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">邮箱</span><span>{{ emp.email || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">出生日期</span><span>{{ emp.birthDate || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">民族</span><span>{{ emp.ethnicity || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">应聘岗位</span><span>{{ emp.applyPosition }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">应聘部门</span><span>{{ emp.applyDeptName || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">学历</span><span>{{ emp.education || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">院校</span><span>{{ emp.graduateSchool || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">专业</span><span>{{ emp.major || '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">工作年限</span><span>{{ emp.workYears ?? '-' }}</span></div>
          <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">期望薪资</span><span>{{ emp.expectedSalary || '-' }}</span></div>
          <div class="col-span-2 flex gap-2"><span class="text-muted-foreground w-20 shrink-0">HR 备注</span><span>{{ emp.hrRemark || '-' }}</span></div>
        </div>
      </div>

      <!-- H5 filled section -->
      <div class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
        <div class="flex items-center gap-2 mb-3">
          <h3 class="text-[13px] font-semibold text-foreground">入职人员填写</h3>
          <Badge v-if="emp.h5FilledAt" variant="success">已填写</Badge>
        </div>
        <div v-if="!emp.h5FilledAt" class="py-6 text-center text-muted-foreground text-[12px]">入职人员尚未填写 H5 表单</div>
        <div v-else>
          <div class="grid grid-cols-3 gap-x-6 gap-y-2 text-[12px] mb-4">
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">身份证号</span><span class="font-mono-tnum">{{ emp.idCardNo || '-' }}</span></div>
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">政治面貌</span><span>{{ emp.politicalStatus || '-' }}</span></div>
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">婚姻状况</span><span>{{ emp.maritalStatus || '-' }}</span></div>
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">籍贯</span><span>{{ emp.nativePlace || '-' }}</span></div>
            <div class="col-span-2 flex gap-2"><span class="text-muted-foreground w-20 shrink-0">现居住地址</span><span>{{ emp.currentAddress || '-' }}</span></div>
            <div class="col-span-3 flex gap-2"><span class="text-muted-foreground w-20 shrink-0">户籍地址</span><span>{{ emp.householdAddress || '-' }}</span></div>
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">开户银行</span><span>{{ emp.bankName || '-' }}</span></div>
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">银行账号</span><span class="font-mono-tnum">{{ emp.bankAccount || '-' }}</span></div>
            <div class="flex gap-2"><span class="text-muted-foreground w-20 shrink-0">开户支行</span><span>{{ emp.bankBranch || '-' }}</span></div>
          </div>

          <template v-if="emp.educations.length">
            <h4 class="text-[12px] font-semibold text-foreground mb-2 mt-4">教育经历</h4>
            <div class="border border-border rounded-[var(--radius)] overflow-hidden mb-3">
              <table class="w-full text-[12px]">
                <thead class="bg-subtle"><tr>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">学校</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-16">学历</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">专业</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-20">开始</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-20">结束</th>
                </tr></thead>
                <tbody class="divide-y divide-border">
                  <tr v-for="e in emp.educations" :key="e.school" class="hover:bg-subtle">
                    <td class="px-3 py-1.5">{{ e.school }}</td>
                    <td class="px-3 py-1.5">{{ e.education }}</td>
                    <td class="px-3 py-1.5">{{ e.major }}</td>
                    <td class="px-3 py-1.5 font-mono-tnum text-muted-foreground">{{ e.startDate }}</td>
                    <td class="px-3 py-1.5 font-mono-tnum text-muted-foreground">{{ e.endDate }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <template v-if="emp.workHistories.length">
            <h4 class="text-[12px] font-semibold text-foreground mb-2">工作经历</h4>
            <div class="border border-border rounded-[var(--radius)] overflow-hidden mb-3">
              <table class="w-full text-[12px]">
                <thead class="bg-subtle"><tr>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">公司</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-24">职位</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-20">开始</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-20">结束</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">离职原因</th>
                </tr></thead>
                <tbody class="divide-y divide-border">
                  <tr v-for="w in emp.workHistories" :key="w.company" class="hover:bg-subtle">
                    <td class="px-3 py-1.5">{{ w.company }}</td>
                    <td class="px-3 py-1.5 text-muted-foreground">{{ w.position }}</td>
                    <td class="px-3 py-1.5 font-mono-tnum text-muted-foreground">{{ w.startDate }}</td>
                    <td class="px-3 py-1.5 font-mono-tnum text-muted-foreground">{{ w.endDate }}</td>
                    <td class="px-3 py-1.5 text-muted-foreground">{{ w.leaveReason || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <template v-if="emp.families.length">
            <h4 class="text-[12px] font-semibold text-foreground mb-2">家庭成员</h4>
            <div class="border border-border rounded-[var(--radius)] overflow-hidden mb-3">
              <table class="w-full text-[12px]">
                <thead class="bg-subtle"><tr>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">姓名</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-16">关系</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">工作单位</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-28">联系电话</th>
                </tr></thead>
                <tbody class="divide-y divide-border">
                  <tr v-for="f in emp.families" :key="f.name" class="hover:bg-subtle">
                    <td class="px-3 py-1.5">{{ f.name }}</td>
                    <td class="px-3 py-1.5 text-muted-foreground">{{ f.relation }}</td>
                    <td class="px-3 py-1.5 text-muted-foreground">{{ f.workplace || '-' }}</td>
                    <td class="px-3 py-1.5 font-mono-tnum text-muted-foreground">{{ f.phone || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>

          <template v-if="emp.emergencyContacts.length">
            <h4 class="text-[12px] font-semibold text-foreground mb-2">紧急联系人</h4>
            <div class="border border-border rounded-[var(--radius)] overflow-hidden">
              <table class="w-full text-[12px]">
                <thead class="bg-subtle"><tr>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground">姓名</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-16">关系</th>
                  <th class="px-3 py-1.5 text-left text-[11px] font-medium text-muted-foreground w-28">联系电话</th>
                </tr></thead>
                <tbody class="divide-y divide-border">
                  <tr v-for="ec in emp.emergencyContacts" :key="ec.name" class="hover:bg-subtle">
                    <td class="px-3 py-1.5">{{ ec.name }}</td>
                    <td class="px-3 py-1.5 text-muted-foreground">{{ ec.relation }}</td>
                    <td class="px-3 py-1.5 font-mono-tnum text-muted-foreground">{{ ec.phone }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </template>
        </div>
      </div>
    </div>
  </div>
</template>
