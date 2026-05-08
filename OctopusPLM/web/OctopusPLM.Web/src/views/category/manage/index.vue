<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { ChevronRight, ChevronDown, Plus, Pencil, Trash2, FolderPlus, Link2, Unlink } from 'lucide-vue-next'
import ButtonVue from '@/components/ui/button.vue'
import InputVue from '@/components/ui/input.vue'
import BadgeVue from '@/components/ui/badge.vue'
import {
  getCategoryTree, createCategory, updateCategory, deleteCategory,
  getAttributeList, createAttribute, updateAttribute, deleteAttribute,
  addAttributeValue, deleteAttributeValue,
  getCategoryAttributes, bindCategoryAttribute, unbindCategoryAttribute,
} from '@/api/category'
import type { CategoryTreeNode, PlmAttribute, CategoryAttributeGrouped, CategoryAttributeResponse } from '@/api/category/types'

// ── Category tree ─────────────────────────────────────────────────────────────
const categoryTree = ref<CategoryTreeNode[]>([])
const expandedIds = ref<Set<number>>(new Set())
const selectedId = ref<number | null>(null)
const treeLoading = ref(false)

function toggleExpand(id: number) {
  if (expandedIds.value.has(id)) expandedIds.value.delete(id)
  else expandedIds.value.add(id)
}

async function loadTree() {
  treeLoading.value = true
  try {
    categoryTree.value = await getCategoryTree()
    const allIds = collectIds(categoryTree.value)
    allIds.forEach(id => expandedIds.value.add(id))
  } catch (e: unknown) {
    console.error((e as Error).message)
  } finally {
    treeLoading.value = false
  }
}

function collectIds(nodes: CategoryTreeNode[]): number[] {
  return nodes.flatMap(n => [n.categoryId, ...collectIds(n.children)])
}

function selectCategory(id: number) {
  selectedId.value = id
}

const selectedCategory = computed(() => {
  if (selectedId.value == null) return null
  return findNode(categoryTree.value, selectedId.value)
})

function findNode(nodes: CategoryTreeNode[], id: number): CategoryTreeNode | null {
  for (const n of nodes) {
    if (n.categoryId === id) return n
    const found = findNode(n.children, id)
    if (found) return found
  }
  return null
}

// ── Category CRUD dialog ──────────────────────────────────────────────────────
const catDialog = ref<{
  visible: boolean
  mode: 'create' | 'edit'
  parentId: number | null
  id: number
  name: string
  orderNum: number
  loading: boolean
}>({ visible: false, mode: 'create', parentId: null, id: 0, name: '', orderNum: 0, loading: false })

function openCreateCat(parentId: number | null) {
  catDialog.value = { visible: true, mode: 'create', parentId, id: 0, name: '', orderNum: 0, loading: false }
}

function openEditCat(node: CategoryTreeNode) {
  catDialog.value = { visible: true, mode: 'edit', parentId: node.parentId, id: node.categoryId, name: node.name, orderNum: node.orderNum, loading: false }
}

async function saveCat() {
  const d = catDialog.value
  if (!d.name.trim()) return
  d.loading = true
  try {
    if (d.mode === 'create') {
      await createCategory({ name: d.name, parentId: d.parentId, orderNum: d.orderNum })
    } else {
      await updateCategory(d.id, { name: d.name, orderNum: d.orderNum })
    }
    d.visible = false
    await loadTree()
  } catch (e: unknown) {
    console.error((e as Error).message)
  } finally {
    d.loading = false
  }
}

