<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Trash2, Edit } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { DictTypeResponse, DictDataResponse } from '@/api/system/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const dictTypes = ref<DictTypeResponse[]>([])
const dictData = ref<DictDataResponse[]>([])
const total = ref(0)
const totalData = ref(0)
const selectedTypeIds = ref<number[]>([])
const selectedDataIds = ref<number[]>([])
const activeType = ref<DictTypeResponse | null>(null)
const typeDialogOpen = ref(false)
const dataDialogOpen = ref(false)
const isEditType = ref(false)
const isEditData = ref(false)
const submitTypeError = ref('')
const submitDataError = ref('')

const typeQuery = reactive({ pageNum: 1, pageSize: 10, dictName: '', dictType: '' })
const dataQuery = reactive({ pageNum: 1, pageSize: 10 })

interface TypeForm { dictId?: number; dictName: string; dictType: string; status: 0 | 1; remark: string }
interface DataForm { dictCode?: number; dictType: string; dictLabel: string; dictValue: string; dictSort: number; status: 0 | 1; remark: string }

const typeForm = reactive<TypeForm>({ dictName: '', dictType: '', status: 1, remark: '' })
const dataForm = reactive<DataForm>({ dictType: '', dictLabel: '', dictValue: '', dictSort: 0, status: 1, remark: '' })

