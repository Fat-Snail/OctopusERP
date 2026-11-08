<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Plus, Settings2, Workflow } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { getFlowDefinitions } from '@/api/flow'
import type { FlowDefinition } from '@/api/flow/types'

const router = useRouter()
const loading = ref(false)
const rows = ref<FlowDefinition[]>([])

async function loadData() {
  loading.value = true
  try { rows.value = await getFlowDefinitions() } finally { loading.value = false }
}

onMounted(loadData)
</script>

<template>
  <div>
    <PageHeader title="流程定义" sub="审批中心 · 流程设计与版本">
      <template #actions>
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
  </div>
</template>
