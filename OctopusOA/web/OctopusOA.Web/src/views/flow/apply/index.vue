<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ChevronLeft, Workflow } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { toast } from '@/components/ui/toast'
import DynamicForm from '@/views/approval/components/DynamicForm.vue'
import { getFlowDefinitions, submitFlow } from '@/api/flow'
import type { FlowDefinition } from '@/api/flow/types'
import type { FormSchema } from '@/api/approval/types'

const router = useRouter()
const definitions = ref<FlowDefinition[]>([])
const selected = ref<FlowDefinition | null>(null)
const title = ref('')
const formData = ref<Record<string, unknown>>({})
const submitting = ref(false)
const dynamicFormRef = ref<InstanceType<typeof DynamicForm>>()

const formSchema = computed<FormSchema>(() => {
  if (!selected.value) return { fields: [] }
  try { return JSON.parse(selected.value.formSchema) as FormSchema } catch { return { fields: [] } }
})
const formFields = computed(() => formSchema.value.fields ?? [])
const formExtensions = computed(() => formSchema.value.extensions ?? [])

function selectDefinition(d: FlowDefinition) {
  selected.value = d
  title.value = ''
  formData.value = {}
}

async function handleSubmit() {
  if (!title.value.trim()) { toast('请输入标题', 'warning'); return }
  const valid = await dynamicFormRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    const res = await submitFlow({
      definitionCode: selected.value!.code,
      title: title.value,
      formData: JSON.stringify(formData.value),
    })
    toast('发起成功', 'success')
    router.push(`/flow/detail/${res.instanceId}`)
  } catch (e: unknown) {
    toast((e as Error).message, 'error')
  } finally {
    submitting.value = false
  }
}

onMounted(async () => {
  try { definitions.value = await getFlowDefinitions() } catch { /* empty */ }
})
</script>

<template>
  <div>
    <PageHeader
      :title="selected ? selected.name : '发起流程'"
      :sub="selected ? '填写表单并提交' : '选择要发起的审批流程'"
    />
    <div class="p-5">
      <div v-if="!selected">
        <div v-if="!definitions.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无可发起的流程</div>
        <div v-else class="grid grid-cols-3 gap-4">
          <button
            v-for="d in definitions"
            :key="d.id"
            class="bg-card border border-border rounded-[var(--radius-lg)] p-5 text-left hover:border-primary hover:shadow-sm transition-all cursor-pointer"
            @click="selectDefinition(d)"
          >
            <div class="flex items-center gap-3 mb-2">
              <div class="w-10 h-10 bg-primary-soft rounded-lg flex items-center justify-center">
                <Workflow :size="20" class="text-primary-soft-fg" />
              </div>
              <span class="text-[14px] font-semibold">{{ d.name }}</span>
            </div>
            <p class="text-[12px] text-muted-foreground font-mono">{{ d.code }} · v{{ d.version }}</p>
          </button>
        </div>
      </div>

      <div v-else class="max-w-[640px]">
        <button
          class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground mb-4 cursor-pointer border-0 bg-transparent"
          @click="selected = null"
        >
          <ChevronLeft :size="14" />返回选择流程
        </button>

        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5 space-y-4">
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">
              <span class="text-danger">*</span> 标题
            </label>
            <input
              v-model="title"
              type="text"
              placeholder="请输入标题"
              class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <DynamicForm ref="dynamicFormRef" :fields="formFields" :extensions="formExtensions" v-model="formData" />
          <div class="flex justify-center gap-3 pt-2">
            <button class="px-6 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer" @click="selected = null">取消</button>
            <button
              :disabled="submitting"
              class="px-6 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50"
              @click="handleSubmit"
            >
              {{ submitting ? '提交中...' : '提交' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
