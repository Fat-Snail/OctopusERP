<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Plus, Trash2 } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'

import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'
import { toast, confirm } from '@/components/ui/toast'
import { getTemplateList, createTemplate, updateTemplate, deleteTemplate, setTemplateNodes } from '@/api/approval'
import type { TemplateResponse, NodeRequest } from '@/api/approval/types'

const loading = ref(false)
const submitting = ref(false)
const list = ref<TemplateResponse[]>([])
const formVisible = ref(false)
const nodesVisible = ref(false)
const isEdit = ref(false)
const currentTemplateId = ref(0)
const nameError = ref('')

const formData = reactive({
  templateId: 0,
  templateName: '',
  templateCode: '',
  description: '',
  formSchema: '{"fields":[]}',
  status: 1,
})

const editNodes = ref<NodeRequest[]>([])

async function loadData() {
  loading.value = true
  try { list.value = await getTemplateList() } finally { loading.value = false }
}

function openAdd() {
  isEdit.value = false
  nameError.value = ''
  Object.assign(formData, { templateId: 0, templateName: '', templateCode: '', description: '', formSchema: '{"fields":[]}', status: 1 })
  formVisible.value = true
}

function openEdit(row: TemplateResponse) {
  isEdit.value = true
  nameError.value = ''
  Object.assign(formData, { templateId: row.templateId, templateName: row.templateName, templateCode: row.templateCode, description: row.description || '', status: row.status })
  formVisible.value = true
}

function openNodes(row: TemplateResponse) {
  currentTemplateId.value = row.templateId
  editNodes.value = row.nodes.map(n => ({ nodeName: n.nodeName, nodeOrder: n.nodeOrder, approverType: n.approverType, approverValue: n.approverValue || '' }))
  nodesVisible.value = true
}

async function handleSubmit() {
  if (!formData.templateName.trim()) { nameError.value = '请输入模板名称'; return }
  nameError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) {
      await updateTemplate({ ...formData })
      toast('修改成功', 'success')
    } else {
      await createTemplate({ ...formData })
      toast('创建成功', 'success')
    }
    formVisible.value = false
    loadData()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

async function handleSaveNodes() {
  const nodes = editNodes.value.map((n, i) => ({ ...n, nodeOrder: i + 1 }))
  submitting.value = true
  try {
    await setTemplateNodes(currentTemplateId.value, nodes)
    toast('节点保存成功', 'success')
    nodesVisible.value = false
    loadData()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

async function handleDelete(row: TemplateResponse) {
  try {
    await confirm(`确认删除模板「${row.templateName}」？`)
    await deleteTemplate(row.templateId)
    toast('删除成功', 'success')
    loadData()
  } catch { /* cancelled */ }
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="流程模板管理">
      <template #actions>
        <button
          class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 flex items-center gap-1"
          @click="openAdd"
        >
          <Plus :size="13" />新增模板
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!list.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无模板</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">模板名称</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">编码</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">描述</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">节点数</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in list" :key="row.templateId" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium">{{ row.templateName }}</td>
              <td class="px-3 py-2 text-muted-foreground font-mono text-[11px]">{{ row.templateCode }}</td>
              <td class="px-3 py-2 text-muted-foreground truncate max-w-[200px]">{{ row.description || '-' }}</td>
              <td class="px-3 py-2 text-center">{{ row.nodes.length }}</td>
              <td class="px-3 py-2">
                <Badge :variant="row.status === 1 ? 'success' : 'secondary'">{{ row.status === 1 ? '启用' : '停用' }}</Badge>
              </td>
              <td class="px-3 py-2">
                <div class="flex items-center gap-2">
                  <button class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openEdit(row)">编辑</button>
                  <button class="text-[11px] text-warning hover:underline cursor-pointer border-0 bg-transparent" @click="openNodes(row)">节点</button>
                  <button class="text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent" @click="handleDelete(row)">删除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Template form modal -->
    <Dialog v-model:open="formVisible">
      <DialogContent class="p-5 w-[480px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">{{ isEdit ? '编辑模板' : '新增模板' }}</DialogTitle>
        </DialogHeader>
        <div class="space-y-3">
          <div>
            <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 模板名称</label>
            <input v-model="formData.templateName" type="text" class="w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" :class="nameError ? 'border-danger' : 'border-input'" />
            <p v-if="nameError" class="text-[11px] text-danger mt-0.5">{{ nameError }}</p>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">模板编码</label>
            <input v-if="isEdit" v-model="formData.templateCode" disabled type="text" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-subtle text-[12px]" />
            <p v-else class="text-[12px] text-muted-foreground">系统自动生成</p>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">描述</label>
            <textarea v-model="formData.description" rows="2" class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">状态</label>
            <div class="flex gap-4 text-[12px]">
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input type="radio" :value="1" v-model="formData.status" class="accent-primary" />启用
              </label>
              <label class="flex items-center gap-1.5 cursor-pointer">
                <input type="radio" :value="0" v-model="formData.status" class="accent-primary" />停用
              </label>
            </div>
          </div>
        </div>
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="formVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleSubmit">
            {{ submitting ? '保存中...' : '确定' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>

    <!-- Nodes modal -->
    <Dialog v-model:open="nodesVisible">
      <DialogContent class="p-5 w-[640px] max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">编辑审批节点</DialogTitle>
        </DialogHeader>
        <div class="space-y-2 mb-3">
          <div v-for="(node, idx) in editNodes" :key="idx" class="flex items-center gap-2">
            <span class="w-6 h-6 rounded-full bg-primary-soft text-primary-soft-fg text-[11px] flex items-center justify-center shrink-0 font-semibold">{{ idx + 1 }}</span>
            <input v-model="node.nodeName" type="text" placeholder="节点名称" class="h-[var(--row-h)] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring w-32" />
            <Select :model-value="node.approverType" @update:model-value="node.approverType = String($event)">
              <SelectTrigger class="w-28">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="role">OA角色</SelectItem>
                <SelectItem value="user">指定用户</SelectItem>
                <SelectItem value="dept_leader">部门主管</SelectItem>
              </SelectContent>
            </Select>
            <input
              v-model="node.approverValue"
              type="text"
              placeholder="角色/用户ID"
              :disabled="node.approverType === 'dept_leader'"
              class="h-[var(--row-h)] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring w-28 disabled:bg-subtle disabled:text-muted-foreground"
            />
            <button class="p-1.5 text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent" @click="editNodes.splice(idx, 1)">
              <Trash2 :size="13" />
            </button>
          </div>
        </div>
        <button
          class="text-[12px] text-primary hover:underline cursor-pointer border-0 bg-transparent"
          @click="editNodes.push({ nodeName: '', nodeOrder: editNodes.length + 1, approverType: 'role', approverValue: '' })"
        >+ 添加节点</button>
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="nodesVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleSaveNodes">
            {{ submitting ? '保存中...' : '保存节点' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
