<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ChevronRight, ChevronDown, Plus, Send, Trash2, Link2, Unlink } from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import InputVue from '@/components/ui/input.vue'
import BadgeVue from '@/components/ui/badge.vue'
import {
  getCategoryModel, getCategoryTree, getCategoryModelVersions,
  getVersionAttributes, createModelVersion, publishModelVersion, deleteModelVersion,
  bindAttributeToVersion, unbindAttributeFromVersion,
  getAttributeList,
} from '@/api/category'
import type {
  CategoryModelDetailResponse, CategoryTreeNode, CategoryModelVersionSummary,
  CategoryAttributeGrouped, CategoryAttributeResponse, PlmAttribute,
} from '@/api/category/types'

// ── Tree ──────────────────────────────────────────────────────────────────────
const loadingTree = ref(false)
const categoryTree = ref<CategoryTreeNode[]>([])
const selectedCategoryId = ref<number | null>(null)
const expandedIds = ref<Set<number>>(new Set())

function toggleExpand(id: number) {
  if (expandedIds.value.has(id)) expandedIds.value.delete(id)
  else expandedIds.value.add(id)
}

function collectAllIds(nodes: CategoryTreeNode[]): number[] {
  return nodes.flatMap(n => [n.categoryId, ...collectAllIds(n.children ?? [])])
}

function findFirstLeaf(nodes: CategoryTreeNode[]): CategoryTreeNode | null {
  for (const node of nodes) {
    if (!node.children?.length) return node
    const found = findFirstLeaf(node.children)
    if (found) return found
  }
  return null
}

// ── Model / versions ──────────────────────────────────────────────────────────
const loadingModel = ref(false)
const model = ref<CategoryModelDetailResponse | null>(null)
const versions = ref<CategoryModelVersionSummary[]>([])
const selectedVersionId = ref<number | null>(null)
const versionAttrs = ref<CategoryAttributeGrouped[]>([])
const loadingAttrs = ref(false)

const selectedVersion = computed(() =>
  versions.value.find(v => v.modelVersionId === selectedVersionId.value) ?? null
)

const isDraft = computed(() => selectedVersion.value?.status === 'draft')

const versionBoundIds = computed(() =>
  new Set(versionAttrs.value.flatMap(g => g.attributes.map(a => a.attributeId)))
)

async function loadModel(categoryId: number) {
  selectedCategoryId.value = categoryId
  loadingModel.value = true
  try {
    model.value = await getCategoryModel(categoryId)
    versions.value = await getCategoryModelVersions(categoryId)
    // Select the active version by default
    const active = versions.value.find(v => v.status === 'active')
    if (active) {
      selectedVersionId.value = active.modelVersionId
      versionAttrs.value = model.value?.attributeGroups ?? []
    } else if (versions.value.length > 0) {
      await selectVersion(versions.value[0].modelVersionId)
    } else {
      selectedVersionId.value = null
      versionAttrs.value = []
    }
  } catch {
    model.value = null
    versions.value = []
  } finally {
    loadingModel.value = false
  }
}

async function selectVersion(versionId: number) {
  selectedVersionId.value = versionId
  const ver = versions.value.find(v => v.modelVersionId === versionId)
  if (!ver || selectedCategoryId.value == null) return

  // If active version, use model.attributeGroups (already loaded)
  if (ver.status === 'active' && model.value) {
    versionAttrs.value = model.value.attributeGroups
    return
  }
  loadingAttrs.value = true
  try {
    versionAttrs.value = await getVersionAttributes(selectedCategoryId.value, versionId)
  } catch {
    versionAttrs.value = []
  } finally {
    loadingAttrs.value = false
  }
}

async function loadTree() {
  loadingTree.value = true
  try {
    categoryTree.value = await getCategoryTree()
    expandedIds.value = new Set(collectAllIds(categoryTree.value))
    const leaf = findFirstLeaf(categoryTree.value)
    if (leaf) await loadModel(leaf.categoryId)
  } catch {
    // ignore
  } finally {
    loadingTree.value = false
  }
}

// ── Version create dialog ─────────────────────────────────────────────────────
const createVersionDialog = ref({ visible: false, changeSummary: '', loading: false })

function openCreateVersion() {
  createVersionDialog.value = { visible: true, changeSummary: '', loading: false }
}

