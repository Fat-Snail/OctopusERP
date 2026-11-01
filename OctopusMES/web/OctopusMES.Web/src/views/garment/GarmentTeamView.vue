<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Plus, X, Power, PowerOff, Pencil } from '@lucide/vue'
import {
  listGarmentTeams,
  createGarmentTeam,
  updateGarmentTeam,
  setGarmentTeamStatus,
  getCandidateLeaders,
} from '@/api/garment'
import type { GarmentTeam, CandidateLeader, UpsertGarmentTeamRequest } from '@/api/garment/types'
import PageHeader from '@/components/app/PageHeader.vue'

const teams = ref<GarmentTeam[]>([])
const loading = ref(false)
const includeInactive = ref(false)
const keyword = ref('')

const editorOpen = ref(false)
const editorMode = ref<'create' | 'edit'>('create')
const editing = ref<UpsertGarmentTeamRequest>(emptyForm())
const candidates = ref<CandidateLeader[]>([])
const saveError = ref<string | null>(null)

function emptyForm(): UpsertGarmentTeamRequest {
  return {
    teamCode: '',
    teamName: '',
    groupLabel: '',
    leader: '',
    leaderUmcUserId: null,
    umcDeptId: null,
    isOutsource: false,
    outsourcePartnerName: null,
    headcount: 12,
    sortOrder: 0,
    remark: null,
  }
}

async function loadTeams() {
  loading.value = true
  try {
    const r = await listGarmentTeams({
      keyword: keyword.value || undefined,
      includeInactive: includeInactive.value,
    })
    teams.value = r.rows
  } finally {
    loading.value = false
  }
}

async function loadCandidates() {
  try {
    const r = await getCandidateLeaders()
    candidates.value = r.rows
  } catch {
    candidates.value = []
  }
}

function openCreate() {
  editorMode.value = 'create'
  editing.value = emptyForm()
  saveError.value = null
  editorOpen.value = true
  loadCandidates()
}

function openEdit(t: GarmentTeam) {
  editorMode.value = 'edit'
  editing.value = {
    teamId: t.teamId,
    teamCode: t.teamCode,
    teamName: t.teamName,
    groupLabel: t.groupLabel,
    leader: t.leader,
    leaderUmcUserId: t.leaderUmcUserId,
    umcDeptId: t.umcDeptId,
    isOutsource: t.isOutsource,
    outsourcePartnerName: t.outsourcePartnerName,
    headcount: t.headcount,
    status: t.status,
    sortOrder: t.sortOrder,
  }
  saveError.value = null
  editorOpen.value = true
  loadCandidates()
}

async function save() {
  saveError.value = null
  try {
    if (editorMode.value === 'create') await createGarmentTeam(editing.value)
    else await updateGarmentTeam(editing.value)
    editorOpen.value = false
    await loadTeams()
  } catch (e: unknown) {
    saveError.value = (e as Error).message || '保存失败'
  }
}

async function toggleStatus(t: GarmentTeam) {
  const next = t.status === 'active' ? 'inactive' : 'active'
  if (next === 'inactive' && !confirm(`确认停用 ${t.teamName}？停用后日历不再渲染该组。`)) return
  await setGarmentTeamStatus(t.teamId, next)
  await loadTeams()
}

function onLeaderSelect(umcUserId: number) {
  const c = candidates.value.find(x => x.umcUserId === umcUserId)
  if (c) {
    editing.value.leaderUmcUserId = c.umcUserId
    editing.value.leader = c.nickName || c.userName
  }
}

const sortedTeams = computed(() => [...teams.value].sort((a, b) => a.sortOrder - b.sortOrder))

onMounted(() => loadTeams())
</script>

