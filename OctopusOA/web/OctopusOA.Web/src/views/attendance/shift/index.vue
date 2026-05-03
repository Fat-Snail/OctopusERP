<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Plus } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'

import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'
import { toast, confirm } from '@/components/ui/toast'
import {
  getShiftList, createShift, updateShift, deleteShift, setDefaultShift,
  getUserShifts, assignUserShift,
} from '@/api/attendance'
import type { AttendanceRule, UserShift } from '@/api/attendance/types'

const tab = ref<'shifts' | 'users'>('shifts')
const loading = ref(false)
const userLoading = ref(false)
const submitting = ref(false)
const shifts = ref<AttendanceRule[]>([])
const userShifts = ref<UserShift[]>([])

const formVisible = ref(false)
const isEdit = ref(false)
const codeError = ref('')
const nameError = ref('')
const formData = reactive<AttendanceRule>({
  id: 0, code: '', name: '',
  workStartTime: '09:00', workEndTime: '18:00',
  lateThresholdMin: 15, earlyLeaveThresholdMin: 30,
  ipWhiteList: null, isDefault: false, status: 1,
})

const assignVisible = ref(false)
const assignTarget = ref<UserShift | null>(null)
const assignShiftId = ref<number>(0)

async function load() {
  loading.value = true
  try { shifts.value = await getShiftList() } finally { loading.value = false }
}

async function loadUsers() {
  userLoading.value = true
  try { userShifts.value = (await getUserShifts()).rows } finally { userLoading.value = false }
}

function openCreate() {
  isEdit.value = false; codeError.value = ''; nameError.value = ''
  Object.assign(formData, { id: 0, code: '', name: '', workStartTime: '09:00', workEndTime: '18:00', lateThresholdMin: 15, earlyLeaveThresholdMin: 30, ipWhiteList: null, isDefault: false, status: 1 })
  formVisible.value = true
}

function openEdit(row: AttendanceRule) {
  isEdit.value = true; codeError.value = ''; nameError.value = ''
  Object.assign(formData, row)
  formVisible.value = true
}

