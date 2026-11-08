<script setup lang="ts">
import { ref, computed } from 'vue'
import type { FormExtension, FormField } from '@/api/approval/types'
import { getFormComponent } from '@/lib/formComponentRegistry'
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
  return field.options?.find(o => o.value === str)?.label ?? str
}

function resolveFieldComponent(field: FormField) {
  return field.componentId ? getFormComponent(field.componentId) : undefined
}

async function validate(): Promise<boolean> {
  errors.value = {}
  let valid = true
  for (const f of props.fields) {
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

    <div v-for="field in fields" :key="field.key" class="flex gap-3">
      <label class="w-24 shrink-0 text-[12px] text-right text-foreground pt-1.5">
        <span v-if="field.required && !readonly" class="text-danger mr-0.5">*</span>
        {{ field.label }}
      </label>
      <div class="flex-1">

        <!-- ── Readonly display ── -->
        <template v-if="readonly">
          <!-- L3 自定义组件（只读） -->
          <component v-if="resolveFieldComponent(field)" :is="resolveFieldComponent(field)"
            :model-value="formData[field.key]" :readonly="true" />
          <!-- Select: look up label directly from options -->
          <span v-else-if="field.type === 'select'" class="flex items-center h-[var(--row-h)] text-[12px] text-foreground">
            {{ selectLabel(field, formData[field.key]) }}
          </span>
          <!-- All other types: plain text -->
          <span v-else class="flex items-center min-h-[var(--row-h)] text-[12px] text-foreground whitespace-pre-wrap">
            {{ formData[field.key] ?? '—' }}
          </span>
        </template>

        <!-- ── Editable controls ── -->
        <template v-else>
          <!-- L3 自定义组件优先 -->
          <component v-if="resolveFieldComponent(field)" :is="resolveFieldComponent(field)"
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
              <SelectItem v-for="opt in field.options" :key="opt.value" :value="opt.value">{{ opt.label }}</SelectItem>
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
            @input="updateField(field.key, ($event.target as HTMLTextAreaElement).value)"
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
