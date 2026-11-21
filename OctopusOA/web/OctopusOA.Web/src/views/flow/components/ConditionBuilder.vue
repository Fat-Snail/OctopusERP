<script setup lang="ts">
/**
 * 条件分支可视化构建器。
 * 把「字段 + 运算符 + 值」的多行条件拼成后端 ExpressionEvaluator 可求值的表达式。
 * 字段来自表单 schema；运算符与语法严格对齐后端（== != > >= < <=，逻辑 && ||）。
 */
import { ref, computed, watch } from 'vue'
import { Plus, Trash2, Braces } from '@lucide/vue'
import type { FormField } from '@/api/approval/types'
import { validateExpression } from '@/lib/formExpr'

const props = defineProps<{
  modelValue: string
  fields: FormField[]
}>()
const emit = defineEmits<{ (e: 'update:modelValue', value: string): void }>()

interface CondRow { field: string; op: string; value: string; logic: '&&' | '||' }

const OPS = [
  { v: '==', l: '等于' }, { v: '!=', l: '不等于' },
  { v: '>', l: '大于' }, { v: '>=', l: '大于等于' },
  { v: '<', l: '小于' }, { v: '<=', l: '小于等于' },
]

const fieldOptions = computed(() => props.fields.filter(f => f.key && f.type !== 'subform' && f.type !== 'file'))

function defaultRow(): CondRow {
  const first = fieldOptions.value[0]
  return { field: first?.key ?? '', op: '==', value: '', logic: '&&' }
}
const rows = ref<CondRow[]>([defaultRow()])
const showRaw = ref(false)
const rawText = ref(props.modelValue || '')

// 把已有表达式解析回行（尽力而为：仅解析简单的 "field op literal && ..." 形式）
function parseExpression(expr: string): CondRow[] {
  if (!expr?.trim()) return [defaultRow()]
  const parts = expr.split(/\s*(&&|\|\|)\s*/)
  const parsed: CondRow[] = []
  for (let i = 0; i < parts.length; i += 2) {
    const clause = parts[i].trim()
    const m = clause.match(/^([A-Za-z_][A-Za-z0-9_]*)\s*(==|!=|>=|<=|>|<)\s*(.+)$/)
    if (m) {
      let val = m[3].trim()
      if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) val = val.slice(1, -1)
      parsed.push({ field: m[1], op: m[2], value: val, logic: (parts[i - 1] as CondRow['logic']) || '&&' })
    } else {
      parsed.push({ ...defaultRow(), value: clause })
    }
  }
  return parsed.length ? parsed : [defaultRow()]
}

watch(
  () => props.modelValue,
  (v) => {
    // 仅当外部值与当前生成值不同时回填，避免编辑时被打断
    if (v !== buildExpr()) {
      rows.value = parseExpression(v)
      rawText.value = v || ''
    }
  },
  { immediate: true }
)

function fieldType(key: string): FormField['type'] | undefined {
  return fieldOptions.value.find(f => f.key === key)?.type
}

function formattedValue(row: CondRow): string {
  const raw = row.value.trim()
  const type = fieldType(row.field)
  if (raw === 'true' || raw === 'false' || raw === 'null') return raw
  if (type === 'number' && /^-?\d+(\.\d+)?$/.test(raw)) return raw
  if (/^-?\d+(\.\d+)?$/.test(raw) && row.op !== '==' && row.op !== '!=') return raw // 比较运算的纯数字
  return JSON.stringify(raw) // 字符串值加引号
}

function buildExpr(): string {
  return rows.value.map((r, i) => {
    if (!r.field) return ''
    const clause = `${r.field} ${r.op} ${formattedValue(r)}`
    return i === 0 ? clause : `${r.logic} ${clause}`
  }).filter(Boolean).join(' ')
}

const preview = computed(() => buildExpr())
const error = computed(() => validateExpression(preview.value))

function commit() {
  rawText.value = preview.value
  emit('update:modelValue', preview.value)
}

