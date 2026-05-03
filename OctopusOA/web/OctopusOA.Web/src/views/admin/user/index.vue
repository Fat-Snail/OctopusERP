<script setup lang="ts">
import { ref, onMounted } from 'vue'
import PageHeader from '@/components/app/PageHeader.vue'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogFooter, DialogTitle } from '@/components/ui/dialog'

import { toast } from '@/components/ui/toast'
import { getOaUserList, updateOaRoles, getOaRoleList } from '@/api/oa/user'
import type { SyncUserResponse } from '@/api/oa/types'
import type { OaRoleItem } from '@/api/oa/user'

const loading = ref(false)
const submitting = ref(false)
const users = ref<SyncUserResponse[]>([])
const roleOptions = ref<OaRoleItem[]>([])
const dialogVisible = ref(false)
const editUser = ref<SyncUserResponse | null>(null)
const editRoles = ref<string[]>([])

const roleMap: Record<string, string> = {
  oa_admin: 'OA 管理员',
  oa_user: '普通员工',
  oa_manager: '部门主管',
}

async function loadData() {
  loading.value = true
  try { users.value = (await getOaUserList()).rows } finally { loading.value = false }
}

function openRoleEdit(row: SyncUserResponse) {
  editUser.value = row; editRoles.value = [...row.oaRoles]; dialogVisible.value = true
}

function toggleRole(roleKey: string) {
  const idx = editRoles.value.indexOf(roleKey)
  if (idx >= 0) editRoles.value.splice(idx, 1)
  else editRoles.value.push(roleKey)
}

async function handleSaveRoles() {
  if (!editUser.value) return
  submitting.value = true
  try {
    await updateOaRoles(editUser.value.id, editRoles.value)
    toast('角色更新成功', 'success'); dialogVisible.value = false; loadData()
  } catch (e: unknown) { toast((e as Error).message, 'error') } finally { submitting.value = false }
}

onMounted(async () => {
  loadData()
  try { roleOptions.value = await getOaRoleList() } catch { /* ignore */ }
})
</script>

<template>
  <div>
    <PageHeader title="用户管理" sub="用户数据从 UMC 同步，此处管理 OA 本地角色分配。" />

    <div class="p-5">
      <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
      <div v-else-if="!users.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无用户</div>
      <div v-else class="border border-border rounded-[var(--radius-lg)] overflow-hidden">
        <table class="w-full text-[12px]">
          <thead class="bg-subtle">
            <tr>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">用户名</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-24">昵称</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">邮箱</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-16">状态</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide">OA 角色</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-36">同步时间</th>
              <th class="px-3 py-2 text-left text-[11px] font-medium text-muted-foreground uppercase tracking-wide w-20">操作</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr v-for="row in users" :key="row.id" class="hover:bg-subtle">
              <td class="px-3 py-2 text-muted-foreground">{{ row.userName }}</td>
              <td class="px-3 py-2 font-medium">{{ row.nickName }}</td>
              <td class="px-3 py-2 text-muted-foreground truncate max-w-[200px]">{{ row.email || '-' }}</td>
              <td class="px-3 py-2">
                <Badge :variant="row.status === 1 ? 'success' : 'danger'">{{ row.status === 1 ? '正常' : '停用' }}</Badge>
              </td>
              <td class="px-3 py-2">
                <div class="flex flex-wrap gap-1">
                  <Badge v-for="r in row.oaRoles" :key="r" variant="secondary">{{ roleMap[r] || r }}</Badge>
                  <span v-if="!row.oaRoles?.length" class="text-muted-foreground text-[11px]">无角色</span>
                </div>
              </td>
              <td class="px-3 py-2 font-mono-tnum text-muted-foreground">{{ row.lastSyncAt || '-' }}</td>
              <td class="px-3 py-2 whitespace-nowrap">
                <button class="text-[11px] text-primary hover:underline cursor-pointer border-0 bg-transparent" @click="openRoleEdit(row)">分配角色</button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Role dialog -->
    <Dialog v-model:open="dialogVisible">
      <DialogContent class="p-5 w-[380px]">
        <DialogHeader>
          <DialogTitle class="text-[14px] font-semibold">分配 OA 角色</DialogTitle>
        </DialogHeader>
        <p v-if="editUser" class="text-[12px] text-muted-foreground mb-3">
          用户：<strong class="text-foreground">{{ editUser.nickName }}</strong>（{{ editUser.userName }}）
        </p>
        <div class="space-y-2">
          <label
            v-for="r in roleOptions"
            :key="r.roleKey"
            class="flex items-center gap-2 cursor-pointer text-[12px] p-2 rounded-[var(--radius)] hover:bg-muted"
          >
            <input
              type="checkbox"
              :checked="editRoles.includes(r.roleKey)"
              class="accent-primary"
              @change="toggleRole(r.roleKey)"
            />
            {{ r.roleName }}
          </label>
        </div>
        <DialogFooter>
          <button class="px-4 py-1.5 text-[12px] border border-border rounded-[var(--radius)] hover:bg-muted cursor-pointer bg-card" @click="dialogVisible = false">取消</button>
          <button :disabled="submitting" class="px-4 py-1.5 text-[12px] bg-primary text-primary-foreground rounded-[var(--radius)] hover:brightness-105 cursor-pointer border-0 disabled:opacity-50" @click="handleSaveRoles">
            {{ submitting ? '保存中...' : '确定' }}
          </button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
