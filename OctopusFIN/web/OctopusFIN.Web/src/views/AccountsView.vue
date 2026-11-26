<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus, Search } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import StatusBadge from '@/components/ui/StatusBadge.vue'
import Modal from '@/components/ui/Modal.vue'
import { getAccounts, createAccount, type Account } from '@/api'

const rows = ref<Account[]>([])
const loading = ref(false)
const keyword = ref('')
const typeFilter = ref('')
const open = ref(false)
const error = ref('')
const form = ref({ accountCode: '', accountName: '', accountType: 'asset', direction: 'debit', remark: '' })

const statusLabels: Record<string, string> = { active: '启用', inactive: '停用' }
const typeLabels: Record<string, string> = { asset: '资产', liability: '负债', equity: '权益', income: '收入', expense: '费用' }

async function load() {
  loading.value = true
  try { rows.value = await getAccounts({ keyword: keyword.value || undefined, type: typeFilter.value || undefined }) }
  catch { rows.value = [] } finally { loading.value = false }
}

async function submit() {
  error.value = ''
  if (!form.value.accountCode || !form.value.accountName) { error.value = '请填写科目编码和名称'; return }
  try {
    await createAccount(form.value)
    open.value = false
    form.value = { accountCode: '', accountName: '', accountType: 'asset', direction: 'debit', remark: '' }
    await load()
  } catch (e) { error.value = (e as Error).message }
}

onMounted(load)
</script>

<template>
  <div>
    <PageHeader title="会计科目" sub="科目表维护（资产/负债/权益/收入/费用）">
      <template #actions>
        <button class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium border-0 cursor-pointer" @click="open = true"><Plus :size="14" /> 新增科目</button>
      </template>
    </PageHeader>
    <div class="p-5">
      <div class="flex items-center gap-3 mb-4">
        <input v-model="keyword" placeholder="编码或名称" class="h-8 px-3 border border-border rounded bg-card text-[12px] w-56" @keyup.enter="load" />
        <select v-model="typeFilter" class="h-8 px-2 border border-border rounded bg-card text-[12px]" @change="load">
          <option value="">全部类型</option>
          <option v-for="(v, k) in typeLabels" :key="k" :value="k">{{ v }}</option>
        </select>
        <button class="flex items-center gap-1 h-8 px-3 border border-border rounded bg-card text-[12px] cursor-pointer" @click="load"><Search :size="13" /> 查询</button>
        <span class="ml-auto text-[11px] text-muted-foreground">共 {{ rows.length }} 个科目</span>
      </div>
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">编码</th><th class="text-left px-4 py-2.5 font-medium">名称</th>
            <th class="text-left px-4 py-2.5 font-medium">类型</th><th class="text-left px-4 py-2.5 font-medium">余额方向</th>
            <th class="text-center px-4 py-2.5 font-medium">级次</th><th class="text-left px-4 py-2.5 font-medium">状态</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="6" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="6" class="text-center py-8 text-muted-foreground">暂无科目</td></tr>
            <tr v-for="r in rows" :key="r.accountId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.accountCode }}</td>
              <td class="px-4 py-2.5 font-medium" :style="{ paddingLeft: `${16 + (r.level - 1) * 18}px` }">{{ r.accountName }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ typeLabels[r.accountType] ?? r.accountType }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.direction === 'debit' ? '借' : '贷' }}</td>
              <td class="px-4 py-2.5 text-center text-muted-foreground">{{ r.level }}</td>
              <td class="px-4 py-2.5"><StatusBadge :value="r.status" :labels="statusLabels" /></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="open" title="新增科目" @close="open = false">
      <div class="space-y-3">
        <label class="block text-[12px]">科目编码<input v-model="form.accountCode" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">科目名称<input v-model="form.accountName" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <div class="grid grid-cols-2 gap-3">
          <label class="block text-[12px]">科目类型<select v-model="form.accountType" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option v-for="(v, k) in typeLabels" :key="k" :value="k">{{ v }}</option></select></label>
          <label class="block text-[12px]">余额方向<select v-model="form.direction" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option value="debit">借</option><option value="credit">贷</option></select></label>
        </div>
        <label class="block text-[12px]">备注<input v-model="form.remark" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <p v-if="error" class="text-[12px] text-danger">{{ error }}</p>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="open = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submit">保存</button>
      </template>
    </Modal>
  </div>
</template>
