<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Plus, Link } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'

import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'
import { toast, confirm } from '@/components/ui/toast'
import { getEmployeeList, createEmployee, deleteEmployee } from '@/api/employee'
import type { EmployeeResponse, CreateEmployeeRequest } from '@/api/employee/types'

const router = useRouter()
const loading = ref(false)
const submitting = ref(false)
const list = ref<EmployeeResponse[]>([])
const query = reactive({ status: '', name: '' })
const createVisible = ref(false)
const linkVisible = ref(false)
const linkUrl = ref('')
const linkName = ref('')

const formData = reactive<CreateEmployeeRequest>({
  name: '', gender: 'male', phone: '', applyPosition: '',
})
const errors = reactive<Record<string, string>>({})

async function loadData() {
  loading.value = true
  try {
    const params: Record<string, string> = {}
    if (query.status) params['status'] = query.status
    if (query.name) params['name'] = query.name
    list.value = (await getEmployeeList(params)).rows
  } finally { loading.value = false }
}

function openCreate() {
  Object.assign(formData, { name: '', gender: 'male', phone: '', email: '', birthDate: undefined, ethnicity: '', applyPosition: '', applyDeptName: '', education: '', graduateSchool: '', major: '', expectedSalary: '', workYears: undefined, hrRemark: '' })
  Object.keys(errors).forEach(k => { errors[k] = '' })
  createVisible.value = true
}

