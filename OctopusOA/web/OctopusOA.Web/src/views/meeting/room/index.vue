<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Plus } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'

import { toast, confirm } from '@/components/ui/toast'
import { getRoomList, createRoom, updateRoom, deleteRoom } from '@/api/meeting'
import type { MeetingRoom } from '@/api/meeting/types'

const loading = ref(false)
const submitting = ref(false)
const rooms = ref<MeetingRoom[]>([])
const formVisible = ref(false)
const isEdit = ref(false)
const nameError = ref('')
const capacityError = ref('')
const equipmentInput = ref('')

const formData = reactive<MeetingRoom>({
  id: 0, name: '', capacity: 6, location: null,
  equipment: [], description: null, imageUrl: null,
  status: 1, createTime: '',
})

const EQUIPMENT_OPTIONS = ['投影', '视频会议', '白板', '电视', '电话']

function toggleEquipment(item: string) {
  const idx = formData.equipment.indexOf(item)
  if (idx >= 0) formData.equipment.splice(idx, 1)
  else formData.equipment.push(item)
}

async function load() {
  loading.value = true
  try { rooms.value = await getRoomList() } finally { loading.value = false }
}

function openCreate() {
  isEdit.value = false; nameError.value = ''; capacityError.value = ''
  Object.assign(formData, { id: 0, name: '', capacity: 6, location: '', equipment: [], description: '', imageUrl: '', status: 1 })
  formVisible.value = true
}

function openEdit(row: MeetingRoom) {
  isEdit.value = true; nameError.value = ''; capacityError.value = ''
  Object.assign(formData, { ...row, equipment: [...row.equipment] })
  formVisible.value = true
}

async function handleSubmit() {
  nameError.value = formData.name.trim() ? '' : '请输入名称'
  capacityError.value = formData.capacity > 0 ? '' : '请输入容量'
  if (nameError.value || capacityError.value) return
  submitting.value = true
  try {
    if (isEdit.value) { await updateRoom(formData); toast('修改成功', 'success') }
    else { await createRoom(formData); toast('创建成功', 'success') }
    formVisible.value = false; await load()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

async function handleDelete(row: MeetingRoom) {
  try {
    await confirm(`确认删除会议室「${row.name}」？`)
    await deleteRoom(row.id); toast('已删除', 'success'); await load()
  } catch { /* cancelled */ }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="会议室管理">
      <template #actions>
        <button class="h-[var(--row-h)] px-3 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 flex items-center gap-1" @click="openCreate">
          <Plus :size="13" />新建会议室
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!rooms.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无会议室</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">名称</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">容量</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-28">位置</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">设备</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">说明</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in rooms" :key="row.id" class="hover:bg-subtle">
              <td class="px-3 py-2 font-medium">{{ row.name }}</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.capacity }} 人</td>
              <td class="px-3 py-2 text-muted-foreground">{{ row.location || '-' }}</td>
              <td class="px-3 py-2">
                <div class="flex flex-wrap gap-1">
                  <Badge v-for="eq in row.equipment" :key="eq" variant="secondary">{{ eq }}</Badge>
                </div>
              </td>
              <td class="px-3 py-2 text-muted-foreground truncate max-w-[180px]">{{ row.description || '-' }}</td>
              <td class="px-3 py-2">
                <Badge :variant="row.status === 1 ? 'success' : 'secondary'">{{ row.status === 1 ? '启用' : '停用' }}</Badge>
              </td>
              <td class="px-3 py-2 whitespace-nowrap">
                <div class="flex items-center gap-2">
                  <button class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openEdit(row)">编辑</button>
                  <button class="text-[11px] text-danger hover:underline cursor-pointer border-0 bg-transparent" @click="handleDelete(row)">删除</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Dialog v-model:open="formVisible">
      <DialogContent class="p-5 w-[520px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">{{ isEdit ? '编辑会议室' : '新建会议室' }}</DialogTitle>
        </DialogHeader>
        <div class="space-y-3">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 名称</label>
              <input v-model="formData.name" type="text" placeholder="如 星光厅" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', nameError ? 'border-danger' : 'border-input']" />
              <p v-if="nameError" class="text-[11px] text-danger mt-0.5">{{ nameError }}</p>
            </div>
            <div>
              <label class="block text-[12px] font-medium mb-1"><span class="text-danger">*</span> 容量（人）</label>
              <input v-model.number="formData.capacity" type="number" min="1" max="100" :class="['w-full h-[var(--row-h)] px-3 border rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring', capacityError ? 'border-danger' : 'border-input']" />
              <p v-if="capacityError" class="text-[11px] text-danger mt-0.5">{{ capacityError }}</p>
            </div>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">位置</label>
            <input v-model="formData.location!" type="text" placeholder="如 A 楼 8F" class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" />
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1.5">设备</label>
            <div class="flex flex-wrap gap-2">
              <button
                v-for="eq in EQUIPMENT_OPTIONS"
                :key="eq"
                :class="['px-3 py-1 text-[11px] rounded-full border cursor-pointer transition-colors', formData.equipment.includes(eq) ? 'bg-primary/10 text-primary border-primary/30' : 'bg-card text-muted-foreground border-border hover:border-primary/30']"
                @click="toggleEquipment(eq)"
              >{{ eq }}</button>
            </div>
            <div class="flex gap-2 mt-2">
              <input v-model="equipmentInput" type="text" placeholder="自定义设备" class="flex-1 h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring" @keyup.enter="() => { if (equipmentInput.trim()) { toggleEquipment(equipmentInput.trim()); equipmentInput = '' } }" />
              <button class="h-[var(--row-h)] px-3 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="() => { if (equipmentInput.trim()) { toggleEquipment(equipmentInput.trim()); equipmentInput = '' } }">添加</button>
            </div>
          </div>
          <div>
            <label class="block text-[12px] font-medium mb-1">说明</label>
            <textarea v-model="formData.description!" rows="2" class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] resize-none focus:outline-none focus:ring-2 focus:ring-ring" />
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
  </div>
</template>
