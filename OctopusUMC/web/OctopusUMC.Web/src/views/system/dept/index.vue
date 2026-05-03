<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Edit, Trash2 } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { DeptResponse } from '@/api/system/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const deptList = ref<DeptResponse[]>([])
const deptTreeForSelect = ref<DeptResponse[]>([])
const dialogOpen = ref(false)
const isEdit = ref(false)
const submitError = ref('')
const query = reactive({ deptName: '' })

interface DeptForm {
  deptId?: number
  parentId: number
  deptName: string
  orderNum: number
  status: 0 | 1
}

const formData = reactive<DeptForm>({ parentId: 0, deptName: '', orderNum: 0, status: 1 })

async function loadDepts() {
  loading.value = true
  try {
    const data = await get<DeptResponse[]>('/system/dept/tree')
    deptTreeForSelect.value = data
    deptList.value = data
  } finally { loading.value = false }
}

function resetQuery() {
  query.deptName = ''
  loadDepts()
}

function openAdd(parentId: number) {
  isEdit.value = false
  Object.assign(formData, { parentId, deptName: '', orderNum: 0, status: 1 })
  delete formData.deptId
  submitError.value = ''
  dialogOpen.value = true
}

function openEdit(row: DeptResponse) {
  isEdit.value = true
  Object.assign(formData, {
    deptId: row.deptId,
    parentId: row.parentId,
    deptName: row.deptName,
    orderNum: row.orderNum,
    status: row.status,
  })
  submitError.value = ''
  dialogOpen.value = true
}

async function handleSubmit() {
  if (!formData.deptName) { submitError.value = '请输入部门名称'; return }
  submitError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) {
      await put('/system/dept', formData)
    } else {
      await post('/system/dept', formData)
    }
    dialogOpen.value = false
    loadDepts()
  } catch {
    submitError.value = '操作失败，请重试'
  } finally { submitting.value = false }
}

async function handleDelete(row: DeptResponse) {
  if (!confirm(`确认删除部门 "${row.deptName}" 吗？`)) return
  await del(`/system/dept/${row.deptId}`)
  loadDepts()
}

// Flatten tree with depth
function flattenDepts(nodes: DeptResponse[], depth = 0): Array<DeptResponse & { depth: number }> {
  const result: Array<DeptResponse & { depth: number }> = []
  for (const n of nodes) {
    result.push({ ...n, depth })
    if (n.children?.length) result.push(...flattenDepts(n.children, depth + 1))
  }
  return result
}

// Flatten for select options
function flattenForSelect(nodes: DeptResponse[], prefix = ''): Array<{ deptId: number; label: string }> {
  const result: Array<{ deptId: number; label: string }> = []
  for (const n of nodes) {
    const label = prefix ? `${prefix} / ${n.deptName}` : n.deptName
    result.push({ deptId: n.deptId, label })
    if (n.children?.length) result.push(...flattenForSelect(n.children, label))
  }
  return result
}

onMounted(loadDepts)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <!-- Search bar -->
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">部门名称</span>
        <input
          v-model="query.deptName"
          placeholder="部门名称"
          class="h-7 w-[160px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </div>
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadDepts">
        <Search :size="12" /> 搜索
      </button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery">
        <RefreshCw :size="12" /> 重置
      </button>
    </div>

    <!-- Action bar -->
    <div class="flex items-center gap-2 px-4 py-2.5 border-b border-border">
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="openAdd(0)">
        <Plus :size="12" /> 新增
      </button>
    </div>

    <!-- Tree table -->
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">部门名称</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">排序</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-24">状态</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">创建时间</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading">
            <td colspan="5" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td>
          </tr>
          <tr
            v-else
            v-for="node in flattenDepts(deptList)"
            :key="node.deptId"
            class="border-b border-border last:border-0 hover:bg-subtle"
          >
            <td class="px-4 py-2 text-[12px] text-foreground" :style="{ paddingLeft: `${16 + node.depth * 16}px` }">
              <span v-if="node.depth > 0" class="text-muted-foreground mr-1">└</span>{{ node.deptName }}
            </td>
            <td class="px-4 py-2 text-[12px] text-right font-mono-tnum text-muted-foreground">{{ node.orderNum }}</td>
            <td class="px-4 py-2">
              <Badge :variant="node.status === 1 ? 'success' : 'danger'">{{ node.status === 1 ? '正常' : '停用' }}</Badge>
            </td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ node.createTime?.slice(0, 10) }}</td>
            <td class="px-4 py-2 text-right">
              <div class="flex items-center justify-end gap-1">
                <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openAdd(node.deptId)">
                  <Plus :size="10" /> 子部门
                </button>
                <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openEdit(node)">
                  <Edit :size="10" /> 编辑
                </button>
                <button class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleDelete(node)">
                  <Trash2 :size="10" /> 删除
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

  <Dialog v-model:open="dialogOpen">
    <DialogContent class="w-[480px]">
      <DialogHeader>
        <DialogTitle>{{ isEdit ? '编辑部门' : '新增部门' }}</DialogTitle>
      </DialogHeader>
      <div class="space-y-3 py-2">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">上级部门</label>
          <select v-model="formData.parentId" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option :value="0">顶级部门</option>
            <option v-for="opt in flattenForSelect(deptTreeForSelect)" :key="opt.deptId" :value="opt.deptId">{{ opt.label }}</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">部门名称 <span class="text-danger">*</span></label>
          <input v-model="formData.deptName" placeholder="请输入部门名称" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">排序</label>
          <input v-model.number="formData.orderNum" type="number" min="0" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">状态</label>
          <div class="flex gap-4">
            <label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer">
              <input v-model="formData.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" />
              <span class="text-[12px] text-foreground">{{ opt.l }}</span>
            </label>
          </div>
        </div>
      </div>
      <p v-if="submitError" class="text-[12px] text-danger">{{ submitError }}</p>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="dialogOpen = false">取消</button>
        <button :disabled="submitting" class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSubmit">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
