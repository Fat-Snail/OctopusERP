<script setup lang="ts">
import { ref, computed } from 'vue'
import { Plus, Trash2, ArrowUp, ArrowDown, Braces, Link2, Sigma, Table2 } from '@lucide/vue'
import type { FormExtension, FormField, SubField } from '@/api/approval/types'
import { validateExpression } from '@/lib/formExpr'

const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()

const FIELD_TYPES: { type: FormField['type']; label: string }[] = [
  { type: 'text', label: '单行文本' },
  { type: 'textarea', label: '多行文本' },
  { type: 'number', label: '数字' },
  { type: 'select', label: '下拉选择' },
  { type: 'date', label: '日期' },
  { type: 'file', label: '文件' },
  { type: 'subform', label: '明细表' },
]

const SUBFIELD_TYPES: { type: SubField['type']; label: string }[] = [
  { type: 'text', label: '文本' },
  { type: 'number', label: '数字' },
  { type: 'date', label: '日期' },
  { type: 'select', label: '下拉' },
]

interface ParsedSchema { fields: FormField[]; extensions: FormExtension[] }

function parse(value: string): ParsedSchema {
  try {
    const parsed = JSON.parse(value) as { fields?: FormField[]; extensions?: FormExtension[] }
    return {
      fields: (parsed.fields ?? []).map(f => ({
        ...f,
        options: f.options ? f.options.map(o => ({ ...o })) : undefined,
        subFields: f.subFields ? f.subFields.map(s => ({ ...s, options: s.options ? s.options.map(o => ({ ...o })) : undefined })) : undefined,
      })),
      extensions: (parsed.extensions ?? []).map(e => ({ ...e })),
    }
  } catch {
    return { fields: [], extensions: [] }
  }
}

const initial = parse(props.modelValue)
const fields = ref<FormField[]>(initial.fields)
const extensions = ref<FormExtension[]>(initial.extensions)
const selected = ref<number | null>(null)
const showJson = ref(false)
const jsonText = ref('')

const selectedField = computed<FormField | null>(() =>
  selected.value != null ? fields.value[selected.value] ?? null : null
)

function buildPayload(): { fields: FormField[]; extensions?: FormExtension[] } {
  const payload: { fields: FormField[]; extensions?: FormExtension[] } = { fields: fields.value }
  if (extensions.value.length) payload.extensions = extensions.value
  return payload
}

function sync() {
  emit('update:modelValue', JSON.stringify(buildPayload()))
  if (showJson.value) jsonText.value = JSON.stringify(buildPayload(), null, 2)
}

function addField(type: FormField['type']) {
  const meta = FIELD_TYPES.find(t => t.type === type)
  const n = fields.value.length + 1
  const field: FormField = {
    key: `field${n}`,
    label: `${meta?.label ?? '字段'}${n}`,
    type,
    required: false,
    ...(type === 'select' ? { options: [{ label: '选项1', value: 'option1' }], optionsFrom: { source: '', labelField: 'label', valueField: 'value' } } : {}),
    ...(type === 'subform' ? { subFields: [
      { key: 'name', label: '名称', type: 'text' },
      { key: 'qty', label: '数量', type: 'number' },
    ] } : {}),
  }
  fields.value.push(field)
  selected.value = fields.value.length - 1
  sync()
}

function removeField(index: number) {
  fields.value.splice(index, 1)
  if (selected.value === index) selected.value = null
  else if (selected.value != null && selected.value > index) selected.value--
  sync()
}

function move(index: number, dir: -1 | 1) {
  const target = index + dir
  if (target < 0 || target >= fields.value.length) return
  const [item] = fields.value.splice(index, 1)
  fields.value.splice(target, 0, item)
  selected.value = target
  sync()
}

function addOption() {
  const f = selectedField.value
  if (!f) return
  f.options = [...(f.options ?? []), { label: `选项${(f.options?.length ?? 0) + 1}`, value: `option${(f.options?.length ?? 0) + 1}` }]
  sync()
}

function removeOption(index: number) {
  selectedField.value?.options?.splice(index, 1)
  sync()
}

// ── 高级特性：表达式校验 / 明细表列 / 关联下拉 ──
const visibleWhenError = computed(() =>
  selectedField.value?.visibleWhen ? validateExpression(selectedField.value.visibleWhen) : null
)
const formulaError = computed(() =>
  selectedField.value?.formula ? validateExpression(selectedField.value.formula) : null
)