async function handleDeleteCat(id: number) {
  if (!confirm('确定删除该类目？')) return
  try {
    await deleteCategory(id)
    if (selectedId.value === id) selectedId.value = null
    await loadTree()
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

// ── Category bound attributes ─────────────────────────────────────────────────
const boundGroups = ref<CategoryAttributeGrouped[]>([])
const boundLoading = ref(false)

const boundAttributes = computed((): CategoryAttributeResponse[] =>
  boundGroups.value.flatMap(g => g.attributes)
)

const boundAttributeIds = computed(() => new Set(boundAttributes.value.map(a => a.attributeId)))

async function loadBoundAttributes() {
  if (selectedId.value == null) { boundGroups.value = []; return }
  boundLoading.value = true
  try {
    boundGroups.value = await getCategoryAttributes(selectedId.value)
  } catch (e: unknown) {
    console.error((e as Error).message)
  } finally {
    boundLoading.value = false
  }
}

watch(selectedId, () => { loadBoundAttributes() })

async function handleUnbind(bindId: number) {
  if (!confirm('确定解绑该属性？')) return
  try {
    await unbindCategoryAttribute(selectedId.value!, bindId)
    await loadBoundAttributes()
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

// ── Bind dialog ───────────────────────────────────────────────────────────────
const bindDialog = ref<{
  visible: boolean
  attr: PlmAttribute | null
  isRequired: boolean
  isSaleAxis: boolean
  groupType: string
  groupName: string
  orderNum: number
  loading: boolean
}>({ visible: false, attr: null, isRequired: false, isSaleAxis: false, groupType: 'basic', groupName: '', orderNum: 0, loading: false })

function openBindDialog(attr: PlmAttribute) {
  bindDialog.value = {
    visible: true,
    attr,
    isRequired: false,
    isSaleAxis: attr.attributeType === 'enum',
    groupType: 'basic',
    groupName: '',
    orderNum: 0,
    loading: false,
  }
}

async function saveBind() {
  const d = bindDialog.value
  if (!d.attr || selectedId.value == null) return
  d.loading = true
  try {
    await bindCategoryAttribute(selectedId.value, {
      attributeId: d.attr.attributeId,
      isRequired: d.isRequired,
      isSaleAxis: d.isSaleAxis,
      groupType: d.groupType,
      groupName: d.groupName || undefined,
      orderNum: d.orderNum,
    })
    d.visible = false
    await loadBoundAttributes()
  } catch (e: unknown) {
    alert((e as Error).message)
  } finally {
    d.loading = false
  }
}

// ── Attribute list ────────────────────────────────────────────────────────────
const attributes = ref<PlmAttribute[]>([])
const attrLoading = ref(false)
const attrKeyword = ref('')

const filteredAttributes = computed(() => {
  const kw = attrKeyword.value.toLowerCase()
  return kw ? attributes.value.filter(a => a.name.toLowerCase().includes(kw) || (a.code ?? '').toLowerCase().includes(kw)) : attributes.value
})

async function loadAttributes() {
  attrLoading.value = true
  try { attributes.value = await getAttributeList() }
  catch (e: unknown) { console.error((e as Error).message) }
  finally { attrLoading.value = false }
}

// ── Attribute CRUD dialog ─────────────────────────────────────────────────────
const attrDialog = ref<{
  visible: boolean
  mode: 'create' | 'edit'
  id: number
  name: string
  code: string
  attributeType: string
  inputType: string
  unit: string
  valueScope: string
  loading: boolean
}>({ visible: false, mode: 'create', id: 0, name: '', code: '', attributeType: 'text', inputType: 'single_line', unit: '', valueScope: 'global', loading: false })

function openCreateAttr() {
  attrDialog.value = { visible: true, mode: 'create', id: 0, name: '', code: '', attributeType: 'text', inputType: 'single_line', unit: '', valueScope: 'global', loading: false }
}

function openEditAttr(attr: PlmAttribute) {
  attrDialog.value = { visible: true, mode: 'edit', id: attr.attributeId, name: attr.name, code: attr.code ?? '', attributeType: attr.attributeType, inputType: attr.inputType, unit: attr.unit ?? '', valueScope: attr.valueScope, loading: false }
}

async function saveAttr() {
  const d = attrDialog.value
  if (!d.name.trim()) return
  d.loading = true
  try {
    if (d.mode === 'create') {
      await createAttribute({ name: d.name, code: d.code || undefined, attributeType: d.attributeType, inputType: d.inputType, unit: d.unit || undefined, valueScope: d.valueScope })
    } else {
      await updateAttribute(d.id, { name: d.name, unit: d.unit || undefined })
    }
    d.visible = false
    await loadAttributes()
  } catch (e: unknown) {
    console.error((e as Error).message)
  } finally {
    d.loading = false
  }
}

async function handleDeleteAttr(id: number) {
  if (!confirm('确定删除该属性？')) return
  try { await deleteAttribute(id); await loadAttributes() }
  catch (e: unknown) { alert((e as Error).message) }
}

// ── Attribute value panel ─────────────────────────────────────────────────────
const selectedAttr = ref<PlmAttribute | null>(null)
const valueDialog = ref<{ visible: boolean; label: string; value: string; orderNum: number; loading: boolean }>({
  visible: false, label: '', value: '', orderNum: 0, loading: false,
})

function openAddValue(attr: PlmAttribute) {
  selectedAttr.value = attr
  valueDialog.value = { visible: true, label: '', value: '', orderNum: 0, loading: false }
}

async function saveValue() {
  const d = valueDialog.value
  if (!selectedAttr.value || !d.label.trim() || !d.value.trim()) return
  d.loading = true
  try {
    await addAttributeValue(selectedAttr.value.attributeId, { label: d.label, value: d.value, orderNum: d.orderNum })
    d.visible = false
    await loadAttributes()
    selectedAttr.value = attributes.value.find(a => a.attributeId === selectedAttr.value!.attributeId) ?? null
  } catch (e: unknown) {
    console.error((e as Error).message)
  } finally {
    d.loading = false
  }
}

async function handleDeleteValue(attrId: number, valueId: number) {
  if (!confirm('确定删除该枚举值？')) return
  try {
    await deleteAttributeValue(valueId)
    await loadAttributes()
    selectedAttr.value = attributes.value.find(a => a.attributeId === attrId) ?? null
  } catch (e: unknown) {
    alert((e as Error).message)
  }
}

const attrTypeLabel: Record<string, string> = { text: '文本', number: '数值', enum: '枚举', boolean: '布尔', date: '日期' }
const groupTypeLabel: Record<string, string> = { basic: '基础属性', sale: '销售属性', logistics: '物流属性', compliance: '合规属性' }

onMounted(() => { loadTree(); loadAttributes() })
</script>

<template>
  <div class="flex gap-3 h-[calc(100vh-120px)]">
    <!-- Left: Category tree -->
    <div class="w-[240px] flex-shrink-0 flex flex-col border border-border rounded-[var(--radius-lg)] bg-card overflow-hidden">
      <div class="px-3 py-2.5 border-b border-border flex items-center justify-between">
        <span class="text-[13px] font-semibold">类目树</span>
        <ButtonVue variant="ghost" size="sm" title="新增根类目" @click="openCreateCat(null)">
          <Plus :size="13" />
        </ButtonVue>
      </div>
      <div class="flex-1 overflow-y-auto py-1">
        <div v-if="treeLoading" class="text-center py-6 text-[12px] text-muted-foreground">加载中...</div>
        <template v-else>
          <div v-for="node in categoryTree" :key="node.categoryId">
            <CategoryNode
              :node="node"
              :selected-id="selectedId"
              :expanded-ids="expandedIds"
              @select="selectCategory"
              @toggle="toggleExpand"
              @add-child="openCreateCat"
              @edit="openEditCat"
              @delete="handleDeleteCat"
            />
          </div>
          <div v-if="categoryTree.length === 0" class="text-center py-6 text-[12px] text-muted-foreground">
            暂无类目，点击 + 新建
          </div>
        </template>
      </div>
    </div>

    <!-- Middle: Category bound attributes (shown when category selected) -->
    <div class="w-[280px] flex-shrink-0 flex flex-col border border-border rounded-[var(--radius-lg)] bg-card overflow-hidden">
      <div class="px-3 py-2.5 border-b border-border flex items-center gap-2">
        <span class="text-[13px] font-semibold truncate">
          {{ selectedCategory ? selectedCategory.name : '类目属性' }}
        </span>
        <Link2 :size="13" class="shrink-0 text-muted-foreground" />
      </div>
      <div class="flex-1 overflow-y-auto">
        <div v-if="!selectedId" class="flex flex-col items-center justify-center h-full text-[12px] text-muted-foreground gap-1.5 py-10">
          <Link2 :size="28" class="opacity-30" />
          <span>请选择左侧类目</span>
          <span class="text-[11px]">查看已绑定属性</span>
        </div>
        <div v-else-if="boundLoading" class="text-center py-8 text-[12px] text-muted-foreground">加载中...</div>
        <template v-else>
          <div v-if="boundAttributes.length === 0" class="flex flex-col items-center justify-center py-10 text-[12px] text-muted-foreground gap-1">
            <span>暂无绑定属性</span>
            <span class="text-[11px]">从右侧属性库点击「绑定」</span>
          </div>
          <div v-else>
            <div v-for="group in boundGroups" :key="group.groupName ?? 'default'" class="mb-1">
              <div class="px-3 pt-2 pb-1 text-[10px] uppercase tracking-[0.06em] text-muted-foreground font-medium">
                {{ group.groupLabel }}
              </div>
              <div
                v-for="attr in group.attributes"
                :key="attr.id"
                class="flex items-center gap-2 px-3 py-2 border-b border-border last:border-0 hover:bg-subtle group/row"
              >
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-1.5">
                    <span class="text-[12px] font-medium truncate">{{ attr.name }}</span>
                    <BadgeVue v-if="attr.isRequired" variant="danger" class="text-[9px] px-1 py-0 shrink-0">必填</BadgeVue>
                    <BadgeVue v-if="attr.isSaleAxis" variant="primary" class="text-[9px] px-1 py-0 shrink-0">销售轴</BadgeVue>
                  </div>
                  <div class="text-[11px] text-muted-foreground">
                    {{ attrTypeLabel[attr.attributeType] ?? attr.attributeType }}
                    <span v-if="attr.unit"> · {{ attr.unit }}</span>
                  </div>
                </div>
                <button
                  class="opacity-0 group-hover/row:opacity-100 transition-opacity p-1 rounded hover:bg-danger/10 text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent shrink-0"
                  title="解绑"
                  @click="handleUnbind(attr.id)"
                >
                  <Unlink :size="12" />
                </button>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>

    <!-- Right: Attribute library -->
    <div class="flex-1 flex flex-col border border-border rounded-[var(--radius-lg)] bg-card overflow-hidden">
      <div class="px-4 py-2.5 border-b border-border flex items-center gap-2">
        <span class="text-[13px] font-semibold">属性库</span>
        <div class="flex-1" />
        <InputVue v-model="attrKeyword" placeholder="搜索属性..." class="w-[180px]" />
        <ButtonVue size="sm" @click="openCreateAttr">
          <Plus :size="13" />
          新增属性
        </ButtonVue>
      </div>
      <div class="flex-1 overflow-y-auto">
        <div v-if="attrLoading" class="text-center py-8 text-[12px] text-muted-foreground">加载中...</div>
        <table v-else class="w-full text-[12px]">
          <thead>
            <tr>
              <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border">属性名称</th>
              <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border">编码</th>
              <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border">类型</th>
              <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border">单位</th>
              <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border w-[240px]">枚举值</th>
              <th class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="filteredAttributes.length === 0">
              <td colspan="6" class="text-center py-10 text-muted-foreground text-[12px]">暂无属性</td>
            </tr>
            <tr
              v-for="attr in filteredAttributes"
              :key="attr.attributeId"
              class="border-b border-border last:border-0 hover:bg-subtle transition-colors align-top"
            >
              <td class="px-3 py-2 font-medium">{{ attr.name }}</td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ attr.code ?? '-' }}</td>
              <td class="px-3 py-2">
                <BadgeVue variant="info">{{ attrTypeLabel[attr.attributeType] ?? attr.attributeType }}</BadgeVue>
              </td>
              <td class="px-3 py-2 text-muted-foreground">{{ attr.unit ?? '-' }}</td>
              <td class="px-3 py-2">
                <div v-if="attr.attributeType === 'enum'" class="flex flex-wrap gap-1 items-center">
                  <div
                    v-for="v in attr.values"
                    :key="v.valueId"
                    class="flex items-center gap-0.5 bg-muted rounded px-1.5 py-0.5 text-[11px]"
                  >
                    <span>{{ v.label }}</span>
                    <button
                      class="text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent p-0 leading-none ml-0.5"
                      title="删除枚举值"
                      @click="handleDeleteValue(attr.attributeId, v.valueId)"
                    >×</button>
                  </div>
                  <button
                    class="text-primary text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                    @click="openAddValue(attr)"
                  >+ 添加</button>
                </div>
                <span v-else class="text-muted-foreground text-[11px]">-</span>
              </td>
              <td class="px-3 py-2">
                <div class="flex items-center gap-2 flex-wrap">
                  <button
                    v-if="selectedId && !boundAttributeIds.has(attr.attributeId)"
                    class="text-success text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                    title="绑定到选中类目"
                    @click="openBindDialog(attr)"
                  >
                    <Link2 :size="12" class="inline mr-0.5" />绑定
                  </button>
                  <BadgeVue v-else-if="selectedId && boundAttributeIds.has(attr.attributeId)" variant="success" class="text-[9px] px-1.5">已绑定</BadgeVue>
                  <button
                    class="text-primary text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                    @click="openEditAttr(attr)"
                  >
                    <Pencil :size="12" class="inline mr-0.5" />编辑
                  </button>
                  <button
                    class="text-danger text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0"
                    @click="handleDeleteAttr(attr.attributeId)"
                  >
                    <Trash2 :size="12" class="inline mr-0.5" />删除
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Category dialog -->
    <Teleport to="body">
      <div v-if="catDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="catDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[380px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">{{ catDialog.mode === 'create' ? '新增类目' : '编辑类目' }}</h3>
          <div class="space-y-3">
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">类目名称 <span class="text-danger">*</span></label>
              <InputVue v-model="catDialog.name" placeholder="请输入类目名称" />
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">排序</label>
              <input
                v-model.number="catDialog.orderNum"
                type="number"
                min="0"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              />
            </div>
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="catDialog.visible = false">取消</ButtonVue>
            <ButtonVue size="sm" :disabled="catDialog.loading || !catDialog.name.trim()" @click="saveCat">
              {{ catDialog.loading ? '保存中...' : '保存' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Bind attribute dialog -->
    <Teleport to="body">
      <div v-if="bindDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="bindDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[420px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">
            绑定属性到「{{ selectedCategory?.name }}」
          </h3>
          <div class="bg-muted rounded-[var(--radius)] px-3 py-2 text-[12px] flex items-center gap-2">
            <BadgeVue variant="info">{{ attrTypeLabel[bindDialog.attr?.attributeType ?? ''] }}</BadgeVue>
            <span class="font-medium">{{ bindDialog.attr?.name }}</span>
            <span v-if="bindDialog.attr?.unit" class="text-muted-foreground">（{{ bindDialog.attr.unit }}）</span>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1 col-span-2">
              <label class="text-[12px] text-muted-foreground">属性分组</label>
              <select
                v-model="bindDialog.groupType"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
              >
                <option v-for="(label, val) in groupTypeLabel" :key="val" :value="val">{{ label }}</option>
              </select>
            </div>
            <div class="space-y-1 col-span-2">
              <label class="text-[12px] text-muted-foreground">分组名称（可选）</label>
              <InputVue v-model="bindDialog.groupName" placeholder="自定义分组名，留空则用分组类型名" />
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">排序</label>
              <input
                v-model.number="bindDialog.orderNum"
                type="number"
                min="0"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
              />
            </div>
            <div class="space-y-2 flex flex-col justify-center">
              <label class="flex items-center gap-2 cursor-pointer text-[12px]">
                <input v-model="bindDialog.isRequired" type="checkbox" class="cursor-pointer" />
                必填属性
              </label>
              <label v-if="bindDialog.attr?.attributeType === 'enum'" class="flex items-center gap-2 cursor-pointer text-[12px]">
                <input v-model="bindDialog.isSaleAxis" type="checkbox" class="cursor-pointer" />
                销售轴（SKU 维度）
              </label>
            </div>
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="bindDialog.visible = false">取消</ButtonVue>
            <ButtonVue size="sm" :disabled="bindDialog.loading" @click="saveBind">
              {{ bindDialog.loading ? '绑定中...' : '确认绑定' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Attribute CRUD dialog -->
    <Teleport to="body">
      <div v-if="attrDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="attrDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[460px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">{{ attrDialog.mode === 'create' ? '新增属性' : '编辑属性' }}</h3>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1 col-span-2">
              <label class="text-[12px] text-muted-foreground">属性名称 <span class="text-danger">*</span></label>
              <InputVue v-model="attrDialog.name" placeholder="属性名称" />
            </div>
            <div v-if="attrDialog.mode === 'create'" class="space-y-1">
              <label class="text-[12px] text-muted-foreground">属性编码（可选）</label>
              <InputVue v-model="attrDialog.code" placeholder="唯一编码" />
            </div>
            <div v-if="attrDialog.mode === 'create'" class="space-y-1">
              <label class="text-[12px] text-muted-foreground">属性类型</label>
              <select
                v-model="attrDialog.attributeType"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
              >
                <option value="text">文本</option>
                <option value="number">数值</option>
                <option value="enum">枚举</option>
                <option value="boolean">布尔</option>
                <option value="date">日期</option>
              </select>
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">单位</label>
              <InputVue v-model="attrDialog.unit" placeholder="如：kg、mm（可选）" />
            </div>
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="attrDialog.visible = false">取消</ButtonVue>
            <ButtonVue size="sm" :disabled="attrDialog.loading || !attrDialog.name.trim()" @click="saveAttr">
              {{ attrDialog.loading ? '保存中...' : '保存' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Add enum value dialog -->
    <Teleport to="body">
      <div v-if="valueDialog.visible" class="fixed inset-0 z-50 flex items-center justify-center" @click.self="valueDialog.visible = false">
        <div class="absolute inset-0 bg-black/50" />
        <div class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[380px] p-6 space-y-4">
          <h3 class="text-[14px] font-semibold">添加枚举值 — {{ selectedAttr?.name }}</h3>
          <div class="grid grid-cols-2 gap-3">
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">显示名称 <span class="text-danger">*</span></label>
              <InputVue v-model="valueDialog.label" placeholder="如：红色" />
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">值 <span class="text-danger">*</span></label>
              <InputVue v-model="valueDialog.value" placeholder="如：red" />
            </div>
            <div class="space-y-1">
              <label class="text-[12px] text-muted-foreground">排序</label>
              <input
                v-model.number="valueDialog.orderNum"
                type="number"
                min="0"
                class="w-full h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-3 text-[12px] text-foreground focus-visible:outline-none"
              />
            </div>
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue variant="outline" size="sm" @click="valueDialog.visible = false">取消</ButtonVue>
            <ButtonVue
              size="sm"
              :disabled="valueDialog.loading || !valueDialog.label.trim() || !valueDialog.value.trim()"
              @click="saveValue"
            >
              {{ valueDialog.loading ? '添加中...' : '添加' }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<!-- Inline recursive tree node -->
<script lang="ts">
import { defineComponent, h, PropType } from 'vue'
import { ChevronRight, ChevronDown, Plus, Pencil, Trash2, FolderPlus } from 'lucide-vue-next'
import type { CategoryTreeNode } from '@/api/category/types'

const CategoryNode = defineComponent({
  name: 'CategoryNode',
  props: {
    node: { type: Object as PropType<CategoryTreeNode>, required: true },
    selectedId: { type: Number as PropType<number | null>, default: null },
    expandedIds: { type: Object as PropType<Set<number>>, required: true },
    depth: { type: Number, default: 0 },
  },
  emits: ['select', 'toggle', 'addChild', 'edit', 'delete'],
  setup(props, { emit }) {
    return () => {
      const node = props.node
      const isExpanded = props.expandedIds.has(node.categoryId)
      const isSelected = props.selectedId === node.categoryId
      const hasChildren = node.children.length > 0
      const indent = props.depth * 16

      const icon = hasChildren
        ? h(isExpanded ? ChevronDown : ChevronRight, { size: 14, class: 'shrink-0 text-muted-foreground' })
        : h('span', { class: 'w-[14px]' })

      const row = h('div', {
        class: [
          'flex items-center gap-1 px-2 py-1.5 cursor-pointer rounded-[var(--radius)] text-[12px] group hover:bg-subtle transition-colors',
          isSelected ? 'bg-[var(--primary-soft)] text-[var(--primary-soft-fg)]' : 'text-foreground',
        ],
        style: { paddingLeft: `${8 + indent}px` },
        onClick: () => { emit('select', node.categoryId); if (hasChildren) emit('toggle', node.categoryId) },
      }, [
        icon,
        h('span', { class: 'flex-1 truncate' }, node.name),
        h('span', { class: 'text-[10px] text-muted-foreground font-mono-tnum opacity-60' }, `L${node.level}`),
        h('div', { class: 'flex items-center gap-0.5 opacity-0 group-hover:opacity-100 transition-opacity', onClick: (e: MouseEvent) => e.stopPropagation() }, [
          h('button', { class: 'p-0.5 rounded hover:bg-primary/10 text-muted-foreground hover:text-primary cursor-pointer border-0 bg-transparent', title: '添加子类目', onClick: () => emit('addChild', node.categoryId) },
            h(FolderPlus, { size: 12 })),
          h('button', { class: 'p-0.5 rounded hover:bg-primary/10 text-muted-foreground hover:text-primary cursor-pointer border-0 bg-transparent', title: '编辑', onClick: () => emit('edit', node) },
            h(Pencil, { size: 12 })),
          h('button', { class: 'p-0.5 rounded hover:bg-danger/10 text-muted-foreground hover:text-danger cursor-pointer border-0 bg-transparent', title: '删除', onClick: () => emit('delete', node.categoryId) },
            h(Trash2, { size: 12 })),
        ]),
      ])

      const children = (isExpanded && hasChildren)
        ? h('div', {}, node.children.map(child =>
            h(CategoryNode, {
              node: child,
              selectedId: props.selectedId,
              expandedIds: props.expandedIds,
              depth: props.depth + 1,
              onSelect: (id: number) => emit('select', id),
              onToggle: (id: number) => emit('toggle', id),
              onAddChild: (id: number) => emit('addChild', id),
              onEdit: (n: CategoryTreeNode) => emit('edit', n),
              onDelete: (id: number) => emit('delete', id),
            })
          ))
        : null

      return h('div', {}, [row, children])
    }
  },
})

export { CategoryNode }
</script>
