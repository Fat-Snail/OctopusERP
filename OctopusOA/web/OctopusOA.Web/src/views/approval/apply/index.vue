<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ChevronLeft, FileText } from 'lucide-vue-next'
import PageHeader from '@/components/app/PageHeader.vue'
import { toast } from '@/components/ui/toast'
import { getAvailableTemplates, submitApproval } from '@/api/approval'
import type { AvailableTemplate } from '@/api/approval'
import type { FormSchema } from '@/api/approval/types'
import DynamicForm from '../components/DynamicForm.vue'

const router = useRouter()
const templates = ref<AvailableTemplate[]>([])
const selectedTemplate = ref<AvailableTemplate | null>(null)
const title = ref('')
const formData = ref<Record<string, unknown>>({})
const submitting = ref(false)
const dynamicFormRef = ref<InstanceType<typeof DynamicForm>>()

const formFields = computed(() => {
  if (!selectedTemplate.value) return []
  try {
    return (JSON.parse(selectedTemplate.value.formSchema) as FormSchema).fields || []
  } catch { return [] }
})

function selectTemplate(t: AvailableTemplate) {
  selectedTemplate.value = t; title.value = ''; formData.value = {}
}

async function handleSubmit() {
  if (!title.value.trim()) { toast('请输入申请标题', 'warning'); return }
  const valid = await dynamicFormRef.value?.validate()
  if (!valid) return
  submitting.value = true
  try {
    await submitApproval({ templateId: selectedTemplate.value!.templateId, title: title.value, formData: JSON.stringify(formData.value) })
    toast('提交成功', 'success')
    router.push('/approval/mine')
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

onMounted(async () => {
  try { templates.value = await getAvailableTemplates() } catch { /* empty */ }
})
</script>

<template>
  <div>
    <PageHeader
      :title="selectedTemplate ? selectedTemplate.templateName : '发起申请'"
      :sub="selectedTemplate ? '填写申请信息并提交' : '选择要发起的审批类型'"
    />

    <div class="p-5">
      <div v-if="!selectedTemplate">
        <div v-if="!templates.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无可用模板</div>
        <div v-else class="grid grid-cols-3 gap-4">
          <button
            v-for="t in templates"
            :key="t.templateId"
            class="bg-card border border-border rounded-[var(--radius-lg)] p-5 text-left hover:border-primary hover:shadow-sm transition-all cursor-pointer"
            @click="selectTemplate(t)"
          >
            <div class="flex items-center gap-3 mb-3">
              <div class="w-10 h-10 bg-primary-soft rounded-lg flex items-center justify-center">
                <FileText :size="20" class="text-primary-soft-fg" />
              </div>
              <span class="text-[14px] font-semibold">{{ t.templateName }}</span>
            </div>
            <p class="text-[12px] text-muted-foreground">{{ t.description || '暂无描述' }}</p>
          </button>
        </div>
      </div>

      <div v-else class="max-w-[640px]">
        <button
          class="flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground mb-4 cursor-pointer border-0 bg-transparent"
          @click="selectedTemplate = null"
        >
          <ChevronLeft :size="14" />返回选择模板
        </button>

        <div class="bg-card border border-border rounded-[var(--radius-lg)] p-5 space-y-4">
          <div>
            <label class="block text-[12px] font-medium text-foreground mb-1.5">
              <span class="text-danger">*</span> 申请标题
            </label>
            <input
              v-model="title"
              type="text"
              placeholder="请输入申请标题"
              class="w-full h-[var(--row-h)] px-3 border border-input rounded-[var(--radius)] bg-card text-[12px] focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <DynamicForm ref="dynamicFormRef" :fields="formFields" v-model="formData" />
          <div class="flex justify-center gap-3 pt-2">
            <button class="px-6 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer" @click="selectedTemplate = null">取消</button>
            <button
              :disabled="submitting"
              class="px-6 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50"
              @click="handleSubmit"
            >
              {{ submitting ? '提交中...' : '提交申请' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