function addSubField() {
  const f = selectedField.value
  if (!f) return
  f.subFields = [...(f.subFields ?? []), { key: `col${(f.subFields?.length ?? 0) + 1}`, label: `列${(f.subFields?.length ?? 0) + 1}`, type: 'text' }]
  sync()
}

function removeSubField(index: number) {
  selectedField.value?.subFields?.splice(index, 1)
  sync()
}

function addSubOption(sf: SubField) {
  sf.options = [...(sf.options ?? []), { label: '选项', value: `option${(sf.options?.length ?? 0) + 1}` }]
  sync()
}

function toggleOptionsFrom() {
  const f = selectedField.value
  if (!f) return
  if (f.optionsFrom) {
    delete f.optionsFrom
  } else {
    f.optionsFrom = { source: '', labelField: 'label', valueField: 'value' }
  }
  sync()
}

function openJson() {
  jsonText.value = JSON.stringify(buildPayload(), null, 2)
  showJson.value = true
}

function applyJson() {
  try {
    const parsed = parse(jsonText.value)
    fields.value = parsed.fields
    extensions.value = parsed.extensions
    selected.value = null
    sync()
  } catch {
    // 非法 JSON 忽略
  }
}

function addExtension() {
  extensions.value.push({ type: 'iframe', slot: 'after_form', title: '子系统信息', url: '', height: 240 })
  sync()
}

function removeExtension(index: number) {
  extensions.value.splice(index, 1)
  sync()
}

function typeLabel(type: string) {
  return FIELD_TYPES.find(t => t.type === type)?.label ?? type
}
</script>

