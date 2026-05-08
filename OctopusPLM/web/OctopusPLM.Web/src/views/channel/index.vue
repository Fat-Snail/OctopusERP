<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { Plus, Pencil, Trash2, ArrowRight, Globe, Tag } from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import InputVue from '@/components/ui/input.vue'
import BadgeVue from '@/components/ui/badge.vue'
import { getCategoryTree } from '@/api/category'
import type { CategoryTreeNode } from '@/api/category/types'
import {
  getChannelList, createChannel, updateChannel, deleteChannel,
  getChannelMappings, createChannelMapping, updateChannelMapping, deleteChannelMapping,
  createAttrMapping, updateAttrMapping, deleteAttrMapping,
} from '@/api/channel'
import { getAttributeList } from '@/api/category'
import type { ChannelDef, ChannelCategoryMappingItem, ChannelAttrMappingItem } from '@/api/channel/types'
import type { PlmAttribute } from '@/api/category/types'

// ── Channels ──────────────────────────────────────────────────────────────────
const channels = ref<ChannelDef[]>([])
const selectedChannel = ref<ChannelDef | null>(null)
const channelLoading = ref(false)

async function loadChannels() {
  channelLoading.value = true
  try { channels.value = await getChannelList() }
  catch (e: unknown) { console.error((e as Error).message) }
  finally { channelLoading.value = false }
}

function selectChannel(ch: ChannelDef) {
  if (selectedChannel.value?.channelId === ch.channelId) return
  selectedChannel.value = ch
  selectedMapping.value = null
}

// ── Channel dialog ────────────────────────────────────────────────────────────
const channelDialog = ref<{
  visible: boolean
  mode: 'create' | 'edit'
  channelId: number
  channelCode: string
  channelName: string
  status: number
  loading: boolean
}>({ visible: false, mode: 'create', channelId: 0, channelCode: '', channelName: '', status: 1, loading: false })

function openCreateChannel() {
  channelDialog.value = { visible: true, mode: 'create', channelId: 0, channelCode: '', channelName: '', status: 1, loading: false }
}

function openEditChannel(ch: ChannelDef) {
  channelDialog.value = { visible: true, mode: 'edit', channelId: ch.channelId, channelCode: ch.channelCode, channelName: ch.channelName, status: ch.status, loading: false }
}

async function saveChannel() {
  const d = channelDialog.value
  if (!d.channelName.trim()) return
  if (d.mode === 'create' && !d.channelCode.trim()) return
  d.loading = true
  try {
    if (d.mode === 'create') {
      await createChannel({ channelCode: d.channelCode, channelName: d.channelName })
    } else {
      await updateChannel(d.channelId, { channelName: d.channelName, status: d.status })
      if (selectedChannel.value?.channelId === d.channelId) {
        selectedChannel.value = { ...selectedChannel.value, channelName: d.channelName, status: d.status }
      }
    }
    d.visible = false
    await loadChannels()
  } catch (e: unknown) { alert((e as Error).message) }
  finally { d.loading = false }
}

async function handleDeleteChannel(ch: ChannelDef) {
  if (!confirm(`确定删除渠道「${ch.channelName}」？`)) return
  try {
    await deleteChannel(ch.channelId)
    if (selectedChannel.value?.channelId === ch.channelId) {
      selectedChannel.value = null
      mappings.value = []
    }
    await loadChannels()
  } catch (e: unknown) { alert((e as Error).message) }
}

// ── Category Mappings ─────────────────────────────────────────────────────────
const mappings = ref<ChannelCategoryMappingItem[]>([])
const selectedMapping = ref<ChannelCategoryMappingItem | null>(null)
const mappingsLoading = ref(false)

async function loadMappings() {
  if (!selectedChannel.value) { mappings.value = []; return }
  mappingsLoading.value = true
  try { mappings.value = await getChannelMappings(selectedChannel.value.channelId) }
  catch (e: unknown) { console.error((e as Error).message) }
  finally { mappingsLoading.value = false }
}

watch(() => selectedChannel.value?.channelId, () => { loadMappings() })

function selectMapping(m: ChannelCategoryMappingItem) {
  selectedMapping.value = selectedMapping.value?.mappingId === m.mappingId ? null : m
}

// ── Category mapping dialog ───────────────────────────────────────────────────
const categoryTree = ref<CategoryTreeNode[]>([])
const flatCategories = ref<{ id: number; label: string }[]>([])

