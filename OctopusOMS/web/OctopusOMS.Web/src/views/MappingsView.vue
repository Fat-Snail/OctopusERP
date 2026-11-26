<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Plus } from '@lucide/vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Modal from '@/components/ui/Modal.vue'
import { getMappings, getChannels, createMapping, type SkuMapping, type Channel } from '@/api'

const rows = ref<SkuMapping[]>([])
const channels = ref<Channel[]>([])
const loading = ref(false)
const open = ref(false)
const error = ref('')
const form = ref({ channelId: 0, channelSkuCode: '', plmProductId: 0, note: '' })

function chName(id: number) { return channels.value.find(c => c.channelId === id)?.channelName ?? `#${id}` }

async function load() {
  loading.value = true
  try { rows.value = await getMappings() } catch { rows.value = [] } finally { loading.value = false }
}

async function submit() {
  error.value = ''
  if (!form.value.channelId || !form.value.channelSkuCode || !form.value.plmProductId) { error.value = '请填写渠道、SKU 编码和 PLM 产品 ID'; return }
  try {
    await createMapping(form.value)
    open.value = false
    form.value = { channelId: channels.value[0]?.channelId ?? 0, channelSkuCode: '', plmProductId: 0, note: '' }
    await load()
  } catch (e) { error.value = (e as Error).message }
}

onMounted(async () => {
  try { channels.value = await getChannels(); form.value.channelId = channels.value[0]?.channelId ?? 0 } catch { /* ignore */ }
  await load()
})
</script>

<template>
  <div>
    <PageHeader title="SKU 映射" sub="渠道 SKU 编码与 PLM 商品 ID 的对应关系">
      <template #actions>
        <button class="flex items-center gap-1.5 h-8 px-3 bg-primary text-primary-foreground rounded-[var(--radius)] text-[12px] font-medium border-0 cursor-pointer" @click="open = true"><Plus :size="14" /> 新增映射</button>
      </template>
    </PageHeader>
    <div class="p-5">
      <div class="bg-card border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-muted text-muted-foreground"><tr>
            <th class="text-left px-4 py-2.5 font-medium">渠道</th><th class="text-left px-4 py-2.5 font-medium">渠道 SKU 编码</th>
            <th class="text-left px-4 py-2.5 font-medium">PLM 产品 ID</th><th class="text-left px-4 py-2.5 font-medium">备注</th>
            <th class="text-left px-4 py-2.5 font-medium">创建时间</th>
          </tr></thead>
          <tbody>
            <tr v-if="loading"><td colspan="5" class="text-center py-8 text-muted-foreground">加载中...</td></tr>
            <tr v-else-if="rows.length === 0"><td colspan="5" class="text-center py-8 text-muted-foreground">暂无映射</td></tr>
            <tr v-for="r in rows" :key="r.mappingId" class="border-t border-border hover:bg-[var(--subtle)]">
              <td class="px-4 py-2.5">{{ chName(r.channelId) }}</td>
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.channelSkuCode }}</td>
              <td class="px-4 py-2.5 font-mono-tnum">{{ r.plmProductId }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.note || '—' }}</td>
              <td class="px-4 py-2.5 text-muted-foreground">{{ r.createdAt?.slice(0, 10) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <Modal :open="open" title="新增 SKU 映射" @close="open = false">
      <div class="space-y-3">
        <label class="block text-[12px]">渠道<select v-model.number="form.channelId" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card"><option :value="0">选择渠道</option><option v-for="c in channels" :key="c.channelId" :value="c.channelId">{{ c.channelName }}</option></select></label>
        <label class="block text-[12px]">渠道 SKU 编码<input v-model="form.channelSkuCode" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">PLM 产品 ID<input v-model.number="form.plmProductId" type="number" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <label class="block text-[12px]">备注<input v-model="form.note" class="mt-1 w-full h-8 px-2 border border-border rounded bg-card" /></label>
        <p v-if="error" class="text-[12px] text-danger">{{ error }}</p>
      </div>
      <template #footer>
        <button class="px-3 py-1.5 border border-border rounded text-[12px] cursor-pointer bg-card" @click="open = false">取消</button>
        <button class="px-3 py-1.5 bg-primary text-primary-foreground rounded text-[12px] cursor-pointer border-0" @click="submit">保存</button>
      </template>
    </Modal>
  </div>
</template>