<template>
  <div class="flex h-full min-h-[420px] flex-col">
    <!-- 字段调色板 -->
    <div class="flex flex-wrap items-center gap-1.5 pb-3 border-b border-border">
      <span class="text-[11px] text-muted-foreground mr-1">添加字段：</span>
      <button
        v-for="t in FIELD_TYPES"
        :key="t.type"
        class="flex items-center gap-1 px-2 py-1 text-[11px] rounded-[var(--radius)] border border-border bg-card hover:bg-muted cursor-pointer"
        @click="addField(t.type)"
      >
        <Plus :size="10" />{{ t.label }}
      </button>
      <button
        class="ml-auto flex items-center gap-1 px-2 py-1 text-[11px] rounded-[var(--radius)] border border-border bg-card hover:bg-muted cursor-pointer"
        @click="showJson = !showJson; showJson && openJson()"
      >
        <Braces :size="11" />{{ showJson ? '收起 JSON' : '高级 JSON' }}
      </button>
    </div>

    <div class="flex-1 flex gap-3 min-h-0 pt-3">
      <!-- 字段列表 -->
      <div class="w-[240px] shrink-0 overflow-y-auto">
        <div v-if="!fields.length" class="text-[11px] text-muted-foreground py-6 text-center">
          还没有字段，点上方按钮添加
        </div>
        <div
          v-for="(f, i) in fields"
          :key="i"
          :class="[
            'flex items-center gap-1 px-2 py-1.5 mb-1 rounded-[var(--radius)] border text-[12px] cursor-pointer',
            selected === i ? 'border-primary bg-primary-soft' : 'border-border bg-card hover:bg-muted',
          ]"
          @click="selected = i"
        >
          <span class="flex-1 truncate">{{ f.label }}</span>
          <span class="text-[10px] text-muted-foreground">{{ typeLabel(f.type) }}</span>
          <span class="text-danger text-[10px]">{{ f.required ? '*' : '' }}</span>
          <button class="text-muted-foreground hover:text-foreground border-0 bg-transparent cursor-pointer" @click.stop="move(i, -1)"><ArrowUp :size="11" /></button>
          <button class="text-muted-foreground hover:text-foreground border-0 bg-transparent cursor-pointer" @click.stop="move(i, 1)"><ArrowDown :size="11" /></button>
          <button class="text-danger hover:brightness-110 border-0 bg-transparent cursor-pointer" @click.stop="removeField(i)"><Trash2 :size="11" /></button>
        </div>
      </div>

      <!-- 属性面板 -->
      <div class="flex-1 min-w-0 overflow-y-auto">
        <template v-if="selectedField">
          <div class="grid grid-cols-2 gap-2">
            <div>
              <label class="block text-[11px] text-muted-foreground mb-0.5">字段标签</label>
              <input v-model="selectedField.label" @input="sync"
                class="w-full h-[28px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[11px] text-muted-foreground mb-0.5">字段键（英文）</label>
              <input v-model="selectedField.key" @input="sync"
                class="w-full h-[28px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono focus:outline-none focus:ring-1 focus:ring-ring" />
            </div>
            <div>
              <label class="block text-[11px] text-muted-foreground mb-0.5">类型</label>
              <select v-model="selectedField.type" @change="sync"
                class="w-full h-[28px] px-1.5 border border-input rounded-[var(--radius)] bg-card text-[12px]">
                <option v-for="t in FIELD_TYPES" :key="t.type" :value="t.type">{{ t.label }}</option>
              </select>
            </div>
            <div class="flex items-end">
              <label class="flex items-center gap-1.5 text-[12px] cursor-pointer">
                <input type="checkbox" v-model="selectedField.required" @change="sync" />
                必填
              </label>
            </div>
          </div>

          <div class="mt-2">
            <label class="block text-[11px] text-muted-foreground mb-0.5">自定义组件 ID（L3，可选）</label>
            <input v-model="selectedField.componentId" @input="sync" placeholder="如 demo:product-picker"
              class="w-full h-[28px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono focus:outline-none focus:ring-1 focus:ring-ring" />
            <p class="text-[10px] text-muted-foreground mt-0.5">设置后该字段用注册的 Vue 组件渲染，忽略「类型」。</p>
          </div>

          <div v-if="selectedField.type === 'select'" class="mt-3 pt-3 border-t border-border">
            <div class="flex items-center justify-between mb-1">
              <span class="text-[11px] text-muted-foreground">下拉选项</span>
              <button class="flex items-center gap-0.5 text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="addOption">
                <Plus :size="10" />添加选项
              </button>
            </div>
            <div v-for="(opt, oi) in selectedField.options ?? []" :key="oi" class="flex items-center gap-1.5 mb-1">
              <input v-model="opt.label" @input="sync" placeholder="显示文本"
                class="flex-1 h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px]" />
              <input v-model="opt.value" @input="sync" placeholder="值"
                class="w-[110px] h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono" />
              <button class="text-danger border-0 bg-transparent cursor-pointer" @click="removeOption(oi)"><Trash2 :size="11" /></button>
            </div>
          </div>

          <!-- 关联数据下拉（type=select） -->
          <div v-if="selectedField.type === 'select'" class="mt-3 pt-3 border-t border-border">
            <div class="flex items-center justify-between mb-1">
              <span class="flex items-center gap-1 text-[11px] text-muted-foreground"><Link2 :size="11" />关联数据下拉</span>
              <button class="text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="toggleOptionsFrom">
                {{ selectedField.optionsFrom ? '移除数据源' : '使用数据源' }}
              </button>
            </div>
            <template v-if="selectedField.optionsFrom">
              <input v-model="selectedField.optionsFrom.source" @input="sync" placeholder="dict:order_status 或 /api/xxx/options"
                class="w-full h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono mb-1" />
              <div class="flex items-center gap-1.5">
                <input v-model="selectedField.optionsFrom.labelField" @input="sync" placeholder="标签字段名 label"
                  class="flex-1 h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[11px]" />
                <input v-model="selectedField.optionsFrom.valueField" @input="sync" placeholder="值字段名 value"
                  class="flex-1 h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[11px]" />
              </div>
              <p class="text-[10px] text-muted-foreground mt-0.5">dict: 走系统字典；/api/ 走带认证接口，需返回数组或 {code,data}。</p>
            </template>
          </div>

          <!-- 字段联动（visibleWhen） -->
          <div class="mt-3 pt-3 border-t border-border">
            <label class="flex items-center gap-1 text-[11px] text-muted-foreground mb-1"><Link2 :size="11" />显示条件（联动）</label>
            <input v-model="selectedField.visibleWhen" @input="sync" placeholder="留空始终显示，如：amount > 1000 && type == 'buy'"
              class="w-full h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono" />
            <p v-if="visibleWhenError" class="text-[10px] text-danger mt-0.5">{{ visibleWhenError }}</p>
            <p v-else class="text-[10px] text-muted-foreground mt-0.5">表达式为真时显示该字段；支持字段引用、比较、&& || ! 与括号。</p>
          </div>

          <!-- 公式字段（formula） -->
          <div class="mt-3 pt-3 border-t border-border">
            <label class="flex items-center gap-1 text-[11px] text-muted-foreground mb-1"><Sigma :size="11" />计算公式（只读）</label>
            <input v-model="selectedField.formula" @input="sync" placeholder="留空非公式，如：quantity * price"
              class="w-full h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono" />
            <p v-if="formulaError" class="text-[10px] text-danger mt-0.5">{{ formulaError }}</p>
            <p v-else class="text-[10px] text-muted-foreground mt-0.5">额外支持 + - * / % 算术；结果自动写入字段，填写时只读。</p>
          </div>

          <!-- 明细表列（type=subform） -->
          <div v-if="selectedField.type === 'subform'" class="mt-3 pt-3 border-t border-border">
            <div class="flex items-center justify-between mb-1">
              <span class="flex items-center gap-1 text-[11px] text-muted-foreground"><Table2 :size="11" />明细表列</span>
              <button class="text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="addSubField">
                <Plus :size="10" />添加列
              </button>
            </div>
            <div v-for="(sf, si) in selectedField.subFields ?? []" :key="si" class="mb-1.5 p-1.5 rounded-[var(--radius)] border border-border">
              <div class="flex items-center gap-1.5 mb-1">
                <input v-model="sf.label" @input="sync" placeholder="列标题"
                  class="w-[80px] h-[24px] px-1.5 border border-input rounded-[var(--radius)] bg-card text-[11px]" />
                <input v-model="sf.key" @input="sync" placeholder="键"
                  class="w-[70px] h-[24px] px-1.5 border border-input rounded-[var(--radius)] bg-card text-[11px] font-mono" />
                <select v-model="sf.type" @change="sync" class="h-[24px] px-1 border border-input rounded-[var(--radius)] bg-card text-[11px]">
                  <option v-for="st in SUBFIELD_TYPES" :key="st.type" :value="st.type">{{ st.label }}</option>
                </select>
                <button class="text-danger border-0 bg-transparent cursor-pointer" @click="removeSubField(si)"><Trash2 :size="11" /></button>
              </div>
              <div v-if="sf.type === 'select'" class="flex flex-wrap items-center gap-1">
                <button class="text-[10px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="addSubOption(sf)">+ 选项</button>
                <span v-for="(so, soi) in sf.options ?? []" :key="soi" class="text-[10px] text-muted-foreground">
                  {{ so.label }}<button class="text-danger border-0 bg-transparent cursor-pointer" @click="sf.options?.splice(soi, 1); sync()">×</button>
                </span>
              </div>
            </div>
          </div>
        </template>
        <div v-else class="text-[11px] text-muted-foreground py-6 text-center">选中左侧字段进行配置</div>
      </div>
    </div>

    <!-- 扩展视图（L2 远程视图） -->
    <div class="mt-3 pt-3 border-t border-border">
      <div class="flex items-center justify-between mb-1">
        <span class="text-[11px] text-muted-foreground">扩展视图（只读嵌入子系统页面）</span>
        <button class="flex items-center gap-0.5 text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="addExtension">
          <Plus :size="10" />添加
        </button>
      </div>
      <div v-if="!extensions.length" class="text-[11px] text-muted-foreground py-1">暂无扩展视图（复杂表单可嵌入子系统只读页）</div>
      <div v-for="(ext, i) in extensions" :key="i" class="flex items-center gap-1.5 mb-1">
        <input v-model="ext.title" @input="sync" placeholder="标题"
          class="w-[92px] h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px]" />
        <input v-model="ext.url" @input="sync" placeholder="https://... 子系统只读页"
          class="flex-1 h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px]" />
        <input v-model.number="ext.height" @input="sync" type="number" placeholder="高"
          class="w-[56px] h-[26px] px-1.5 border border-input rounded-[var(--radius)] bg-card text-[12px]" />
        <button class="text-danger border-0 bg-transparent cursor-pointer" @click="removeExtension(i)"><Trash2 :size="11" /></button>
      </div>
    </div>

    <!-- 高级 JSON -->
    <div v-if="showJson" class="mt-3 pt-3 border-t border-border">
      <div class="flex items-center justify-between mb-1">
        <span class="text-[11px] text-muted-foreground">Form Schema JSON</span>
        <button class="text-[11px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="applyJson">应用 JSON</button>
      </div>
      <textarea v-model="jsonText" rows="8"
        class="w-full px-2 py-1.5 border border-input rounded-[var(--radius)] bg-card text-[11px] font-mono resize-none focus:outline-none focus:ring-1 focus:ring-ring" />
    </div>
  </div>
</template>
