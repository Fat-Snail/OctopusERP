<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Edit, Trash2 } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { MenuResponse } from '@/api/system/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const menuTree = ref<MenuResponse[]>([])
const menuTreeForSelect = ref<MenuResponse[]>([])
const dialogOpen = ref(false)
const isEdit = ref(false)
const submitError = ref('')
const query = reactive({ menuName: '' })

interface MenuForm {
  menuId?: number
  parentId: number
  menuName: string
  menuType: 'M' | 'C' | 'F'
  path: string
  component: string
  permission: string
  icon: string
  orderNum: number
  status: 0 | 1
}

const formData = reactive<MenuForm>({
  parentId: 0, menuName: '', menuType: 'C', path: '', component: '', permission: '', icon: '', orderNum: 0, status: 1,
})

async function loadMenus() {
  loading.value = true
  try {
    const data = await get<MenuResponse[]>('/system/menu/tree')
    menuTree.value = data
    menuTreeForSelect.value = data
  } finally { loading.value = false }
}

function resetQuery() { query.menuName = ''; loadMenus() }

function openAdd(parentId = 0) {
  isEdit.value = false
  Object.assign(formData, { parentId, menuName: '', menuType: 'C', path: '', component: '', permission: '', icon: '', orderNum: 0, status: 1 })
  delete formData.menuId
  submitError.value = ''
  dialogOpen.value = true
}

function openEdit(row: MenuResponse) {
  isEdit.value = true
  Object.assign(formData, {
    menuId: row.menuId, parentId: row.parentId, menuName: row.menuName, menuType: row.menuType,
    path: row.path ?? '', component: row.component ?? '', permission: row.permission ?? '',
    icon: row.icon ?? '', orderNum: row.orderNum, status: row.status,
  })
  submitError.value = ''
  dialogOpen.value = true
}

async function handleSubmit() {
  if (!formData.menuName) { submitError.value = '请输入菜单名称'; return }
  submitError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) { await put('/system/menu', formData) }
    else { await post('/system/menu', formData) }
    dialogOpen.value = false
    loadMenus()
  } catch { submitError.value = '操作失败' } finally { submitting.value = false }
}

async function handleDelete(row: MenuResponse) {
  if (!confirm(`确认删除菜单 "${row.menuName}" 吗？`)) return
  await del(`/system/menu/${row.menuId}`)
  loadMenus()
}

function flattenMenus(nodes: MenuResponse[], depth = 0): Array<MenuResponse & { depth: number }> {
  const result: Array<MenuResponse & { depth: number }> = []
  for (const n of nodes) {
    result.push({ ...n, depth })
    if (n.children?.length) result.push(...flattenMenus(n.children, depth + 1))
  }
  return result
}

function flattenForSelect(nodes: MenuResponse[], prefix = ''): Array<{ menuId: number; label: string }> {
  const result: Array<{ menuId: number; label: string }> = []
  for (const n of nodes) {
    if (n.menuType === 'F') continue
    const label = prefix ? `${prefix} / ${n.menuName}` : n.menuName
    result.push({ menuId: n.menuId, label })
    if (n.children?.length) result.push(...flattenForSelect(n.children, label))
  }
  return result
}

const menuTypeMap: Record<string, { label: string; variant: 'default' | 'success' | 'secondary' }> = {
  M: { label: '目录', variant: 'default' },
  C: { label: '菜单', variant: 'success' },
  F: { label: '按钮', variant: 'secondary' },
}

onMounted(loadMenus)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">菜单名称</span>
        <input v-model="query.menuName" placeholder="菜单名称" class="h-7 w-[160px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadMenus"><Search :size="12" /> 搜索</button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery"><RefreshCw :size="12" /> 重置</button>
    </div>
    <div class="px-4 py-2.5 border-b border-border">
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="openAdd()"><Plus :size="12" /> 新增</button>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">菜单名称</th>
            <th class="text-center text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">类型</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-16">排序</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">权限标识</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">组件路径</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium w-20">状态</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="7" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else v-for="node in flattenMenus(menuTree)" :key="node.menuId" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2 text-[12px] text-foreground" :style="{ paddingLeft: `${16 + node.depth * 16}px` }">
              <span v-if="node.depth > 0" class="text-muted-foreground mr-1">└</span>{{ node.menuName }}
            </td>
            <td class="px-4 py-2 text-center">
              <Badge :variant="menuTypeMap[node.menuType]?.variant || 'secondary'">{{ menuTypeMap[node.menuType]?.label }}</Badge>
            </td>
            <td class="px-4 py-2 text-[12px] text-right font-mono-tnum text-muted-foreground">{{ node.orderNum }}</td>
            <td class="px-4 py-2 text-[11px] font-mono-tnum text-muted-foreground truncate max-w-[180px]">{{ node.permission || '-' }}</td>
            <td class="px-4 py-2 text-[11px] font-mono-tnum text-muted-foreground truncate max-w-[180px]">{{ node.component || '-' }}</td>
            <td class="px-4 py-2"><Badge :variant="node.status === 1 ? 'success' : 'danger'">{{ node.status === 1 ? '正常' : '停用' }}</Badge></td>
            <td class="px-4 py-2 text-right">
              <div class="flex items-center justify-end gap-1">
                <button v-if="node.menuType !== 'F'" class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openAdd(node.menuId)"><Plus :size="10" /> 新增</button>
                <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openEdit(node)"><Edit :size="10" /> 编辑</button>
                <button class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleDelete(node)"><Trash2 :size="10" /> 删除</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

  <Dialog v-model:open="dialogOpen">
    <DialogContent class="w-[560px]">
      <DialogHeader><DialogTitle>{{ isEdit ? '编辑菜单' : '新增菜单' }}</DialogTitle></DialogHeader>
      <div class="grid grid-cols-2 gap-3 py-2">
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">上级菜单</label>
          <select v-model="formData.parentId" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option :value="0">顶级菜单</option>
            <option v-for="opt in flattenForSelect(menuTreeForSelect)" :key="opt.menuId" :value="opt.menuId">{{ opt.label }}</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">菜单名称 <span class="text-danger">*</span></label>
          <input v-model="formData.menuName" placeholder="请输入菜单名称" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">菜单类型</label>
          <select v-model="formData.menuType" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option value="M">目录</option>
            <option value="C">菜单</option>
            <option value="F">按钮</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">路由地址</label>
          <input v-model="formData.path" placeholder="/system/user" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">组件路径</label>
          <input v-model="formData.component" placeholder="system/user/index" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">权限标识</label>
          <input v-model="formData.permission" placeholder="system:user:list" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">排序</label>
          <input v-model.number="formData.orderNum" type="number" min="0" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">状态</label>
          <div class="flex gap-4">
            <label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer"><input v-model="formData.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" /><span class="text-[12px] text-foreground">{{ opt.l }}</span></label>
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