async function loadTypes() {
  loading.value = true
  try {
    const data = await get<PagedResult<DictTypeResponse>>('/system/dict/type/list', typeQuery)
    dictTypes.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

async function loadData(type: DictTypeResponse) {
  activeType.value = type
  const data = await get<PagedResult<DictDataResponse>>('/system/dict/data/list', { ...dataQuery, dictType: type.dictType })
  dictData.value = data.rows
  totalData.value = data.total
}

function openAddType() {
  isEditType.value = false
  Object.assign(typeForm, { dictName: '', dictType: '', status: 1, remark: '' })
  delete typeForm.dictId
  submitTypeError.value = ''
  typeDialogOpen.value = true
}

function openEditType(row: DictTypeResponse) {
  isEditType.value = true
  Object.assign(typeForm, { dictId: row.dictId, dictName: row.dictName, dictType: row.dictType, status: row.status, remark: '' })
  submitTypeError.value = ''
  typeDialogOpen.value = true
}

async function handleSubmitType() {
  if (!typeForm.dictName || !typeForm.dictType) { submitTypeError.value = '请填写字典名称和编码'; return }
  submitTypeError.value = ''
  submitting.value = true
  try {
    if (isEditType.value) { await put('/system/dict/type', typeForm) }
    else { await post('/system/dict/type', typeForm) }
    typeDialogOpen.value = false
    loadTypes()
  } catch { submitTypeError.value = '操作失败' } finally { submitting.value = false }
}

async function handleDeleteType(row: DictTypeResponse) {
  if (!confirm(`确认删除字典 "${row.dictName}" 吗？`)) return
  await del(`/system/dict/type/${row.dictId}`)
  if (activeType.value?.dictId === row.dictId) { activeType.value = null; dictData.value = [] }
  loadTypes()
}

function openAddData() {
  if (!activeType.value) return
  isEditData.value = false
  Object.assign(dataForm, { dictType: activeType.value.dictType, dictLabel: '', dictValue: '', dictSort: 0, status: 1, remark: '' })
  delete dataForm.dictCode
  submitDataError.value = ''
  dataDialogOpen.value = true
}

function openEditData(row: DictDataResponse) {
  isEditData.value = true
  Object.assign(dataForm, { dictCode: row.dictCode, dictType: row.dictType, dictLabel: row.dictLabel, dictValue: row.dictValue, dictSort: row.dictSort, status: row.status, remark: '' })
  submitDataError.value = ''
  dataDialogOpen.value = true
}

async function handleSubmitData() {
  if (!dataForm.dictLabel || !dataForm.dictValue) { submitDataError.value = '请填写标签和键值'; return }
  submitDataError.value = ''
  submitting.value = true
  try {
    if (isEditData.value) { await put('/system/dict/data', dataForm) }
    else { await post('/system/dict/data', dataForm) }
    dataDialogOpen.value = false
    if (activeType.value) loadData(activeType.value)
  } catch { submitDataError.value = '操作失败' } finally { submitting.value = false }
}

async function handleDeleteData(row: DictDataResponse) {
  if (!confirm(`确认删除 "${row.dictLabel}" 吗？`)) return
  await del(`/system/dict/data/${row.dictCode}`)
  if (activeType.value) loadData(activeType.value)
}

onMounted(loadTypes)
</script>

<template>
  <div class="flex gap-3">
    <!-- Left: dict type list -->
    <div class="w-[420px] shrink-0 bg-card border border-border rounded-lg overflow-hidden flex flex-col">
      <div class="flex items-center gap-2 px-3 py-3 border-b border-border flex-wrap">
        <input v-model="typeQuery.dictName" placeholder="字典名称" class="h-7 flex-1 min-w-[80px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        <input v-model="typeQuery.dictType" placeholder="字典编码" class="h-7 flex-1 min-w-[80px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        <button class="h-7 px-2 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1 hover:opacity-90 cursor-pointer border-0" @click="loadTypes"><Search :size="12" /></button>
      </div>
      <div class="px-3 py-2 border-b border-border">
        <button class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="openAddType"><Plus :size="12" /> 新增</button>
      </div>
      <div class="flex-1 overflow-auto">
        <table class="w-full">
          <thead>
            <tr class="bg-subtle border-b border-border">
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">字典名称</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">字典编码</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium w-16">状态</th>
              <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading"><td colspan="4" class="py-6 text-center text-[12px] text-muted-foreground">加载中...</td></tr>
            <tr v-else v-for="row in dictTypes" :key="row.dictId"
              :class="['border-b border-border last:border-0 cursor-pointer', activeType?.dictId === row.dictId ? 'bg-primary-soft' : 'hover:bg-subtle']"
              @click="loadData(row)"
            >
              <td class="px-3 py-2 text-[12px] text-foreground font-medium">{{ row.dictName }}</td>
              <td class="px-3 py-2 text-[11px] font-mono-tnum text-muted-foreground">{{ row.dictType }}</td>
              <td class="px-3 py-2"><Badge :variant="row.status === 1 ? 'success' : 'danger'" class="text-[10px]">{{ row.status === 1 ? '正常' : '停用' }}</Badge></td>
              <td class="px-3 py-2 text-right">
                <div class="flex items-center justify-end gap-1">
                  <button class="h-6 px-1.5 text-[10px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent" @click.stop="openEditType(row)"><Edit :size="10" /></button>
                  <button class="h-6 px-1.5 text-[10px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent" @click.stop="handleDeleteType(row)"><Trash2 :size="10" /></button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Right: dict data list -->
    <div class="flex-1 min-w-0 bg-card border border-border rounded-lg overflow-hidden flex flex-col">
      <div class="px-4 py-3 border-b border-border flex items-center justify-between">
        <span class="text-[13px] font-semibold text-foreground">
          {{ activeType ? `${activeType.dictName} 字典数据` : '请选择字典类型' }}
        </span>
        <button v-if="activeType" class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0" @click="openAddData"><Plus :size="12" /> 新增</button>
      </div>
      <div class="flex-1 overflow-auto">
        <table class="w-full">
          <thead>
            <tr class="bg-subtle border-b border-border">
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">标签名</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">键值</th>
              <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">排序</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">状态</th>
              <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!activeType"><td colspan="5" class="py-8 text-center text-[12px] text-muted-foreground">← 请先选择左侧字典类型</td></tr>
            <tr v-else-if="!dictData.length"><td colspan="5" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td></tr>
            <tr v-else v-for="row in dictData" :key="row.dictCode" class="border-b border-border last:border-0 hover:bg-subtle">
              <td class="px-4 py-2 text-[12px] text-foreground font-medium">{{ row.dictLabel }}</td>
              <td class="px-4 py-2 text-[12px] font-mono-tnum text-muted-foreground">{{ row.dictValue }}</td>
              <td class="px-4 py-2 text-[12px] text-right font-mono-tnum text-muted-foreground">{{ row.dictSort }}</td>
              <td class="px-4 py-2"><Badge :variant="row.status === 1 ? 'success' : 'danger'">{{ row.status === 1 ? '正常' : '停用' }}</Badge></td>
              <td class="px-4 py-2 text-right">
                <div class="flex items-center justify-end gap-1">
                  <button class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="openEditData(row)"><Edit :size="10" /> 编辑</button>
                  <button class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1" @click="handleDeleteData(row)"><Trash2 :size="10" /> 删除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>

  <!-- Dict type dialog -->
  <Dialog v-model:open="typeDialogOpen">
    <DialogContent class="w-[480px]">
      <DialogHeader><DialogTitle>{{ isEditType ? '编辑字典类型' : '新增字典类型' }}</DialogTitle></DialogHeader>
      <div class="space-y-3 py-2">
        <div><label class="block text-[12px] font-medium text-foreground mb-1">字典名称 <span class="text-danger">*</span></label><input v-model="typeForm.dictName" placeholder="请输入字典名称" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">字典编码 <span class="text-danger">*</span></label><input v-model="typeForm.dictType" placeholder="如：sys_user_sex" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">状态</label><div class="flex gap-4"><label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer"><input v-model="typeForm.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" /><span class="text-[12px] text-foreground">{{ opt.l }}</span></label></div></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">备注</label><textarea v-model="typeForm.remark" rows="2" class="w-full px-2.5 py-1.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring resize-none" /></div>
      </div>
      <p v-if="submitTypeError" class="text-[12px] text-danger">{{ submitTypeError }}</p>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="typeDialogOpen = false">取消</button>
        <button :disabled="submitting" class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSubmitType">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>

  <!-- Dict data dialog -->
  <Dialog v-model:open="dataDialogOpen">
    <DialogContent class="w-[480px]">
      <DialogHeader><DialogTitle>{{ isEditData ? '编辑字典数据' : '新增字典数据' }}</DialogTitle></DialogHeader>
      <div class="space-y-3 py-2">
        <div><label class="block text-[12px] font-medium text-foreground mb-1">标签名 <span class="text-danger">*</span></label><input v-model="dataForm.dictLabel" placeholder="如：男" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">键值 <span class="text-danger">*</span></label><input v-model="dataForm.dictValue" placeholder="如：1" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">排序</label><input v-model.number="dataForm.dictSort" type="number" min="0" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" /></div>
        <div><label class="block text-[12px] font-medium text-foreground mb-1">状态</label><div class="flex gap-4"><label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer"><input v-model="dataForm.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" /><span class="text-[12px] text-foreground">{{ opt.l }}</span></label></div></div>
      </div>
      <p v-if="submitDataError" class="text-[12px] text-danger">{{ submitDataError }}</p>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="dataDialogOpen = false">取消</button>
        <button :disabled="submitting" class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSubmitData">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