function flattenTree(nodes: CategoryTreeNode[], depth = 0): { id: number; label: string }[] {
  return nodes.flatMap(n => [
    { id: n.categoryId, label: '  '.repeat(depth) + n.name },
    ...flattenTree(n.children, depth + 1),
  ])
}

async function loadCategoryTree() {
  try {
    categoryTree.value = await getCategoryTree()
    flatCategories.value = flattenTree(categoryTree.value)
  } catch { /* ignore */ }
}

const mappingDialog = ref<{
  visible: boolean
  mode: 'create' | 'edit'
  mappingId: number
  categoryId: number
  externalCategoryId: string
  externalCategoryName: string
  mappingVersion: string
  status: number
  loading: boolean
}>({ visible: false, mode: 'create', mappingId: 0, categoryId: 0, externalCategoryId: '', externalCategoryName: '', mappingVersion: 'v1', status: 1, loading: false })

function openCreateMapping() {
  if (!flatCategories.value.length) loadCategoryTree()
  mappingDialog.value = { visible: true, mode: 'create', mappingId: 0, categoryId: 0, externalCategoryId: '', externalCategoryName: '', mappingVersion: 'v1', status: 1, loading: false }
}

function openEditMapping(m: ChannelCategoryMappingItem) {
  mappingDialog.value = { visible: true, mode: 'edit', mappingId: m.mappingId, categoryId: m.categoryId, externalCategoryId: m.externalCategoryId, externalCategoryName: m.externalCategoryName, mappingVersion: m.mappingVersion, status: m.status, loading: false }
}

async function saveMapping() {
  const d = mappingDialog.value
  if (!selectedChannel.value) return
  if (d.mode === 'create' && !d.categoryId) { alert('请选择内部类目'); return }
  if (!d.externalCategoryId.trim() || !d.externalCategoryName.trim()) { alert('请填写外部类目 ID 和名称'); return }
  d.loading = true
  try {
    if (d.mode === 'create') {
      await createChannelMapping(selectedChannel.value.channelId, {
        categoryId: d.categoryId,
        externalCategoryId: d.externalCategoryId,
        externalCategoryName: d.externalCategoryName,
        mappingVersion: d.mappingVersion || 'v1',
      })
    } else {
      await updateChannelMapping(selectedChannel.value.channelId, d.mappingId, {
        externalCategoryId: d.externalCategoryId,
        externalCategoryName: d.externalCategoryName,
        status: d.status,
      })
    }
    d.visible = false
    await loadMappings()
    if (selectedMapping.value) {
      selectedMapping.value = mappings.value.find(m => m.mappingId === selectedMapping.value!.mappingId) ?? null
    }
  } catch (e: unknown) { alert((e as Error).message) }
  finally { d.loading = false }
}

async function handleDeleteMapping(m: ChannelCategoryMappingItem) {
  if (!confirm(`确定删除类目「${m.categoryName}」的映射？`)) return
  try {
    await deleteChannelMapping(selectedChannel.value!.channelId, m.mappingId)
    if (selectedMapping.value?.mappingId === m.mappingId) selectedMapping.value = null
    await loadMappings()
  } catch (e: unknown) { alert((e as Error).message) }
}

// ── Attribute Mappings ────────────────────────────────────────────────────────
const attributes = ref<PlmAttribute[]>([])

async function loadAttributes() {
  try { attributes.value = await getAttributeList() }
  catch { /* ignore */ }
}

const attrMappingDialog = ref<{
  visible: boolean
  mode: 'create' | 'edit'
  attrMappingId: number
  attributeId: number
  externalAttributeId: string
  externalAttributeName: string
  isRequiredOutbound: boolean
  loading: boolean
}>({ visible: false, mode: 'create', attrMappingId: 0, attributeId: 0, externalAttributeId: '', externalAttributeName: '', isRequiredOutbound: false, loading: false })

function openCreateAttrMapping() {
  if (!attributes.value.length) loadAttributes()
  attrMappingDialog.value = { visible: true, mode: 'create', attrMappingId: 0, attributeId: 0, externalAttributeId: '', externalAttributeName: '', isRequiredOutbound: false, loading: false }
}

