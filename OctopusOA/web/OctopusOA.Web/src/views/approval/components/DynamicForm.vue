<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import type { FormExtension, FormField, SubField } from '@/api/approval/types'
import { getFormComponent } from '@/lib/formComponentRegistry'
import { evaluate, evaluateValue } from '@/lib/formExpr'
import { createAuthedHttp } from '@/utils/http'
import { Select, SelectTrigger, SelectContent, SelectItem, SelectValue } from '@/components/ui/select'

const props = defineProps<{
  fields: FormField[]
  modelValue: Record<string, unknown>
  readonly?: boolean
  extensions?: FormExtension[]
}>()

const beforeExtensions = computed(() =>
  (props.extensions ?? []).filter(e => (e.slot ?? 'after_form') === 'before_form')
)
const afterExtensions = computed(() =>
  (props.extensions ?? []).filter(e => (e.slot ?? 'after_form') !== 'before_form')
)

const emit = defineEmits<{
  (e: 'update:modelValue', val: Record<string, unknown>): void
}>()

const errors = ref<Record<string, string>>({})

const formData = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
})

function updateField(key: string, value: unknown) {
  emit('update:modelValue', { ...props.modelValue, [key]: value })
  if (errors.value[key]) delete errors.value[key]
}

function selectLabel(field: FormField, value: unknown): string {
  const str = String(value ?? '')
  const opts = dynamicOptions.value[field.key] ?? field.options ?? []
  return opts.find(o => o.value === str)?.label ?? str
}

function resolveFieldComponent(field: FormField) {
  return field.componentId ? getFormComponent(field.componentId) : undefined
}

// ── 字段联动：visibleWhen 为真（或无条件）才显示 ──
const visibleFields = computed(() =>
  props.fields.filter(f => !f.visibleWhen || evaluate(f.visibleWhen, props.modelValue))
)

// ── 关联数据下拉：异步加载选项 ──
const http = createAuthedHttp('/api')
const dynamicOptions = ref<Record<string, { label: string; value: string }[]>>({})

async function loadOptions(field: FormField) {
  const from = field.optionsFrom
  if (!from?.source) return
  try {
    let rows: Record<string, unknown>[] = []
    if (from.source.startsWith('dict:')) {
      const code = from.source.slice(5)
      const res: unknown = await http.get(`/system/dict/data/${encodeURIComponent(code)}`)
      rows = extractRows(res)
    } else {
      const res: unknown = await http.get(from.source)
      rows = extractRows(res)
    }
    const labelField = from.labelField || 'label'
    const valueField = from.valueField || 'value'
    dynamicOptions.value = {
      ...dynamicOptions.value,
      [field.key]: rows.map(r => ({
        label: String(r[labelField] ?? ''),
        value: String(r[valueField] ?? ''),
      })),
    }
  } catch {
    // 加载失败时回退到静态 options（若配置）
  }
}

function extractRows(res: unknown): Record<string, unknown>[] {
  if (Array.isArray(res)) return res as Record<string, unknown>[]
  if (res && typeof res === 'object') {
    const obj = res as Record<string, unknown>
    const data = obj.data ?? obj.rows ?? obj.result
    if (Array.isArray(data)) return data as Record<string, unknown>[]
  }
  return []
}

function fieldOptions(field: FormField): { label: string; value: string }[] {
  return dynamicOptions.value[field.key] ?? field.options ?? []
}

onMounted(() => {
  props.fields.filter(f => f.type === 'select' && f.optionsFrom?.source).forEach(loadOptions)
})

// ── 公式字段：随依赖字段变化自动计算并写回 ──
const formulaFields = computed(() => props.fields.filter(f => f.formula?.trim()))

watch(
  () => props.modelValue,
  (val) => {
    if (props.readonly) return
    let patched: Record<string, unknown> | null = null
    for (const f of formulaFields.value) {
      try {
        const result = evaluateValue(f.formula, val)
        if (result !== null && Object.is(val[f.key], result) === false) {
          if (!patched) patched = { ...val }
          patched[f.key] = result
        }
      } catch {
        // 公式非法或依赖缺失时忽略，不阻塞填写
      }
    }
    if (patched) emit('update:modelValue', patched)
  },
  { immediate: true, deep: true }
)

// ── 明细表：增删行 ──
function subRows(field: FormField): Record<string, unknown>[] {
  const v = props.modelValue[field.key]
  return Array.isArray(v) ? (v as Record<string, unknown>[]) : []
}

