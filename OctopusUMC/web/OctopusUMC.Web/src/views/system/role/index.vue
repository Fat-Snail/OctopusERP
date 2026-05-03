<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Trash2, Edit, ShieldCheck } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { RoleResponse, MenuResponse } from '@/api/system/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const roles = ref<RoleResponse[]>([])
const total = ref(0)
const selectedIds = ref<number[]>([])
const dialogOpen = ref(false)
const menuDialogOpen = ref(false)
const isEdit = ref(false)
const submitError = ref('')
const menuTree = ref<MenuResponse[]>([])
const selectedMenuIds = ref<number[]>([])
const bindingRoleId = ref(0)

interface RoleForm {
  roleId?: number
  roleName: string
  roleKey: string
  roleSort: number
  dataScope: '1' | '2' | '3' | '4' | '5'
  status: 0 | 1
  remark: string
}

const query = reactive({ pageNum: 1, pageSize: 10, roleName: '', roleKey: '', status: undefined as number | undefined })
const formData = reactive<RoleForm>({ roleName: '', roleKey: '', roleSort: 0, dataScope: '1', status: 1, remark: '' })

const dataScopeOptions = [
  { value: '1', label: '全部数据权限' },
  { value: '2', label: '本部门及子部门' },
  { value: '3', label: '本部门数据' },
  { value: '4', label: '仅本人数据' },
  { value: '5', label: '自定义数据权限' },
]

async function loadRoles() {
  loading.value = true
  try {
    const data = await get<PagedResult<RoleResponse>>('/system/role/list', query)
    roles.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

function resetQuery() { query.roleName = ''; query.roleKey = ''; query.status = undefined; loadRoles() }

function toggleSelect(id: number) {
  const idx = selectedIds.value.indexOf(id)
  if (idx >= 0) selectedIds.value.splice(idx, 1)
  else selectedIds.value.push(id)
}
function toggleSelectAll() {
  selectedIds.value = selectedIds.value.length === roles.value.length ? [] : roles.value.map(r => r.roleId)
}

function openAdd() {
  isEdit.value = false
  Object.assign(formData, { roleName: '', roleKey: '', roleSort: 0, dataScope: '1', status: 1, remark: '' })
  delete formData.roleId
  submitError.value = ''
  dialogOpen.value = true
}

function openEdit(row: RoleResponse) {
  isEdit.value = true
  Object.assign(formData, { roleId: row.roleId, roleName: row.roleName, roleKey: row.roleKey, roleSort: row.roleSort, dataScope: row.dataScope, status: row.status, remark: row.remark ?? '' })
  submitError.value = ''
  dialogOpen.value = true
}

async function handleSubmit() {
  if (!formData.roleName) { submitError.value = '请输入角色名称'; return }
  if (!formData.roleKey) { submitError.value = '请输入权限字符'; return }
  submitError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) { await put('/system/role', formData) }
    else { await post('/system/role', { ...formData, menuIds: [] }) }
    dialogOpen.value = false
    loadRoles()
  } catch { submitError.value = '操作失败' } finally { submitting.value = false }
}

async function handleDelete(row: RoleResponse) {
  if (!confirm(`确认删除角色 "${row.roleName}" 吗？`)) return
  await del(`/system/role/${row.roleId}`)
  loadRoles()
}

async function handleBatchDelete() {
  if (!selectedIds.value.length || !confirm(`确认删除 ${selectedIds.value.length} 个角色吗？`)) return
  await del(`/system/role/${selectedIds.value.join(',')}`)
  selectedIds.value = []
  loadRoles()
}

async function openMenuDialog(row: RoleResponse) {
  if (!menuTree.value.length) {
    const data = await get<MenuResponse[]>('/system/menu/tree')
    menuTree.value = data
  }
  bindingRoleId.value = row.roleId
  selectedMenuIds.value = [...(row.menuIds ?? [])]
  menuDialogOpen.value = true
}

async function handleBindMenus() {
  await post('/system/role/menu', { roleId: bindingRoleId.value, menuIds: selectedMenuIds.value })
  menuDialogOpen.value = false
  loadRoles()
}

function toggleMenuId(id: number) {
  const idx = selectedMenuIds.value.indexOf(id)
  if (idx >= 0) selectedMenuIds.value.splice(idx, 1)
  else selectedMenuIds.value.push(id)
}

function flattenMenus(nodes: MenuResponse[], depth = 0): Array<MenuResponse & { depth: number }> {
  const result: Array<MenuResponse & { depth: number }> = []
  for (const n of nodes) {
    result.push({ ...n, depth })
    if (n.children?.length) result.push(...flattenMenus(n.children, depth + 1))
  }
  return result
}

onMounted(loadRoles)
</script>