function openEditAttrMapping(am: ChannelAttrMappingItem) {
  attrMappingDialog.value = { visible: true, mode: 'edit', attrMappingId: am.mappingId, attributeId: am.attributeId, externalAttributeId: am.externalAttributeId, externalAttributeName: am.externalAttributeName, isRequiredOutbound: am.isRequiredOutbound, loading: false }
}

async function saveAttrMapping() {
  const d = attrMappingDialog.value
  if (!selectedChannel.value || !selectedMapping.value) return
  if (d.mode === 'create' && !d.attributeId) { alert('请选择内部属性'); return }
  if (!d.externalAttributeId.trim() || !d.externalAttributeName.trim()) { alert('请填写外部属性 ID 和名称'); return }
  d.loading = true
  try {
    if (d.mode === 'create') {
      await createAttrMapping(selectedChannel.value.channelId, selectedMapping.value.mappingId, {
        attributeId: d.attributeId,
        externalAttributeId: d.externalAttributeId,
        externalAttributeName: d.externalAttributeName,
        isRequiredOutbound: d.isRequiredOutbound,
      })
    } else {
      await updateAttrMapping(selectedChannel.value.channelId, selectedMapping.value.mappingId, d.attrMappingId, {
        externalAttributeId: d.externalAttributeId,
        externalAttributeName: d.externalAttributeName,
        isRequiredOutbound: d.isRequiredOutbound,
      })
    }
    d.visible = false
    await loadMappings()
    selectedMapping.value = mappings.value.find(m => m.mappingId === selectedMapping.value!.mappingId) ?? null
  } catch (e: unknown) { alert((e as Error).message) }
  finally { d.loading = false }
}

async function handleDeleteAttrMapping(am: ChannelAttrMappingItem) {
  if (!confirm(`确定删除属性「${am.attributeName}」的映射？`)) return
  try {
    await deleteAttrMapping(selectedChannel.value!.channelId, selectedMapping.value!.mappingId, am.mappingId)
    await loadMappings()
    selectedMapping.value = mappings.value.find(m => m.mappingId === selectedMapping.value!.mappingId) ?? null
  } catch (e: unknown) { alert((e as Error).message) }
}

// ── Mapped attribute IDs for current selected mapping ─────────────────────────
function mappedAttrIds(): Set<number> {
  return new Set(selectedMapping.value?.attributeMappings.map(a => a.attributeId) ?? [])
}

// ── Filtered attributes not yet mapped ───────────────────────────────────────
function unmappedAttributes(): PlmAttribute[] {
  const ids = mappedAttrIds()
  return attributes.value.filter(a => !ids.has(a.attributeId))
}

onMounted(() => { loadChannels(); loadCategoryTree(); loadAttributes() })
</script>

