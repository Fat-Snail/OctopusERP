<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Plus, Settings2, Workflow, LayoutTemplate, X } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { getFlowDefinitions } from '@/api/flow'
import type { FlowDefinition } from '@/api/flow/types'
import { FLOW_TEMPLATES, FLOW_TEMPLATE_DRAFT_KEY, type FlowTemplateDraft } from '../flowTemplates'

const router = useRouter()
const loading = ref(false)
const rows = ref<FlowDefinition[]>([])
const showTemplates = ref(false)

async function loadData() {
  loading.value = true
  try { rows.value = await getFlowDefinitions() } finally { loading.value = false }
}

onMounted(loadData)

function useTemplate(t: typeof FLOW_TEMPLATES[number]) {
  const draft: FlowTemplateDraft = {
    code: t.code,
    name: t.name,
    category: t.category,
    formSchema: JSON.stringify(t.formSchema),
    graphJson: JSON.stringify(t.graph),
  }
  sessionStorage.setItem(FLOW_TEMPLATE_DRAFT_KEY, JSON.stringify(draft))
  showTemplates.value = false
  router.push('/flow/designer/new')
}
</script>

<template>
  <div>
    <PageHeader title="流程定义" sub="审批中心 · 流程设计与版本">
      <template #actions>
        <button
          class="flex items-center gap-1 px-3 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card"
          @click="showTemplates = true"
        >
          <LayoutTemplate :size="13" />从模板新建
        </button>
        <button
          class="flex items-center gap-1 px-3 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0"
          @click="router.push('/flow/designer/new')"
        >
          <Plus :size="13" />新建流程
        </button>
      </template>
    </PageHeader>

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!rows.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无流程定义，点击「新建流程」开始设计</div>
      <div v-else class="grid grid-cols-2 gap-4">
        <div v-for="d in rows" :key="d.id" class="bg-card border border-border rounded-[var(--radius-lg)] p-4">
          <div class="flex items-start justify-between">
            <div class="flex items-center gap-3">
              <div class="w-9 h-9 bg-primary-soft rounded-lg flex items-center justify-center">
                <Workflow :size="18" class="text-primary-soft-fg" />
              </div>
              <div>
                <div class="text-[13px] font-semibold">{{ d.name }}</div>
                <div class="text-[11px] text-muted-foreground font-mono">{{ d.code }} · v{{ d.version }}</div>
              </div>
            </div>
            <Badge v-if="d.category" variant="info">{{ d.category }}</Badge>
          </div>
          <div class="mt-3 flex items-center justify-between">
            <div class="text-[11px] text-muted-foreground">
              业务键：<span class="font-mono">{{ d.businessKey || '—' }}</span>
            </div>
            <button
              class="flex items-center gap-1 text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent"
              @click="router.push(`/flow/designer/${d.id}`)"
            >
              <Settings2 :size="11" />设计
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 流程模板库 -->
    <div v-if="showTemplates" class="fixed inset-0 z-50 flex items-center justify-center bg-black/40" @click.self="showTemplates = false">
      <div class="w-[720px] max-w-[92vw] max-h-[86vh] overflow-y-auto bg-card border border-border rounded-[var(--radius-lg)] shadow-xl">
        <div class="flex items-center justify-between px-5 py-4 border-b border-border sticky top-0 bg-card">
          <div>
            <div class="text-[14px] font-semibold">从模板新建流程</div>
            <div class="text-[11px] text-muted-foreground mt-0.5">选择模板后载入设计器，可继续修改再保存</div>
          </div>
          <button class="text-muted-foreground hover:text-foreground border-0 bg-transparent cursor-pointer" @click="showTemplates = false"><X :size="16" /></button>
        </div>
        <div class="grid grid-cols-2 gap-3 p-5">
          <button
            v-for="t in FLOW_TEMPLATES" :key="t.id"
            class="text-left p-3.5 border border-border rounded-[var(--radius)] bg-card hover:border-primary hover:bg-primary-soft/30 cursor-pointer transition-colors"
            @click="useTemplate(t)"
          >
            <div class="flex items-center gap-2 mb-1.5">
              <Badge variant="info">{{ t.category }}</Badge>
              <span class="text-[13px] font-semibold">{{ t.name }}</span>
            </div>
            <p class="text-[11px] text-muted-foreground leading-relaxed">{{ t.description }}</p>
            <div class="mt-2 text-[10px] text-muted-foreground font-mono">code: {{ t.code }}</div>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