<template>
  <PageHeader title="车间组管理" :sub="`共 ${teams.length} 个组`">
    <template #actions>
      <button
        class="inline-flex items-center gap-1.5 px-3 h-8 rounded-[var(--radius)] text-[12px] bg-primary text-primary-foreground hover:opacity-90 transition-opacity cursor-pointer"
        @click="openCreate"
      >
        <Plus class="size-3.5" /> 新增组
      </button>
    </template>
  </PageHeader>

  <div class="p-5 space-y-3">
    <!-- 筛选栏 -->
    <div class="bg-card border border-border rounded-[var(--radius)] px-3 py-2.5 flex items-center gap-2">
      <input
        v-model="keyword"
        @keyup.enter="loadTeams"
        placeholder="搜索组编码 / 组长 / 外协方"
        class="h-8 px-2 rounded-[var(--radius)] border border-border bg-card text-[12px] w-[240px]"
      />
      <label class="flex items-center gap-1.5 text-[12px] text-muted-foreground cursor-pointer">
        <input type="checkbox" v-model="includeInactive" @change="loadTeams" class="cursor-pointer" />
        含停用
      </label>
      <button
        class="px-3 h-8 rounded-[var(--radius)] border border-border bg-card text-[12px] hover:bg-muted cursor-pointer"
        @click="loadTeams"
      >搜索</button>
    </div>

    <!-- 列表 -->
    <div class="bg-card border border-border rounded-[var(--radius)] overflow-hidden">
      <div v-if="loading" class="p-8 text-center text-muted-foreground text-[13px]">加载中…</div>
      <div v-else-if="teams.length === 0" class="p-8 text-center text-muted-foreground text-[13px]">无车间组</div>
      <table v-else class="w-full text-[12.5px]">
        <thead class="bg-muted/50">
          <tr>
            <th class="px-3 py-2 text-left font-medium">编码</th>
            <th class="px-3 py-2 text-left font-medium">名称</th>
            <th class="px-3 py-2 text-left font-medium">类型</th>
            <th class="px-3 py-2 text-left font-medium">组长 / 合作方</th>
            <th class="px-3 py-2 text-right font-medium">人数</th>
            <th class="px-3 py-2 text-left font-medium">状态</th>
            <th class="px-3 py-2 text-left font-medium">UMC</th>
            <th class="px-3 py-2 text-right font-medium">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="t in sortedTeams" :key="t.teamId" class="border-t border-border hover:bg-muted/30">
            <td class="px-3 py-2 font-mono text-foreground">{{ t.teamCode }}</td>
            <td class="px-3 py-2">{{ t.teamName }}</td>
            <td class="px-3 py-2">
              <span v-if="t.isOutsource" class="text-[10.5px] px-1.5 py-0.5 rounded bg-[color-mix(in_oklch,var(--info)_14%,transparent)] text-[var(--info)]">外协</span>
              <span v-else class="text-[10.5px] px-1.5 py-0.5 rounded bg-primary-soft text-[var(--primary-soft-fg)]">自有</span>
            </td>
            <td class="px-3 py-2">{{ t.isOutsource ? t.outsourcePartnerName : t.leader }}</td>
            <td class="px-3 py-2 text-right">{{ t.headcount ?? '—' }}</td>
            <td class="px-3 py-2">
              <span :class="[
                'inline-block px-1.5 py-0.5 rounded text-[10.5px]',
                t.status === 'active'
                  ? 'bg-[color-mix(in_oklch,var(--success)_14%,transparent)] text-[var(--success)]'
                  : 'bg-muted text-muted-foreground'
              ]">{{ t.status }}</span>
            </td>
            <td class="px-3 py-2 text-[10.5px] text-muted-foreground">
              <span v-if="t.leaderUmcUserId">U#{{ t.leaderUmcUserId }}</span>
              <span v-if="t.umcDeptId" class="ml-1">D#{{ t.umcDeptId }}</span>
              <span v-if="!t.leaderUmcUserId && !t.umcDeptId">—</span>
            </td>
            <td class="px-3 py-2 text-right whitespace-nowrap">
              <button
                class="inline-flex items-center gap-1 px-2 h-7 rounded-[var(--radius)] text-[11.5px] border border-border hover:bg-muted transition-colors cursor-pointer mr-1"
                @click="openEdit(t)"
              ><Pencil class="size-3" /> 编辑</button>
              <button
                :class="[
                  'inline-flex items-center gap-1 px-2 h-7 rounded-[var(--radius)] text-[11.5px] border transition-colors cursor-pointer',
                  t.status === 'active'
                    ? 'border-border hover:bg-[color-mix(in_oklch,var(--warning)_8%,transparent)] text-[var(--warning)]'
                    : 'border-border hover:bg-[color-mix(in_oklch,var(--success)_8%,transparent)] text-[var(--success)]',
                ]"
                @click="toggleStatus(t)"
              >
                <PowerOff v-if="t.status === 'active'" class="size-3" />
                <Power v-else class="size-3" />
                {{ t.status === 'active' ? '停用' : '启用' }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>

  <!-- 编辑对话框 -->
  <Teleport to="body">
    <div v-if="editorOpen" class="fixed inset-0 z-50 bg-black/40 flex items-center justify-center" @click.self="editorOpen = false">
      <div class="bg-card border border-border rounded-[var(--radius)] shadow-lg w-[460px] max-h-[90vh] overflow-y-auto">
        <div class="p-4 border-b border-border flex items-center justify-between">
          <div class="text-[14px] font-medium">{{ editorMode === 'create' ? '新增组' : '编辑组' }}</div>
          <button class="text-muted-foreground hover:text-foreground cursor-pointer" @click="editorOpen = false"><X class="size-4" /></button>
        </div>
        <div class="p-4 space-y-3 text-[12px]">
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label class="text-[10.5px] text-muted-foreground">组编码 <span class="text-[var(--danger)]">*</span></label>
              <input
                v-model="editing.teamCode"
                :disabled="editorMode === 'edit'"
                placeholder="A / E / L-嘉禾"
                class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card font-mono disabled:opacity-50"
              />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">组名 <span class="text-[var(--danger)]">*</span></label>
              <input v-model="editing.teamName" placeholder="E 组-新人" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">组别标签</label>
              <input v-model="editing.groupLabel" placeholder="E组 / L组" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">人数</label>
              <input v-model.number="editing.headcount" type="number" min="0" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
          </div>

          <div class="border-t border-border pt-3">
            <label class="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" v-model="editing.isOutsource" class="cursor-pointer" />
              <span>外协组(无 UMC 用户,只记录合作方名)</span>
            </label>
          </div>

          <div v-if="editing.isOutsource">
            <label class="text-[10.5px] text-muted-foreground">外协合作方名 <span class="text-[var(--danger)]">*</span></label>
            <input v-model="editing.outsourcePartnerName" placeholder="嘉禾外协厂" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
          </div>

          <template v-else>
            <div>
              <label class="text-[10.5px] text-muted-foreground">组长(从 UMC 候选选) <span class="text-[var(--danger)]">*</span></label>
              <select
                :value="editing.leaderUmcUserId ?? ''"
                @change="(e) => onLeaderSelect(Number((e.target as HTMLSelectElement).value))"
                class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card cursor-pointer"
              >
                <option value="" disabled>-- 选择 UMC 用户 --</option>
                <option
                  v-for="c in candidates"
                  :key="c.umcUserId"
                  :value="c.umcUserId"
                  :disabled="c.isOccupied && c.umcUserId !== editing.leaderUmcUserId"
                >
                  {{ c.nickName || c.userName }} (U#{{ c.umcUserId }}){{ c.isOccupied && c.umcUserId !== editing.leaderUmcUserId ? ' · 已占用' : '' }}
                </option>
              </select>
              <div class="text-[10.5px] text-muted-foreground mt-1">候选用户由 MES 同步自 UMC,如需新增请先在 UMC 创建</div>
            </div>
            <div>
              <label class="text-[10.5px] text-muted-foreground">UMC 部门 ID(可选)</label>
              <input v-model.number="editing.umcDeptId" type="number" class="w-full h-8 px-2 rounded-[var(--radius)] border border-border bg-card" />
            </div>
          </template>

          <div v-if="saveError" class="text-[11.5px] text-[var(--danger)] bg-[color-mix(in_oklch,var(--danger)_8%,transparent)] px-2 py-1.5 rounded">
            {{ saveError }}
          </div>
        </div>
        <div class="p-4 border-t border-border flex justify-end gap-2">
          <button
            class="px-3 h-8 rounded-[var(--radius)] border border-border bg-card text-[12px] hover:bg-muted cursor-pointer"
            @click="editorOpen = false"
          >取消</button>
          <button
            class="px-3 h-8 rounded-[var(--radius)] bg-primary text-primary-foreground text-[12px] hover:opacity-90 cursor-pointer"
            @click="save"
          >保存</button>
        </div>
      </div>
    </div>
  </Teleport>
</template>