async function handleSubmit() {
  codeError.value = formData.code.trim() ? '' : '请输入班次编码'
  nameError.value = formData.name.trim() ? '' : '请输入班次名称'
  if (codeError.value || nameError.value) return
  submitting.value = true
  try {
    if (isEdit.value) { await updateShift(formData); toast('修改成功', 'success') }
    else { await createShift(formData); toast('创建成功', 'success') }
    formVisible.value = false
    await load()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

async function handleDelete(row: AttendanceRule) {
  try {
    await confirm(`确认删除班次「${row.name}」？`)
    await deleteShift(row.id); toast('已删除', 'success'); await load()
  } catch { /* cancelled */ }
}

async function handleSetDefault(row: AttendanceRule) {
  try {
    await confirm(`将「${row.name}」设为默认班次？`)
    await setDefaultShift(row.id); toast('已设为默认', 'success'); await load()
  } catch { /* cancelled */ }
}

function openAssign(row: UserShift) {
  assignTarget.value = row; assignShiftId.value = row.shiftId; assignVisible.value = true
}

async function handleAssign() {
  if (!assignTarget.value) return
  submitting.value = true
  try {
    await assignUserShift(assignTarget.value.umcUserId, assignShiftId.value)
    toast('分配成功', 'success'); assignVisible.value = false; await loadUsers()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

onMounted(async () => { await load(); await loadUsers() })
</script>

<template>
  <div>
    <PageHeader title="班次管理">
      <template #actions>
        <button v-if="tab === 'shifts'" class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 flex items-center gap-1" @click="openCreate">
          <Plus :size="13" />新建班次
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <div class="flex gap-0 border border-border rounded-[var(--radius)] overflow-hidden w-fit mb-4 text-[12px]">
        <button :class="['px-4 py-1.5 cursor-pointer border-0 transition-colors', tab === 'shifts' ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:bg-muted']" @click="tab = 'shifts'">班次列表</button>
        <button :class="['px-4 py-1.5 cursor-pointer border-0 transition-colors', tab === 'users' ? 'bg-primary text-primary-foreground' : 'bg-card text-muted-foreground hover:bg-muted']" @click="tab = 'users'">员工班次分配</button>
      </div>

      <!-- Shifts tab -->
      <div v-if="tab === 'shifts'">
        <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
        <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
          <table class="w-full text-[12px]">
            <thead class="bg-subtle">
              <tr>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">编码</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">名称</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-32">工作时段</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">阈值（迟到/早退）</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">IP 白名单</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">状态</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">操作</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="row in shifts" :key="row.id" class="hover:bg-subtle">
                <td class="px-3 py-2 font-mono text-[11px]">{{ row.code }}</td>
                <td class="px-3 py-2 font-medium">
                  {{ row.name }}
                  <Badge v-if="row.isDefault" variant="success" class="ml-1">默认</Badge>
                </td>
                <td class="px-3 py-2 font-mono-tnum text-muted-foreground whitespace-nowrap">{{ row.workStartTime }} – {{ row.workEndTime }}</td>
                <td class="px-3 py-2 text-muted-foreground">{{ row.lateThresholdMin }} / {{ row.earlyLeaveThresholdMin }} 分钟</td>
                <td class="px-3 py-2 text-muted-foreground truncate max-w-[160px]">{{ row.ipWhiteList || '不限制' }}</td>
                <td class="px-3 py-2">
                  <Badge :variant="row.status === 1 ? 'success' : 'secondary'">{{ row.status === 1 ? '启用' : '停用' }}</Badge>
                </td>
                <td class="px-3 py-2 whitespace-nowrap">
                  <div class="flex items-center gap-2">
                    <button class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openEdit(row)">编辑</button>
                    <button v-if="!row.isDefault" class="text-[11px] text-warning hover:underline cursor-pointer border-0 bg-transparent" @click="handleSetDefault(row)">设为默认</button>
                    <button v-if="!row.isDefault" class="text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent" @click="handleDelete(row)">删除</button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Users tab -->
      <div v-else>
        <div v-if="userLoading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
        <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
          <table class="w-full text-[12px]">
            <thead class="bg-subtle">
              <tr>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">姓名</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">用户名</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">当前班次</th>
                <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">操作</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border">
              <tr v-for="row in userShifts" :key="row.umcUserId" class="hover:bg-subtle">
                <td class="px-3 py-2 font-medium">{{ row.nickName }}</td>
                <td class="px-3 py-2 text-muted-foreground">{{ row.userName }}</td>
                <td class="px-3 py-2">
                  <Badge variant="secondary">{{ row.shiftName }}</Badge>
                  <span class="ml-2 text-[11px] text-muted-foreground font-mono-tnum">{{ row.workStartTime }}–{{ row.workEndTime }}</span>
                </td>
                <td class="px-3 py-2 whitespace-nowrap">
                  <button class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openAssign(row)">分配班次</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Shift form modal -->
    <Dialog v-model:open="formVisible">
      <DialogContent class="p-5 w-[480px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">{{ isEdit ? '编辑班次' : '新建班次' }}</DialogTitle>
        </DialogHeader>
        <div class="space-y-3">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 班次编码</label>
              <input v-model="formData.code" :disabled="isEdit" type="text" placeholder="如 night、flex_2" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring disabled:bg-subtle', codeError ? 'border-danger' : 'border-input']" />
              <p v-if="codeError" class="text-[11px] text-danger mt-0.5">{{ codeError }}</p>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 班次名称</label>
              <input v-model="formData.name" type="text" placeholder="如 夜班、弹性班 B" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', nameError ? 'border-danger' : 'border-input']" />
              <p v-if="nameError" class="text-[11px] text-danger mt-0.5">{{ nameError }}</p>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">上班时间</label>
              <input v-model="formData.workStartTime" type="time" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">下班时间</label>
              <input v-model="formData.workEndTime" type="time" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">迟到阈值（分钟）</label>
              <input v-model.number="formData.lateThresholdMin" type="number" min="0" max="120" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1">早退阈值（分钟）</label>
              <input v-model.number="formData.earlyLeaveThresholdMin" type="number" min="0" max="120" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
            </div>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">IP 白名单</label>
            <textarea v-model="formData.ipWhiteList!" rows="2" placeholder="逗号分隔，留空则不限制" class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">状态</label>
            <div class="flex gap-4 text-[12px]">
              <label class="flex items-center gap-1.5 cursor-pointer"><input type="radio" :value="1" v-model="formData.status" class="accent-primary" />启用</label>
              <label class="flex items-center gap-1.5 cursor-pointer"><input type="radio" :value="0" v-model="formData.status" class="accent-primary" />停用</label>
            </div>
          </div>
        </div>
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="formVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleSubmit">
            {{ submitting ? '保存中...' : '保存' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Assign shift modal -->
    <Dialog v-model:open="assignVisible">
      <DialogContent class="p-5 w-[380px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">分配班次</DialogTitle>
        </DialogHeader>
        <p v-if="assignTarget" class="text-[12px] text-muted-foreground mb-3">
          为 <strong class="text-foreground">{{ assignTarget.nickName }}</strong>（{{ assignTarget.userName }}）选择班次：
        </p>
        <Select :model-value="String(assignShiftId)" @update:model-value="assignShiftId = Number($event)">
          <SelectTrigger class="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem v-for="s in shifts" :key="s.id" :value="String(s.id)">
              {{ s.name }}（{{ s.workStartTime }}–{{ s.workEndTime }}）
            </SelectItem>
          </SelectContent>
        </Select>
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="assignVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleAssign">
            {{ submitting ? '确认中...' : '确认' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