function addSubRow(field: FormField) {
  const row: Record<string, unknown> = {}
  ;(field.subFields ?? []).forEach(sf => { row[sf.key] = sf.type === 'number' ? 0 : '' })
  updateField(field.key, [...subRows(field), row])
}

function removeSubRow(field: FormField, index: number) {
  const rows = subRows(field).slice()
  rows.splice(index, 1)
  updateField(field.key, rows)
}

function updateSubRow(field: FormField, index: number, key: string, value: unknown) {
  const rows = subRows(field).slice()
  rows[index] = { ...rows[index], [key]: value }
  updateField(field.key, rows)
}

function subFieldLabel(sf: SubField) { return sf.label || sf.key }

async function validate(): Promise<boolean> {
  errors.value = {}
  let valid = true
  for (const f of visibleFields.value) {
    if (f.formula?.trim()) continue
    if (f.type === 'subform') {
      if (f.required && subRows(f).length === 0) {
        errors.value[f.key] = `请至少添加一行${f.label}`
        valid = false
      }
      continue
    }
    if (f.required && !props.modelValue[f.key]) {
      errors.value[f.key] = `${f.label}不能为空`
      valid = false
    }
  }
  return valid
}

defineExpose({ validate })
</script>

<template>
  <div class="space-y-4">
    <!-- 扩展视图：表单之前 -->
    <template v-for="(ext, i) in beforeExtensions" :key="'ext-b-' + i">
      <div class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div v-if="ext.title" class="px-3 py-2 text-[12px] font-medium bg-subtle border-b border-border">{{ ext.title }}</div>
        <iframe v-if="ext.type === 'iframe' && ext.url" :src="ext.url" :title="ext.title ?? '子系统视图'"
          class="w-full bg-card" :style="{ height: (Number(ext.height) || 240) + 'px' }" />
        <div v-else class="p-3 text-[11px] text-muted-foreground">
          {{ ext.type === 'component' ? `自定义组件 ${ext.componentId ?? ''}（未注册）` : '未配置视图 URL' }}
        </div>
      </div>
    </template>

    <div v-for="field in visibleFields" :key="field.key" class="flex gap-3">
      <label class="w-24 shrink-0 text-[12px] text-right text-foreground pt-1.5">
        <span v-if="field.required && !readonly" class="text-danger mr-0.5">*</span>
        {{ field.label }}
      </label>
      <div class="flex-1">

        <!-- ── 明细表 ── -->
        <template v-if="field.type === 'subform'">
          <div class="border border-border rounded-[var(--radius)] overflow-hidden">
            <table class="w-full text-[12px]">
              <thead>
                <tr class="bg-subtle text-[11px] text-muted-foreground">
                  <th v-for="sf in field.subFields ?? []" :key="sf.key" class="text-left px-2 py-1.5 font-medium">{{ subFieldLabel(sf) }}</th>
                  <th v-if="!readonly" class="w-10"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-border">
                <tr v-for="(row, ri) in subRows(field)" :key="ri">
                  <td v-for="sf in field.subFields ?? []" :key="sf.key" class="px-1.5 py-1">
                    <template v-if="readonly">{{ row[sf.key] ?? '—' }}</template>
                    <select v-else-if="sf.type === 'select'" :value="String(row[sf.key] ?? '')"
                      class="w-full h-[26px] px-1 border border-input rounded-[var(--radius)] bg-card text-[12px]"
                      @change="updateSubRow(field, ri, sf.key, ($event.target as HTMLSelectElement).value)">
                      <option v-for="opt in sf.options ?? []" :key="opt.value" :value="opt.value">{{ opt.label }}</option>
                    </select>
                    <input v-else-if="sf.type === 'number'" type="number" :value="(row[sf.key] ?? '') as number"
                      class="w-full h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono-tnum"
                      @input="updateSubRow(field, ri, sf.key, Number(($event.target as HTMLInputElement).value))" />
                    <input v-else-if="sf.type === 'date'" type="date" :value="(row[sf.key] ?? '') as string"
                      class="w-full h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px]"
                      @input="updateSubRow(field, ri, sf.key, ($event.target as HTMLInputElement).value)" />
                    <input v-else type="text" :value="(row[sf.key] ?? '') as string"
                      class="w-full h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px]"
                      @input="updateSubRow(field, ri, sf.key, ($event.target as HTMLInputElement).value)" />
                  </td>
                  <td v-if="!readonly" class="text-center">
                    <button class="text-danger border-0 bg-transparent cursor-pointer" @click="removeSubRow(field, ri)">×</button>
                  </td>
                </tr>
                <tr v-if="!subRows(field).length">
                  <td :colspan="(field.subFields?.length ?? 0) + (readonly ? 0 : 1)" class="text-center py-3 text-[11px] text-muted-foreground">暂无明细行</td>
                </tr>
              </tbody>
            </table>
          </div>
          <button v-if="!readonly" class="mt-1 text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="addSubRow(field)">
            + 添加一行
          </button>
        </template>

        <!-- ── Readonly display ── -->
        <template v-else-if="readonly">
          <component v-if="resolveFieldComponent(field)" :is="resolveFieldComponent(field)"
            :model-value="formData[field.key]" :readonly="true" />
          <span v-else-if="field.type === 'select'" class="flex items-center h-[var(--row-h)] text-[12px] text-foreground">
            {{ selectLabel(field, formData[field.key]) }}
          </span>
          <span v-else class="flex items-center min-h-[var(--row-h)] text-[12px] text-foreground whitespace-pre-wrap">
            {{ formData[field.key] ?? '—' }}
          </span>
        </template>

        <!-- ── Editable controls ── -->
        <template v-else>
          <!-- 公式字段：只读展示计算值 -->
          <div v-if="field.formula?.trim()"
            class="flex items-center h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-subtle text-[12px] text-foreground font-mono-tnum">
            {{ formData[field.key] ?? '—' }}
          </div>
          <!-- L3 自定义组件优先 -->
          <component v-else-if="resolveFieldComponent(field)" :is="resolveFieldComponent(field)"
            :model-value="formData[field.key]"
            @update:model-value="updateField(field.key, $event)" />
          <!-- Select -->
          <Select
            v-else-if="field.type === 'select'"
            :model-value="(formData[field.key] ?? '') as string"
            @update:model-value="updateField(field.key, $event)"
          >
            <SelectTrigger class="w-full"><SelectValue :placeholder="`请选择${field.label}`" /></SelectTrigger>
            <SelectContent>
              <SelectItem v-for="opt in fieldOptions(field)" :key="opt.value" :value="opt.value">{{ opt.label }}</SelectItem>
            </SelectContent>
          </Select>

          <!-- Date -->
          <input
            v-else-if="field.type === 'date'"
            type="date"
            :value="(formData[field.key] ?? '') as string"
            class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            @input="updateField(field.key, ($event.target as HTMLInputElement).value)"
          />

          <!-- Number -->
          <input
            v-else-if="field.type === 'number'"
            type="number"
            :value="(formData[field.key] ?? '') as number"
            min="0"
            class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring font-mono-tnum"
            @input="updateField(field.key, Number(($event.target as HTMLInputElement).value))"
          />

          <!-- Textarea -->
          <textarea
            v-else-if="field.type === 'textarea'"
            :value="(formData[field.key] ?? '') as string"
            rows="3"
            :placeholder="`请输入${field.label}`"
            class="w-full px-3 py-2 border border-input rounded-[var(--radius)] bg-card text-[12px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring resize-none"
            @input="updateField(field.key, ($event.target as HTMLInputElement).value)"
          />

          <!-- Text (default) -->
          <input
            v-else
            type="text"
            :value="(formData[field.key] ?? '') as string"
            :placeholder="`请输入${field.label}`"
            class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            @input="updateField(field.key, ($event.target as HTMLInputElement).value)"
          />

          <p v-if="errors[field.key]" class="text-[11px] text-danger mt-1">{{ errors[field.key] }}</p>
        </template>

      </div>
    </div>

    <!-- 扩展视图：表单之后 -->
    <template v-for="(ext, i) in afterExtensions" :key="'ext-a-' + i">
      <div class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <div v-if="ext.title" class="px-3 py-2 text-[12px] font-medium bg-subtle border-b border-border">{{ ext.title }}</div>
        <iframe v-if="ext.type === 'iframe' && ext.url" :src="ext.url" :title="ext.title ?? '子系统视图'"
          class="w-full bg-card" :style="{ height: (Number(ext.height) || 240) + 'px' }" />
        <div v-else class="p-3 text-[11px] text-muted-foreground">
          {{ ext.type === 'component' ? `自定义组件 ${ext.componentId ?? ''}（未注册）` : '未配置视图 URL' }}
        </div>
      </div>
    </template>
  </div>
</template>