function addRow() { rows.value.push({ ...defaultRow(), logic: rows.value.length ? '&&' : '&&' }); commit() }
function removeRow(i: number) { if (rows.value.length <= 1) return; rows.value.splice(i, 1); commit() }
function onRowChange() { commit() }

function applyRaw() {
  emit('update:modelValue', rawText.value)
  rows.value = parseExpression(rawText.value)
}
</script>

<template>
  <div class="border border-border rounded-[var(--radius)] p-2.5">
    <div class="flex items-center justify-between mb-2">
      <span class="text-[11px] text-muted-foreground">可视化条件构建</span>
      <button class="text-[10px] text-primary hover:underline border-0 bg-transparent cursor-pointer flex items-center gap-0.5" @click="showRaw = !showRaw; showRaw && (rawText = preview)">
        <Braces :size="10" />{{ showRaw ? '可视化' : '高级文本' }}
      </button>
    </div>

    <template v-if="!showRaw">
      <div v-if="!fieldOptions.length" class="text-[10px] text-warning py-2">
        当前流程还没有可用的表单字段，请先在「表单设计」中添加。
      </div>
      <template v-else>
        <div v-for="(row, i) in rows" :key="i" class="mb-1.5">
          <div v-if="i > 0" class="flex items-center gap-1 mb-1">
            <select v-model="row.logic" @change="onRowChange" class="h-[24px] px-1 border border-input rounded-[var(--radius)] bg-card text-[11px]">
              <option value="&&">并且 (&&)</option>
              <option value="||">或者 (||)</option>
            </select>
            <span class="text-[10px] text-muted-foreground">连接上一条件</span>
          </div>
          <div class="flex items-center gap-1">
            <select v-model="row.field" @change="onRowChange" class="flex-1 h-[26px] px-1 border border-input rounded-[var(--radius)] bg-card text-[11px]">
              <option v-for="f in fieldOptions" :key="f.key" :value="f.key">{{ f.label }} ({{ f.key }})</option>
            </select>
            <select v-model="row.op" @change="onRowChange" class="w-[86px] h-[26px] px-1 border border-input rounded-[var(--radius)] bg-card text-[11px]">
              <option v-for="o in OPS" :key="o.v" :value="o.v">{{ o.l }}</option>
            </select>
            <input v-model="row.value" @input="onRowChange" placeholder="值"
              class="w-[100px] h-[26px] px-2 border border-input rounded-[var(--radius)] bg-card text-[11px]" />
            <button class="text-danger border-0 bg-transparent cursor-pointer" @click="removeRow(i)"><Trash2 :size="11" /></button>
          </div>
        </div>
        <button class="flex items-center gap-0.5 text-[10px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="addRow">
          <Plus :size="10" />添加条件
        </button>
      </template>

      <div class="mt-2 pt-2 border-t border-border">
        <div class="text-[10px] text-muted-foreground mb-0.5">生成的表达式</div>
        <code class="block text-[11px] font-mono px-2 py-1 rounded-[var(--radius)] bg-subtle break-all">{{ preview || '（无条件，始终走此分支）' }}</code>
        <p v-if="error" class="text-[10px] text-danger mt-1">{{ error }}</p>
      </div>
    </template>

    <template v-else>
      <input v-model="rawText" placeholder="如 amount >= 100000 && type == 'buy'"
        class="w-full h-[28px] px-2 border border-input rounded-[var(--radius)] bg-card text-[12px] font-mono mb-1.5" />
      <div class="flex items-center justify-between">
        <p class="text-[10px] text-muted-foreground">支持 == != &gt; &gt;= &lt; &lt;=、&amp;&amp; || !、括号</p>
        <button class="text-[10px] text-primary hover:underline border-0 bg-transparent cursor-pointer" @click="applyRaw">应用</button>
      </div>
      <p v-if="validateExpression(rawText)" class="text-[10px] text-danger mt-1">{{ validateExpression(rawText) }}</p>
    </template>
  </div>
</template>