async function handleCreate() {
  errors['name'] = formData.name.trim() ? '' : '请输入姓名'
  errors['phone'] = formData.phone.trim() ? '' : '请输入手机号'
  errors['applyPosition'] = formData.applyPosition.trim() ? '' : '请输入应聘岗位'
  if (Object.values(errors).some(Boolean)) return
  submitting.value = true
  try {
    const emp = await createEmployee(formData)
    toast('创建成功', 'success')
    createVisible.value = false
    linkName.value = emp.name
    linkUrl.value = emp.h5Url || `${window.location.origin}/h5/onboard/${emp.h5Token}`
    linkVisible.value = true
    loadData()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

function copyH5Link(row: EmployeeResponse) {
  linkName.value = row.name
  linkUrl.value = row.h5Url || `${window.location.origin}/h5/onboard/${row.h5Token}`
  linkVisible.value = true
}

function doCopy() {
  navigator.clipboard.writeText(linkUrl.value)
  toast('已复制到剪贴板', 'success')
}

async function handleDelete(row: EmployeeResponse) {
  try {
    await confirm(`确认删除「${row.name}」的档案？`)
    await deleteEmployee(row.employeeId); toast('已删除', 'success'); loadData()
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
    <PageHeader title="职员管理">
      <template #actions>
        <button class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 flex items-center gap-1" @click="openCreate">
          <Plus :size="13" />新建档案
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <!-- Search bar -->
      <div class="flex items-center gap-2 mb-4">
        <Select v-model="query.status" @update:model-value="loadData">
          <SelectTrigger class="w-28">
            <SelectValue placeholder="全部状态" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">全部状态</SelectItem>
            <SelectItem value="temp">临时</SelectItem>
            <SelectItem value="pending">待入职</SelectItem>
            <SelectItem value="active">在职</SelectItem>
            <SelectItem value="rejected">已拒绝</SelectItem>
            <SelectItem value="resigned">已离职</SelectItem>
          </SelectContent>
        </Select>
        <input v-model="query.name" type="text" placeholder="搜索姓名" class="h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring w-36" @keyup.enter="loadData" />
        <button class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0" @click="loadData">搜索</button>
      </div>

      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!list.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无档案</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">姓名</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">手机号</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">应聘岗位</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">部门</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">学历</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">H5</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">创建时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in list" :key="row.employeeId" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium">{{ row.name }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ row.phone }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.applyPosition }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.applyDeptName || '-' }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.education || '-' }}</td>
              <td class="px-3 py-2"><Badge :variant="statusVariant(row.status)">{{ statusLabel(row.status) }}</Badge></td>
              <td class="px-3 py-2">
                <Badge :variant="row.h5FilledAt ? 'success' : 'secondary'">{{ row.h5FilledAt ? '已填' : '未填' }}</Badge>
              </td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ row.createTime }}</td>
              <td class="px-3 py-2 whitespace-nowrap">
                <div class="flex items-center gap-2">
                  <button class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="router.push(`/employee/${row.employeeId}`)">详情</button>
                  <button v-if="row.status === 'temp'" class="flex items-center gap-0.5 text-[11px] text-info hover:underline cursor-pointer border-0 bg-transparent" @click="copyH5Link(row)">
                    <Link :size="10" />链接
                  </button>
                  <button v-if="row.status === 'temp' || row.status === 'rejected'" class="text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent" @click="handleDelete(row)">删除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Create modal -->
    <Dialog v-model:open="createVisible">
      <DialogContent class="p-5 w-[580px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">新建职员档案</DialogTitle>
        </DialogHeader>
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 姓名</label>
            <input v-model="formData.name" type="text" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', errors['name'] ? 'border-danger' : 'border-input']" />
            <p v-if="errors['name']" class="text-[11px] text-danger mt-0.5">{{ errors['name'] }}</p>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 性别</label>
            <div class="flex gap-4 text-[12px] h-[var(--row-h)] items-center">
              <label class="flex items-center gap-1.5 cursor-pointer"><input type="radio" value="male" v-model="formData.gender" class="accent-primary" />男</label>
              <label class="flex items-center gap-1.5 cursor-pointer"><input type="radio" value="female" v-model="formData.gender" class="accent-primary" />女</label>
            </div>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 手机号</label>
            <input v-model="formData.phone" type="text" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', errors['phone'] ? 'border-danger' : 'border-input']" />
            <p v-if="errors['phone']" class="text-[11px] text-danger mt-0.5">{{ errors['phone'] }}</p>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">邮箱</label>
            <input v-model="(formData as Record<string, unknown>)['email'] as string" type="email" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">出生日期</label>
            <input v-model="(formData as Record<string, unknown>)['birthDate'] as string" type="date" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">民族</label>
            <input v-model="(formData as Record<string, unknown>)['ethnicity'] as string" type="text" placeholder="如：汉族" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 应聘岗位</label>
            <input v-model="formData.applyPosition" type="text" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', errors['applyPosition'] ? 'border-danger' : 'border-input']" />
            <p v-if="errors['applyPosition']" class="text-[11px] text-danger mt-0.5">{{ errors['applyPosition'] }}</p>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">应聘部门</label>
            <input v-model="(formData as Record<string, unknown>)['applyDeptName'] as string" type="text" placeholder="如：技术部" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">学历</label>
            <Select :model-value="(formData as Record<string, unknown>)['education'] as string || ''" @update:model-value="(formData as Record<string, unknown>)['education'] = $event">
              <SelectTrigger class="w-full">
                <SelectValue placeholder="请选择" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">请选择</SelectItem>
                <SelectItem value="高中">高中</SelectItem>
                <SelectItem value="大专">大专</SelectItem>
                <SelectItem value="本科">本科</SelectItem>
                <SelectItem value="硕士">硕士</SelectItem>
                <SelectItem value="博士">博士</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">工作年限</label>
            <input v-model.number="(formData as Record<string, unknown>)['workYears'] as number" type="number" min="0" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">毕业院校</label>
            <input v-model="(formData as Record<string, unknown>)['graduateSchool'] as string" type="text" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">专业</label>
            <input v-model="(formData as Record<string, unknown>)['major'] as string" type="text" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">期望薪资</label>
            <input v-model="(formData as Record<string, unknown>)['expectedSalary'] as string" type="text" placeholder="如：15K-20K" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div class="col-span-2">
            <label class="block text-[12px] font-medium mb-1">HR 备注</label>
            <textarea v-model="(formData as Record<string, unknown>)['hrRemark'] as string" rows="2" class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
        </div>
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="createVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleCreate">
            {{ submitting ? '创建中...' : '创建并生成入职链接' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- H5 link modal -->
    <Dialog v-model:open="linkVisible">
      <DialogContent class="p-5 w-[480px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">入职填写链接</DialogTitle>
        </DialogHeader>
        <p class="text-[12px] text-muted-foreground mb-3">请将以下链接发送给 <strong class="text-foreground">{{ linkName }}</strong>：</p>
        <div class="flex gap-2">
          <input :value="linkUrl" readonly class="flex-1 h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-subtle text-[12px]" />
          <button class="h-[var(--row-h)] px-3 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card flex items-center gap-1" @click="doCopy">
            <Link :size="12" />复制
          </button>
        </div>
        <p class="text-[11px] text-muted-foreground mt-2">候选人通过此链接填写入职信息后，状态将自动变为「待入职」。</p>
        <div class="flex justify-end mt-4">
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="linkVisible = false">关闭</button>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