async function saveCreateVersion() {
  if (selectedCategoryId.value == null) return
  createVersionDialog.value.loading = true
  try {
    await createModelVersion(selectedCategoryId.value, {
      changeSummary: createVersionDialog.value.changeSummary || undefined,
    })
    createVersionDialog.value.visible = false
    await loadModel(selectedCategoryId.value)
    // Auto-select the new draft
    const draft = versions.value.find(v => v.status === 'draft')
    if (draft) await selectVersion(draft.modelVersionId)
  } catch (e: unknown) {
    alert((e as Error).message)
  } finally {
    createVersionDialog.value.loading = false
  }
}

async function handlePublish(versionId: number) {
  if (!confirm('确定发布该草稿版本？发布后当前生效版本将被归档。')) return
  try {
    await publishModelVersion(selectedCategoryId.value!, versionId)
    await loadModel(selectedCategoryId.value!)
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

async function handleDeleteVersion(versionId: number) {
  if (!confirm('确定删除该草稿版本？')) return
  try {
    await deleteModelVersion(selectedCategoryId.value!, versionId)
    await loadModel(selectedCategoryId.value!)
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

// ── Version attribute binding ─────────────────────────────────────────────────
const allAttributes = ref<PlmAttribute[]>([])
const bindAttrDialog = ref<{
  visible: boolean
  attr: PlmAttribute | null
  isRequired: boolean
  isSaleAxis: boolean
  groupType: string
  orderNum: number
  loading: boolean
}>({ visible: false, attr: null, isRequired: false, isSaleAxis: false, groupType: 'basic', orderNum: 0, loading: false })

async function openBindAttrDialog(attr: PlmAttribute) {
  bindAttrDialog.value = {
    visible: true,
    attr,
    isRequired: false,
    isSaleAxis: attr.attributeType === 'enum',
    groupType: 'basic',
    orderNum: 0,
    loading: false,
  }
}

async function saveBindAttr() {
  const d = bindAttrDialog.value
  if (!d.attr || selectedCategoryId.value == null || selectedVersionId.value == null) return
  d.loading = true
  try {
    await bindAttributeToVersion(selectedCategoryId.value, selectedVersionId.value, {
      attributeId: d.attr.attributeId,
      isRequired: d.isRequired,
      isSaleAxis: d.isSaleAxis,
      groupType: d.groupType,
      orderNum: d.orderNum,
    })
    d.visible = false
    await selectVersion(selectedVersionId.value)
  } catch (e: unknown) {
    alert((e as Error).message)
  } finally {
    d.loading = false
  }
}

async function handleUnbindFromVersion(attr: CategoryAttributeResponse) {
  if (!confirm(`确定从此版本解绑「${attr.name}」？`)) return
  try {
    await unbindAttributeFromVersion(selectedCategoryId.value!, selectedVersionId.value!, attr.id)
    await selectVersion(selectedVersionId.value!)
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

const addAttrDialog = ref({ visible: false, keyword: '' })
const filteredUnboundAttrs = computed(() => {
  const kw = addAttrDialog.value.keyword.toLowerCase()
  return allAttributes.value.filter(a =>
    !versionBoundIds.value.has(a.attributeId) &&
    (kw === '' || a.name.toLowerCase().includes(kw) || (a.code ?? '').toLowerCase().includes(kw))
  )
})

async function openAddAttrDialog() {
  addAttrDialog.value = { visible: true, keyword: '' }
  if (allAttributes.value.length === 0) {
    try { allAttributes.value = await getAttributeList() } catch { /* ignore */ }
  }
}

// ── Formatters ────────────────────────────────────────────────────────────────
function formatDate(value?: string | null) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-CN')
}

function statusLabel(status?: string | null) {
  const map: Record<string, string> = { draft: '草稿', active: '生效中', archived: '已归档' }
  return status ? (map[status] ?? status) : '-'
}

function statusClass(status?: string | null) {
  if (status === 'active') return 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
  if (status === 'draft') return 'bg-[color-mix(in_oklch,var(--warning)_18%,transparent)] text-[var(--warning)]'
  return 'bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]'
}

const groupTypeLabel: Record<string, string> = {
  basic: '基础属性', sale: '销售属性', logistics: '物流属性',
  compliance: '合规属性', detail: '详情属性',
}
const attrTypeLabel: Record<string, string> = { text: '文本', number: '数值', enum: '枚举', boolean: '布尔', date: '日期' }

const hasDraft = computed(() => versions.value.some(v => v.status === 'draft'))

onMounted(loadTree)
</script>

<template>
  <div class="flex gap-4 min-h-[calc(100vh-160px)]">
    <!-- Left tree panel -->
    <aside class="w-[220px] shrink-0 bg-card border border-border rounded-[var(--radius)] flex flex-col overflow-hidden">
      <div class="px-3.5 py-2.5 border-b border-border">
        <span class="text-[13px] font-semibold text-foreground">类目树</span>
      </div>
      <div class="flex-1 overflow-y-auto py-1 px-1">
        <div v-if="loadingTree" class="p-3 space-y-2">
          <div v-for="i in 6" :key="i" class="h-[28px] bg-muted rounded animate-pulse" />
        </div>
        <div v-else-if="!categoryTree.length" class="p-4 text-center text-[12px] text-muted-foreground">
          暂无类目数据
        </div>
        <template v-else>
          <template v-for="node in categoryTree" :key="node.categoryId">
            <div>
              <button
                :class="[
                  'w-full flex items-center gap-1.5 px-2 h-[28px] rounded-[var(--radius)] text-[12px] transition-colors cursor-pointer border-0 text-left',
                  selectedCategoryId === node.categoryId
                    ? 'bg-primary-soft text-primary-soft-fg font-medium'
                    : 'text-foreground hover:bg-muted',
                ]"
                @click="loadModel(node.categoryId)"
              >
                <button
                  v-if="node.children?.length"
                  class="shrink-0 border-0 bg-transparent p-0 cursor-pointer text-muted-foreground"
                  @click.stop="toggleExpand(node.categoryId)"
                >
                  <ChevronDown v-if="expandedIds.has(node.categoryId)" :size="12" />
                  <ChevronRight v-else :size="12" />
                </button>
                <span v-else class="w-3 shrink-0" />
                <span class="flex-1 truncate">{{ node.name }}</span>
              </button>
              <div v-if="node.children?.length && expandedIds.has(node.categoryId)" class="pl-4">
                <button
                  v-for="child in node.children"
                  :key="child.categoryId"
                  :class="[
                    'w-full flex items-center gap-1.5 px-2 h-[28px] rounded-[var(--radius)] text-[12px] transition-colors cursor-pointer border-0 text-left',
                    selectedCategoryId === child.categoryId
                      ? 'bg-primary-soft text-primary-soft-fg font-medium'
                      : 'text-foreground hover:bg-muted',
                  ]"
                  @click="loadModel(child.categoryId)"
                >
                  <button
                    v-if="child.children?.length"
                    class="shrink-0 border-0 bg-transparent p-0 cursor-pointer text-muted-foreground"
                    @click.stop="toggleExpand(child.categoryId)"
                  >
                    <ChevronDown v-if="expandedIds.has(child.categoryId)" :size="12" />
                    <ChevronRight v-else :size="12" />
                  </button>
                  <span v-else class="w-3 shrink-0" />
                  <span class="flex-1 truncate">{{ child.name }}</span>
                </button>
                <template v-for="child in node.children" :key="'gc-' + child.categoryId">
                  <div v-if="child.children?.length && expandedIds.has(child.categoryId)" class="pl-4">
                    <button
                      v-for="gc in child.children"
                      :key="gc.categoryId"
                      :class="[
                        'w-full flex items-center gap-1.5 px-2 h-[28px] rounded-[var(--radius)] text-[12px] transition-colors cursor-pointer border-0 text-left',
                        selectedCategoryId === gc.categoryId
                          ? 'bg-primary-soft text-primary-soft-fg font-medium'
                          : 'text-foreground hover:bg-muted',
                      ]"
                      @click="loadModel(gc.categoryId)"
                    >
                      <span class="w-3 shrink-0" />
                      <span class="flex-1 truncate">{{ gc.name }}</span>
                    </button>
                  </div>
                </template>
              </div>
            </div>
          </template>
        </template>
      </div>
    </aside>

    <!-- Right content panel -->
    <div class="flex-1 min-w-0 space-y-4">
      <!-- Loading -->
      <div v-if="loadingModel" class="space-y-4">
        <div v-for="i in 3" :key="i" class="bg-card border border-border rounded-[var(--radius)] p-5">
          <div class="h-4 bg-muted rounded w-32 mb-3 animate-pulse" />
          <div class="space-y-2">
            <div v-for="j in 2" :key="j" class="h-3 bg-muted rounded animate-pulse" />
          </div>
        </div>
      </div>

      <div v-else-if="!model" class="bg-card border border-border rounded-[var(--radius)] p-10 text-center text-[13px] text-muted-foreground">
        请在左侧选择一个类目查看模型
      </div>

      <template v-else>
        <!-- Hero card -->
        <div class="bg-card border border-border rounded-[var(--radius)] p-5">
          <div class="flex items-start justify-between gap-4">
            <div>
              <h2 class="text-[18px] font-semibold text-foreground mb-1">{{ model.categoryName }}</h2>
              <p v-if="model.activeVersion" class="text-[12px] text-muted-foreground">
                当前生效版本：<span class="font-mono-tnum">{{ model.activeVersion.versionNo }}</span>
                <span :class="['inline-flex items-center px-1.5 py-0.5 rounded text-[11px] font-medium ml-1.5', statusClass(model.activeVersion.status)]">
                  {{ statusLabel(model.activeVersion.status) }}
                </span>
              </p>
              <p v-else class="text-[12px] text-muted-foreground">当前类目还没有生效模型版本</p>
            </div>
            <ButtonVue
              v-if="!hasDraft"
              size="sm"
              @click="openCreateVersion"
            >
              <Plus :size="13" />
              新建版本
            </ButtonVue>
            <BadgeVue v-else variant="warning" class="shrink-0">有草稿未发布</BadgeVue>
          </div>
        </div>

        <!-- Version list with selection -->
        <div class="bg-card border border-border rounded-[var(--radius)] p-5">
          <div class="text-[13px] font-semibold text-foreground mb-3">版本列表</div>
          <div class="overflow-x-auto">
            <table class="w-full text-[12px]">
              <thead>
                <tr class="border-b border-border">
                  <th class="text-left py-2 pr-4 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[110px]">版本号</th>
                  <th class="text-left py-2 pr-4 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[90px]">状态</th>
                  <th class="text-left py-2 pr-4 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">生效时间</th>
                  <th class="text-left py-2 pr-4 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">发布时间</th>
                  <th class="text-left py-2 pr-4 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">变更说明</th>
                  <th class="text-left py-2 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[120px]">操作</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="v in versions"
                  :key="v.modelVersionId"
                  :class="[
                    'border-b border-border transition-colors cursor-pointer',
                    selectedVersionId === v.modelVersionId ? 'bg-primary-soft' : 'hover:bg-subtle',
                  ]"
                  @click="selectVersion(v.modelVersionId)"
                >
                  <td class="py-2 pr-4 font-mono-tnum font-medium">{{ v.versionNo }}</td>
                  <td class="py-2 pr-4">
                    <span :class="['inline-flex items-center px-1.5 py-0.5 rounded text-[11px] font-medium', statusClass(v.status)]">
                      {{ statusLabel(v.status) }}
                    </span>
                  </td>
                  <td class="py-2 pr-4 font-mono-tnum text-muted-foreground">{{ formatDate(v.effectiveFrom) }}</td>
                  <td class="py-2 pr-4 font-mono-tnum text-muted-foreground">{{ formatDate(v.publishedAt) }}</td>
                  <td class="py-2 pr-4 text-muted-foreground truncate max-w-[180px]">{{ v.changeSummary || '-' }}</td>
                  <td class="py-2" @click.stop>
                    <div v-if="v.status === 'draft'" class="flex items-center gap-2">
                      <button
                        class="flex items-center gap-1 text-success text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                        @click="handlePublish(v.modelVersionId)"
                      >
                        <Send :size="11" />发布
                      </button>
                      <button
                        class="flex items-center gap-1 text-danger text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                        @click="handleDeleteVersion(v.modelVersionId)"
                      >
                        <Trash2 :size="11" />删除
                      </button>
                    </div>
                  </td>
                </tr>
                <tr v-if="!versions.length">
                  <td colspan="6" class="py-4 text-center text-muted-foreground">暂无版本记录</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Attribute groups of selected version -->
        <div class="bg-card border border-border rounded-[var(--radius)] p-5">
          <div class="flex items-center justify-between mb-3">
            <div class="text-[13px] font-semibold text-foreground">
              属性模型
              <span v-if="selectedVersion" class="text-[11px] font-normal text-muted-foreground ml-1.5">
                {{ selectedVersion.versionNo }} · {{ statusLabel(selectedVersion.status) }}
              </span>
            </div>
            <ButtonVue v-if="isDraft" size="sm" variant="outline" @click="openAddAttrDialog">
              <Plus :size="13" />
              添加属性
            </ButtonVue>
          </div>

          <div v-if="loadingAttrs" class="py-6 text-center text-[12px] text-muted-foreground">加载中...</div>
          <div v-else-if="versionAttrs.length === 0" class="py-8 text-center text-[12px] text-muted-foreground">
            {{ isDraft ? '此版本暂无属性，点击「添加属性」从属性库选择' : '此版本没有属性定义' }}
          </div>
          <div v-else>
            <div v-for="group in versionAttrs" :key="group.groupName ?? 'default'" class="mb-5 last:mb-0">
              <div class="text-[12px] font-semibold text-foreground mb-2 pb-1 border-b border-border">{{ group.groupLabel }}</div>
              <table class="w-full text-[12px]">
                <thead>
                  <tr class="border-b border-border">
                    <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">属性名</th>
                    <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[120px]">编码</th>
                    <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[80px]">类型</th>
                    <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[60px]">必填</th>
                    <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[60px]">销售轴</th>
                    <th class="text-left py-1.5 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">可选值</th>
                    <th v-if="isDraft" class="py-1.5 w-[50px]" />
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="attr in group.attributes"
                    :key="attr.id"
                    class="border-b border-border hover:bg-subtle transition-colors group/attrrow"
                  >
                    <td class="py-1.5 pr-3 font-medium">{{ attr.name }}</td>
                    <td class="py-1.5 pr-3 font-mono-tnum text-muted-foreground">{{ attr.code || '-' }}</td>
                    <td class="py-1.5 pr-3">
                      <BadgeVue variant="info">{{ attrTypeLabel[attr.attributeType] ?? attr.attributeType }}</BadgeVue>
                    </td>
                    <td class="py-1.5 pr-3">
                      <span :class="attr.isRequired ? 'text-[var(--danger)]' : 'text-muted-foreground'">
                        {{ attr.isRequired ? '是' : '否' }}
                      </span>
                    </td>
                    <td class="py-1.5 pr-3 text-muted-foreground">{{ attr.isSaleAxis ? '是' : '-' }}</td>
                    <td class="py-1.5">
                      <div class="flex flex-wrap gap-1">
                        <span
                          v-for="val in attr.values"
                          :key="val.valueId"
                          class="inline-flex items-center px-1.5 py-0.5 rounded text-[11px] bg-muted text-muted-foreground"
                        >
                          {{ val.label }}
                        </span>
                        <span v-if="!attr.values.length" class="text-muted-foreground">-</span>
                      </div>
                    </td>
                    <td v-if="isDraft" class="py-1.5 text-right pr-1">
                      <button
                        class="opacity-0 group-hover/attrrow:opacity-100 transition-opacity p-1 rounded hover:bg-danger/10 text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent"
                        title="解绑"
                        @click="handleUnbindFromVersion(attr)"
                      >
                        <Unlink :size="12" />
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- Rules (active version only) -->
        <div v-if="model.rules.length && selectedVersion?.status === 'active'" class="bg-card border border-border rounded-[var(--radius)] p-5">
          <div class="text-[13px] font-semibold text-foreground mb-3">规则引擎</div>
          <table class="w-full text-[12px]">
            <thead>
              <tr class="border-b border-border">
                <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">规则名</th>
                <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[140px]">编码</th>
                <th class="text-left py-1.5 pr-3 text-[11px] uppercase tracking-wide text-muted-foreground font-medium w-[80px]">优先级</th>
                <th class="text-left py-1.5 text-[11px] uppercase tracking-wide text-muted-foreground font-medium">提示信息</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="rule in model.rules" :key="rule.ruleId" class="border-b border-border hover:bg-subtle">
                <td class="py-1.5 pr-3">{{ rule.ruleName }}</td>
                <td class="py-1.5 pr-3 font-mono-tnum text-muted-foreground">{{ rule.ruleCode }}</td>
                <td class="py-1.5 pr-3 font-mono-tnum text-right">{{ rule.priority }}</td>
                <td class="py-1.5 text-muted-foreground">{{ rule.message || '-' }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </template>
    </div>

    <!-- Create version dialog -->
    <Teleport to="body">
      <div v-if="createVersionDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="createVersionDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[420px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">新建版本</h3>
          <p class="text-[12px] text-muted-foreground">将在当前生效版本的属性基础上创建草稿，可编辑后发布。</p>
          <div class="space-y-1">
            <label class="text-[12px] text-muted-foreground">变更说明（可选）</label>
            <InputVue v-model="createVersionDialog.changeSummary" placeholder="描述本次变更的内容..." />
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="createVersionDialog.visible = false">取消</ButtonVue>
            <ButtonVue size="sm" :disabled="createVersionDialog.loading" @click="saveCreateVersion">
              {{ createVersionDialog.loading ? '创建中...' : '创建草稿' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Add attribute to version dialog -->
    <Teleport to="body">
      <div v-if="addAttrDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="addAttrDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[520px] p-6 space-y-4 max-h-[80vh] flex flex-col">
          <h3 class="text-[14px] font-semibold shrink-0">添加属性到草稿版本</h3>
          <InputVue v-model="addAttrDialog.keyword" placeholder="搜索属性名称或编码..." class="shrink-0" />
          <div class="flex-1 overflow-y-auto border border-border rounded-[var(--radius)]">
            <div v-if="filteredUnboundAttrs.length === 0" class="py-8 text-center text-[12px] text-muted-foreground">
              {{ addAttrDialog.keyword ? '无匹配属性' : '所有属性都已绑定' }}
            </div>
            <table v-else class="w-full text-[12px]">
              <tbody>
                <tr
                  v-for="attr in filteredUnboundAttrs"
                  :key="attr.attributeId"
                  class="border-b border-border last:border-0 hover:bg-subtle"
                >
                  <td class="px-3 py-2 font-medium">{{ attr.name }}</td>
                  <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ attr.code ?? '-' }}</td>
                  <td class="px-3 py-2">
                    <BadgeVue variant="info">{{ attrTypeLabel[attr.attributeType] ?? attr.attributeType }}</BadgeVue>
                  </td>
                  <td class="px-3 py-2 text-right">
                    <button
                      class="flex items-center gap-1 text-primary text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 ml-auto"
                      @click="addAttrDialog.visible = false; openBindAttrDialog(attr)"
                    >
                      <Link2 :size="11" />选择
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <div class="flex justify-end shrink-0">
            <ButtonVue variant="outline" size="sm" @click="addAttrDialog.visible = false">关闭</ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Bind attribute config dialog -->
    <Teleport to="body">
      <div v-if="bindAttrDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="bindAttrDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[400px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">配置属性绑定</h3>
          <div class="bg-muted rounded-[var(--radius)] px-3 py-2 text-[12px] flex items-center gap-2">
            <BadgeVue variant="info">{{ attrTypeLabel[bindAttrDialog.attr?.attributeType ?? ''] }}</BadgeVue>
            <span class="font-medium">{{ bindAttrDialog.attr?.name }}</span>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1 col-span-2">
              <label class="text-[12px] text-muted-foreground">属性分组</label>
              <select
                v-model="bindAttrDialog.groupType"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
              >
                <option v-for="(label, val) in groupTypeLabel" :key="val" :value="val">{{ label }}</option>
              </select>
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">排序</label>
              <input
                v-model.number="bindAttrDialog.orderNum"
                type="number"
                min="0"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
              />
            </div>
            <div class="space-y-2 flex flex-col justify-center">
              <label class="flex items-center gap-2 cursor-pointer text-[12px]">
                <input v-model="bindAttrDialog.isRequired" type="checkbox" class="cursor-pointer" />
                必填属性
              </label>
              <label v-if="bindAttrDialog.attr?.attributeType === 'enum'" class="flex items-center gap-2 cursor-pointer text-[12px]">
                <input v-model="bindAttrDialog.isSaleAxis" type="checkbox" class="cursor-pointer" />
                销售轴（SKU 维度）
              </label>
            </div>
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="bindAttrDialog.visible = false">取消</ButtonVue>
            <ButtonVue size="sm" :disabled="bindAttrDialog.loading" @click="saveBindAttr">
              {{ bindAttrDialog.loading ? '绑定中...' : '确认绑定' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