<template>
  <div class="bg-card border border-border rounded-lg overflow-hidden">
    <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">角色名称</span>
        <input v-model="query.roleName" placeholder="角色名称" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <div class="flex items-center gap-1.5">
        <span class="text-[12px] text-muted-foreground">权限字符</span>
        <input v-model="query.roleKey" placeholder="权限字符" class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
      </div>
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="loadRoles"><Search :size="12" /> 搜索</button>
      <button class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border" @click="resetQuery"><RefreshCw :size="12" /> 重置</button>
    </div>
    <div class="flex items-center gap-2 px-4 py-2.5 border-b border-border">
      <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="openAdd"><Plus :size="12" /> 新增</button>
      <button :disabled="!selectedIds.length" class="h-7 px-3 bg-danger/10 text-danger text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-danger/20 cursor-pointer border border-danger/20 disabled:opacity-40" @click="handleBatchDelete"><Trash2 :size="12" /> 删除</button>
    </div>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="w-10 px-3 py-2.5 text-center"><input type="checkbox" :checked="selectedIds.length === roles.length && roles.length > 0" class="w-3.5 h-3.5 rounded border-border cursor-pointer" @change="toggleSelectAll" /></th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">角色名称</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">权限字符</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">排序</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">数据范围</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">状态</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">创建时间</th>
            <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="loading"><td colspan="8" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
          <tr v-else-if="!roles.length"><td colspan="8" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
          <tr v-else v-for="row in roles" :key="row.roleId" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-3 py-2 text-center"><input type="checkbox" :checked="selectedIds.includes(row.roleId)" class="w-3.5 h-3.5 rounded border-border cursor-pointer" @change="toggleSelect(row.roleId)" /></td>
            <td class="px-4 py-2 text-[12px] text-foreground font-medium">{{ row.roleName }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ row.roleKey }}</td>
            <td class="px-4 py-2 text-[12px] text-right font-mono-tnum text-muted-foreground">{{ row.roleSort }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ dataScopeOptions.find(o => o.value === row.dataScope)?.label }}</td>
            <td class="px-4 py-2"><Badge :variant="row.status === 1 ? 'success' : 'danger'">{{ row.status === 1 ? '正常' : '停用' }}</Badge></td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.createTime?.slice(0, 10) }}</td>
            <td class="px-4 py-2 text-right">
              <div class="flex items-center justify-end gap-1">
                <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openEdit(row)"><Edit :size="11" /> 编辑</button>
                <button class="h-6 px-2 text-[11px] text-info hover:bg-info/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openMenuDialog(row)"><ShieldCheck :size="11" /> 权限</button>
                <button class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleDelete(row)"><Trash2 :size="11" /> 删除</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div class="flex items-center justify-between px-4 py-2.5 border-t border-border bg-card">
      <span class="text-[12px] text-muted-foreground">共 {{ total }} 条</span>
      <div class="flex items-center gap-1">
        <button :disabled="query.pageNum <= 1" class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80" @click="() => { query.pageNum--; loadRoles() }">上一页</button>
        <span class="h-7 px-3 flex items-center text-[12px] text-foreground">{{ query.pageNum }}</span>
        <button :disabled="query.pageNum * query.pageSize >= total" class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80" @click="() => { query.pageNum++; loadRoles() }">下一页</button>
      </div>
    </div>
  </div>

  <!-- Role form dialog -->
  <Dialog v-model:open="dialogOpen">
    <DialogContent class="w-[520px]">
      <DialogHeader><DialogTitle>{{ isEdit ? '编辑角色' : '新增角色' }}</DialogTitle></DialogHeader>
      <div class="grid grid-cols-2 gap-3 py-2">
        <div><label class="block text-[12px] font-medium text-foreground mb-1">角色名称 <span class="text-danger">*</span></label><input v-model="formData.roleName" placeholder="角色名称" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">权限字符 <span class="text-danger">*</span></label><input v-model="formData.roleKey" placeholder="如：admin" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">排序</label><input v-model.number="formData.roleSort" type="number" min="0" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">数据范围</label>
          <select v-model="formData.dataScope" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option v-for="o in dataScopeOptions" :key="o.value" :value="o.value">{{ o.label }}</option>
          </select>
        </div>
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">状态</label>
          <div class="flex gap-4">
            <label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer"><input v-model="formData.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" /><span class="text-[12px] text-foreground">{{ opt.l }}</span></label>
          </div>
        </div>
        <div class="col-span-2"><label class="block text-[12px] font-medium text-foreground mb-1">备注</label><textarea v-model="formData.remark" rows="2" class="w-full px-2.5 py-1.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring resize-none" /></div>
      </div>
      <p v-if="submitError" class="text-[12px] text-danger">{{ submitError }}</p>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="dialogOpen = false">取消</button>
        <button :disabled="submitting" class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSubmit">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>

  <!-- Menu permission dialog -->
  <Dialog v-model:open="menuDialogOpen">
    <DialogContent class="w-[480px]">
      <DialogHeader><DialogTitle>分配菜单权限</DialogTitle></DialogHeader>
      <div class="max-h-[400px] overflow-y-auto border border-border rounded-[var(--radius)] py-1">
        <label
          v-for="node in flattenMenus(menuTree)"
          :key="node.menuId"
          :style="{ paddingLeft: `${12 + node.depth * 16}px` }"
          class="flex items-center gap-2 h-[var(--row-h)] pr-3 hover:bg-subtle cursor-pointer"
        >
          <input type="checkbox" :checked="selectedMenuIds.includes(node.menuId)" class="w-3.5 h-3.5 rounded border-border cursor-pointer" @change="toggleMenuId(node.menuId)" />
          <span class="text-[12px] text-foreground">{{ node.menuName }}</span>
          <Badge :variant="node.menuType === 'M' ? 'default' : node.menuType === 'C' ? 'success' : 'secondary'" class="ml-auto">
            {{ node.menuType === 'M' ? '目录' : node.menuType === 'C' ? '菜单' : '按钮' }}
          </Badge>
        </label>
      </div>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="menuDialogOpen = false">取消</button>
        <button class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 cursor-pointer border-0" @click="handleBindMenus">保存</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