<template>
  <div class="flex gap-3 h-[calc(100vh-120px)]">
    <!-- Left: Channel list -->
    <div class="w-[220px] flex-shrink-0 flex flex-col border border-border rounded-[var(--radius-lg)] bg-card overflow-hidden">
      <div class="px-3 py-2.5 border-b border-border flex items-center justify-between">
        <span class="text-[13px] font-semibold flex items-center gap-1.5">
          <Globe :size="14" class="text-muted-foreground" />
          渠道列表
        </span>
        <ButtonVue variant="ghost" size="sm" title="新建渠道" @click="openCreateChannel">
          <Plus :size="13" />
        </ButtonVue>
      </div>
      <div class="flex-1 overflow-y-auto py-1.5 px-1.5">
        <div v-if="channelLoading" class="text-center py-6 text-[12px] text-muted-foreground">加载中...</div>
        <template v-else>
          <div
            v-for="ch in channels"
            :key="ch.channelId"
            :class="[
              'flex items-center gap-2 px-2 py-2 rounded-[var(--radius)] cursor-pointer transition-colors group',
              selectedChannel?.channelId === ch.channelId
                ? 'bg-primary-soft text-primary-soft-fg'
                : 'hover:bg-muted text-foreground',
            ]"
            @click="selectChannel(ch)"
          >
            <div class="flex-1 min-w-0">
              <div class="text-[12px] font-medium truncate">{{ ch.channelName }}</div>
              <div class="text-[10.5px] font-mono-tnum opacity-70 truncate">{{ ch.channelCode }}</div>
            </div>
            <BadgeVue
              v-if="ch.status !== 1"
              variant="warning"
              class="text-[9px] px-1 py-0 shrink-0"
            >停用</BadgeVue>
            <div
              class="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"
              @click.stop
            >
              <button
                class="p-0.5 rounded hover:bg-primary/10 text-muted-foreground hover:text-primary cursor-pointer border-0 bg-transparent"
                title="编辑"
                @click="openEditChannel(ch)"
              >
                <Pencil :size="11" />
              </button>
              <button
                class="p-0.5 rounded hover:bg-danger/10 text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent"
                title="删除"
                @click="handleDeleteChannel(ch)"
              >
                <Trash2 :size="11" />
              </button>
            </div>
          </div>
          <div v-if="channels.length === 0" class="text-center py-8 text-[12px] text-muted-foreground">
            暂无渠道，点击 + 新建
          </div>
        </template>
      </div>
    </div>

    <!-- Middle: Category mappings -->
    <div class="w-[300px] flex-shrink-0 flex flex-col border border-border rounded-[var(--radius-lg)] bg-card overflow-hidden">
      <div class="px-3 py-2.5 border-b border-border flex items-center gap-2">
        <span class="text-[13px] font-semibold truncate flex items-center gap-1.5">
          <ArrowRight :size="14" class="text-muted-foreground shrink-0" />
          {{ selectedChannel ? selectedChannel.channelName + ' — 类目映射' : '类目映射' }}
        </span>
        <div class="flex-1" />
        <ButtonVue
          v-if="selectedChannel"
          variant="ghost"
          size="sm"
          title="新建类目映射"
          @click="openCreateMapping"
        >
          <Plus :size="13" />
        </ButtonVue>
      </div>
      <div class="flex-1 overflow-y-auto">
        <div v-if="!selectedChannel" class="flex flex-col items-center justify-center h-full text-[12px] text-muted-foreground gap-1.5 py-10">
          <Globe :size="28" class="opacity-25" />
          <span>请先选择左侧渠道</span>
        </div>
        <div v-else-if="mappingsLoading" class="text-center py-8 text-[12px] text-muted-foreground">加载中...</div>
        <template v-else>
          <div v-if="mappings.length === 0" class="flex flex-col items-center justify-center py-10 text-[12px] text-muted-foreground">
            暂无类目映射，点击右上角 + 新建
          </div>
          <div
            v-for="m in mappings"
            :key="m.mappingId"
            :class="[
              'border-b border-border last:border-0 px-3 py-2.5 cursor-pointer transition-colors group',
              selectedMapping?.mappingId === m.mappingId
                ? 'bg-primary-soft'
                : 'hover:bg-subtle',
            ]"
            @click="selectMapping(m)"
          >
            <div class="flex items-start gap-2">
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-1.5">
                  <span class="text-[12px] font-medium truncate">{{ m.categoryName }}</span>
                  <span class="text-[10px] text-muted-foreground font-mono-tnum shrink-0">{{ m.mappingVersion }}</span>
                </div>
                <div class="text-[11px] text-muted-foreground mt-0.5 truncate">
                  → {{ m.externalCategoryName }}
                  <span class="font-mono-tnum text-[10px]">（{{ m.externalCategoryId }}）</span>
                </div>
                <div class="text-[10.5px] text-muted-foreground mt-0.5">
                  {{ m.attributeMappings.length }} 个属性映射
                </div>
              </div>
              <div
                class="flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity shrink-0"
                @click.stop
              >
                <button
                  class="p-0.5 rounded hover:bg-primary/10 text-muted-foreground hover:text-primary cursor-pointer border-0 bg-transparent"
                  @click="openEditMapping(m)"
                ><Pencil :size="12" /></button>
                <button
                  class="p-0.5 rounded hover:bg-danger/10 text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent"
                  @click="handleDeleteMapping(m)"
                ><Trash2 :size="12" /></button>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>

    <!-- Right: Attribute mappings for selected category mapping -->
    <div class="flex-1 flex flex-col border border-border rounded-[var(--radius-lg)] bg-card overflow-hidden">
      <div class="px-4 py-2.5 border-b border-border flex items-center gap-2">
        <span class="text-[13px] font-semibold flex items-center gap-1.5">
          <Tag :size="14" class="text-muted-foreground" />
          {{ selectedMapping ? selectedMapping.categoryName + ' — 属性映射' : '属性映射' }}
        </span>
        <div class="flex-1" />
        <ButtonVue
          v-if="selectedMapping"
          size="sm"
          @click="openCreateAttrMapping"
        >
          <Plus :size="13" />
          添加属性映射
        </ButtonVue>
      </div>
      <div class="flex-1 overflow-y-auto">
        <div v-if="!selectedMapping" class="flex flex-col items-center justify-center h-full text-[12px] text-muted-foreground gap-1.5 py-10">
          <Tag :size="28" class="opacity-25" />
          <span>请先选择中间栏的类目映射</span>
          <span class="text-[11px]">查看和管理属性映射</span>
        </div>
        <template v-else>
          <div v-if="selectedMapping.attributeMappings.length === 0" class="flex flex-col items-center justify-center py-10 text-[12px] text-muted-foreground">
            暂无属性映射，点击右上角添加
          </div>
          <table v-else class="w-full text-[12px]">
            <thead>
              <tr>
                <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-4 py-2 font-medium border-b border-border">内部属性</th>
                <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-4 py-2 font-medium border-b border-border">外部属性 ID</th>
                <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-4 py-2 font-medium border-b border-border">外部属性名</th>
                <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-4 py-2 font-medium border-b border-border">必填</th>
                <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-4 py-2 font-medium border-b border-border">操作</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="am in selectedMapping.attributeMappings"
                :key="am.mappingId"
                class="border-b border-border last:border-0 hover:bg-subtle transition-colors"
              >
                <td class="px-4 py-2 font-medium">
                  {{ am.attributeName }}
                  <span v-if="am.attributeCode" class="ml-1 text-[10.5px] font-mono-tnum text-muted-foreground">{{ am.attributeCode }}</span>
                </td>
                <td class="px-4 py-2 font-mono-tnum text-muted-foreground">{{ am.externalAttributeId }}</td>
                <td class="px-4 py-2">{{ am.externalAttributeName }}</td>
                <td class="px-4 py-2">
                  <BadgeVue v-if="am.isRequiredOutbound" variant="danger" class="text-[9px] px-1.5">必填</BadgeVue>
                  <span v-else class="text-muted-foreground text-[11px]">-</span>
                </td>
                <td class="px-4 py-2">
                  <div class="flex items-center gap-2">
                    <button
                      class="text-primary text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                      @click="openEditAttrMapping(am)"
                    ><Pencil :size="12" class="inline mr-0.5" />编辑</button>
                    <button
                      class="text-danger text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                      @click="handleDeleteAttrMapping(am)"
                    ><Trash2 :size="12" class="inline mr-0.5" />删除</button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </template>
      </div>
    </div>
  </div>

  <!-- Channel dialog -->
  <Teleport to="body">
    <div v-if="channelDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="channelDialog.visible = false">
      <div class="absolute inset-0 bg-black/50" />
      <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[380px] p-6 space-y-4">
        <h3 class="text-[14px] font-semibold">{{ channelDialog.mode === 'create' ? '新建渠道' : '编辑渠道' }}</h3>
        <div class="space-y-3">
          <div v-if="channelDialog.mode === 'create'" class="space-y-1">
            <label class="text-[12px] text-muted-foreground">渠道编码 <span class="text-danger">*</span></label>
            <InputVue v-model="channelDialog.channelCode" placeholder="如：1688、taobao、douyin" />
          </div>
          <div v-else class="space-y-1">
            <label class="text-[12px] text-muted-foreground">渠道编码</label>
            <div class="text-[12px] font-mono-tnum text-muted-foreground px-3 py-1.5 bg-muted rounded-[var(--radius)]">{{ channelDialog.channelCode }}</div>
          </div>
          <div class="space-y-1">
            <label class="text-[12px] text-muted-foreground">渠道名称 <span class="text-danger">*</span></label>
            <InputVue v-model="channelDialog.channelName" placeholder="如：1688批发平台" />
          </div>
          <div v-if="channelDialog.mode === 'edit'" class="space-y-1">
            <label class="text-[12px] text-muted-foreground">状态</label>
            <select
              v-model.number="channelDialog.status"
              class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
            >
              <option :value="1">启用</option>
              <option :value="0">停用</option>
            </select>
          </div>
        </div>
        <div class="flex justify-end gap-2">
          <ButtonVue variant="outline" size="sm" @click="channelDialog.visible = false">取消</ButtonVue>
          <ButtonVue size="sm" :disabled="channelDialog.loading" @click="saveChannel">
            {{ channelDialog.loading ? '保存中...' : '保存' }}
          </ButtonVue>
        </div>
      </div>
    </div>
  </Teleport>

  <!-- Category mapping dialog -->
  <Teleport to="body">
    <div v-if="mappingDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="mappingDialog.visible = false">
      <div class="absolute inset-0 bg-black/50" />
      <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[440px] p-6 space-y-4">
        <h3 class="text-[14px] font-semibold">{{ mappingDialog.mode === 'create' ? '新建类目映射' : '编辑类目映射' }}</h3>
        <div class="space-y-3">
          <div v-if="mappingDialog.mode === 'create'" class="space-y-1">
            <label class="text-[12px] text-muted-foreground">内部类目 <span class="text-danger">*</span></label>
            <select
              v-model.number="mappingDialog.categoryId"
              class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
            >
              <option :value="0" disabled>请选择类目</option>
              <option v-for="c in flatCategories" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">外部类目 ID <span class="text-danger">*</span></label>
              <InputVue v-model="mappingDialog.externalCategoryId" placeholder="渠道侧类目 ID" />
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">外部类目名称 <span class="text-danger">*</span></label>
              <InputVue v-model="mappingDialog.externalCategoryName" placeholder="渠道侧类目名称" />
            </div>
          </div>
          <div class="space-y-1">
            <label class="text-[12px] text-muted-foreground">映射版本</label>
            <InputVue v-model="mappingDialog.mappingVersion" placeholder="v1" />
          </div>
          <div v-if="mappingDialog.mode === 'edit'" class="space-y-1">
            <label class="text-[12px] text-muted-foreground">状态</label>
            <select
              v-model.number="mappingDialog.status"
              class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
            >
              <option :value="1">启用</option>
              <option :value="0">停用</option>
            </select>
          </div>
        </div>
        <div class="flex justify-end gap-2">
          <ButtonVue variant="outline" size="sm" @click="mappingDialog.visible = false">取消</ButtonVue>
          <ButtonVue size="sm" :disabled="mappingDialog.loading" @click="saveMapping">
            {{ mappingDialog.loading ? '保存中...' : '保存' }}
          </ButtonVue>
        </div>
      </div>
    </div>
  </Teleport>

  <!-- Attribute mapping dialog -->
  <Teleport to="body">
    <div v-if="attrMappingDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="attrMappingDialog.visible = false">
      <div class="absolute inset-0 bg-black/50" />
      <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[440px] p-6 space-y-4">
        <h3 class="text-[14px] font-semibold">{{ attrMappingDialog.mode === 'create' ? '添加属性映射' : '编辑属性映射' }}</h3>
        <div class="space-y-3">
          <div v-if="attrMappingDialog.mode === 'create'" class="space-y-1">
            <label class="text-[12px] text-muted-foreground">内部属性 <span class="text-danger">*</span></label>
            <select
              v-model.number="attrMappingDialog.attributeId"
              class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
            >
              <option :value="0" disabled>请选择属性</option>
              <option
                v-for="a in unmappedAttributes()"
                :key="a.attributeId"
                :value="a.attributeId"
              >{{ a.name }}{{ a.code ? ` (${a.code})` : '' }}</option>
            </select>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">外部属性 ID <span class="text-danger">*</span></label>
              <InputVue v-model="attrMappingDialog.externalAttributeId" placeholder="渠道侧属性 ID" />
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">外部属性名称 <span class="text-danger">*</span></label>
              <InputVue v-model="attrMappingDialog.externalAttributeName" placeholder="渠道侧属性名称" />
            </div>
          </div>
          <label class="flex items-center gap-2 cursor-pointer text-[12px]">
            <input v-model="attrMappingDialog.isRequiredOutbound" type="checkbox" class="cursor-pointer" />
            出站必填（渠道要求此属性必须有值）
          </label>
        </div>
        <div class="flex justify-end gap-2">
          <ButtonVue variant="outline" size="sm" @click="attrMappingDialog.visible = false">取消</ButtonVue>
          <ButtonVue size="sm" :disabled="attrMappingDialog.loading" @click="saveAttrMapping">
            {{ attrMappingDialog.loading ? '保存中...' : '保存' }}
          </ButtonVue>
        </div>
      </div>
    </div>
  </Teleport>
</template>
