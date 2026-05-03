<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Search, RefreshCw, Plus, Trash2, Edit, KeyRound } from 'lucide-vue-next'
import { get, post, put, del } from '@/utils/http'
import type { UserResponse, DeptResponse, RoleResponse, PostResponse } from '@/api/system/types'
import type { PagedResult } from '@/api/types'
import Badge from '@/components/ui/badge.vue'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog'

const loading = ref(false)
const submitting = ref(false)
const users = ref<UserResponse[]>([])
const total = ref(0)
const selectedIds = ref<number[]>([])
const deptTree = ref<DeptResponse[]>([])
const roles = ref<RoleResponse[]>([])
const posts = ref<PostResponse[]>([])
const dialogOpen = ref(false)
const resetPwdOpen = ref(false)
const isEdit = ref(false)
const submitError = ref('')

interface UserForm {
  userId?: number
  userName: string
  nickName: string
  email: string
  phoneNumber: string
  sex: string
  password: string
  deptIds: number[]
  postIds: number[]
  roleIds: number[]
  status: 0 | 1
  remark: string
}

const query = reactive({
  pageNum: 1,
  pageSize: 10,
  userName: '',
  phoneNumber: '',
  status: undefined as number | undefined,
  deptId: undefined as number | undefined,
})

const formData = reactive<UserForm>({
  userName: '', nickName: '', email: '', phoneNumber: '',
  sex: '1', password: '', deptIds: [], postIds: [], roleIds: [], status: 1, remark: '',
})

const pwdForm = reactive({ userId: 0, userName: '', password: '' })

async function loadUsers() {
  loading.value = true
  try {
    const data = await get<PagedResult<UserResponse>>('/system/user/list', { ...query })
    users.value = data.rows
    total.value = data.total
  } finally { loading.value = false }
}

async function loadDepts() {
  const data = await get<DeptResponse[]>('/system/dept/tree')
  deptTree.value = data
}

async function loadRoles() {
  const data = await get<PagedResult<RoleResponse>>('/system/role/list', { pageNum: 1, pageSize: 100 })
  roles.value = data.rows
}

async function loadPosts() {
  const data = await get<PagedResult<PostResponse>>('/system/post/list', { pageNum: 1, pageSize: 100 })
  posts.value = data.rows
}

function resetQuery() {
  query.userName = ''
  query.phoneNumber = ''
  query.status = undefined
  query.deptId = undefined
  loadUsers()
}

function handleDeptClick(node: DeptResponse) {
  query.deptId = node.deptId
  loadUsers()
}

function toggleSelect(userId: number) {
  const idx = selectedIds.value.indexOf(userId)
  if (idx >= 0) selectedIds.value.splice(idx, 1)
  else selectedIds.value.push(userId)
}

function toggleSelectAll() {
  if (selectedIds.value.length === users.value.length) {
    selectedIds.value = []
  } else {
    selectedIds.value = users.value.map(u => u.userId)
  }
}

function openAdd() {
  isEdit.value = false
  Object.assign(formData, { userName: '', nickName: '', email: '', phoneNumber: '', sex: '1', password: '', deptIds: [], postIds: [], roleIds: [], status: 1, remark: '' })
  delete formData.userId
  submitError.value = ''
  dialogOpen.value = true
}

function openEdit(row: UserResponse) {
  isEdit.value = true
  Object.assign(formData, {
    userId: row.userId,
    userName: row.userName,
    nickName: row.nickName,
    email: row.email,
    phoneNumber: row.phoneNumber ?? '',
    sex: row.sex,
    deptIds: row.deptIds ?? (row.deptId ? [row.deptId] : []),
    postIds: row.postIds ?? (row.postId ? [row.postId] : []),
    roleIds: [],
    status: row.status,
    remark: row.remark ?? '',
  })
  submitError.value = ''
  dialogOpen.value = true
}

async function handleSubmit() {
  if (!formData.userName) { submitError.value = '请输入用户名'; return }
  if (!formData.nickName) { submitError.value = '请输入昵称'; return }
  if (!formData.email) { submitError.value = '请输入邮箱'; return }
  if (!isEdit.value && !formData.password) { submitError.value = '请输入密码'; return }
  submitError.value = ''
  submitting.value = true
  try {
    if (isEdit.value) {
      await put('/system/user', formData)
    } else {
      await post('/system/user', formData)
    }
    dialogOpen.value = false
    loadUsers()
  } catch {
    submitError.value = '操作失败，请重试'
  } finally { submitting.value = false }
}

async function handleDelete(row: UserResponse) {
  if (!confirm(`确认删除用户 "${row.userName}" 吗？`)) return
  await del(`/system/user/${row.userId}`)
  loadUsers()
}

async function handleBatchDelete() {
  if (!selectedIds.value.length) return
  if (!confirm(`确认删除选中的 ${selectedIds.value.length} 个用户吗？`)) return
  await del(`/system/user/${selectedIds.value.join(',')}`)
  selectedIds.value = []
  loadUsers()
}

async function handleStatusChange(row: UserResponse) {
  await put('/system/user/status', { userId: row.userId, status: row.status })
}

function openResetPwd(row: UserResponse) {
  pwdForm.userId = row.userId
  pwdForm.userName = row.userName
  pwdForm.password = ''
  resetPwdOpen.value = true
}

async function handleResetPwd() {
  if (!pwdForm.password) return
  await put('/system/user/resetPwd', { userId: pwdForm.userId, password: pwdForm.password })
  resetPwdOpen.value = false
}

// Flatten dept tree for rendering
function flattenDepts(nodes: DeptResponse[], depth = 0): Array<DeptResponse & { depth: number }> {
  const result: Array<DeptResponse & { depth: number }> = []
  for (const n of nodes) {
    result.push({ ...n, depth })
    if (n.children?.length) result.push(...flattenDepts(n.children, depth + 1))
  }
  return result
}

onMounted(() => {
  loadUsers()
  loadDepts()
  loadRoles()
  loadPosts()
})
</script>

<template>
  <div class="flex gap-3 h-[calc(100vh-160px)]">
    <!-- Dept tree sidebar -->
    <div class="w-[200px] shrink-0 bg-card border border-border rounded-lg overflow-hidden flex flex-col">
      <div class="px-3 py-2.5 border-b border-border">
        <span class="text-[12px] font-semibold text-foreground">组织机构</span>
      </div>
      <div class="flex-1 overflow-y-auto py-1">
        <button
          v-for="node in flattenDepts(deptTree)"
          :key="node.deptId"
          :class="[
            'w-full flex items-center h-[var(--row-h)] px-2 text-[12px] rounded transition-colors cursor-pointer border-0 text-left',
            query.deptId === node.deptId
              ? 'bg-primary-soft text-primary-soft-fg font-medium'
              : 'text-muted-foreground hover:bg-muted hover:text-foreground'
          ]"
          :style="{ paddingLeft: `${8 + node.depth * 12}px` }"
          @click="handleDeptClick(node)"
        >
          {{ node.deptName }}
        </button>
      </div>
    </div>

    <!-- Main content -->
    <div class="flex-1 min-w-0 flex flex-col bg-card border border-border rounded-lg overflow-hidden">
      <!-- Search bar -->
      <div class="flex items-center gap-2 px-4 py-3 border-b border-border flex-wrap">
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">用户名</span>
          <input
            v-model="query.userName"
            placeholder="用户名"
            class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">手机号</span>
          <input
            v-model="query.phoneNumber"
            placeholder="手机号"
            class="h-7 w-[120px] px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
        <div class="flex items-center gap-1.5">
          <span class="text-[12px] text-muted-foreground">状态</span>
          <select
            v-model="query.status"
            class="h-7 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring"
          >
            <option :value="undefined">全部</option>
            <option :value="1">正常</option>
            <option :value="0">停用</option>
          </select>
        </div>
        <button
          class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0"
          @click="loadUsers"
        >
          <Search :size="12" /> 搜索
        </button>
        <button
          class="h-7 px-3 bg-muted text-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-muted/80 cursor-pointer border border-border"
          @click="resetQuery"
        >
          <RefreshCw :size="12" /> 重置
        </button>
      </div>

      <!-- Action bar -->
      <div class="flex items-center gap-2 px-4 py-2.5 border-b border-border">
        <button
          class="h-7 px-3 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 cursor-pointer border-0"
          @click="openAdd"
        >
          <Plus :size="12" /> 新增
        </button>
        <button
          :disabled="!selectedIds.length"
          class="h-7 px-3 bg-danger/10 text-danger text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:bg-danger/20 cursor-pointer border border-danger/20 disabled:opacity-40"
          @click="handleBatchDelete"
        >
          <Trash2 :size="12" /> 删除
        </button>
      </div>

      <!-- Table -->
      <div class="flex-1 overflow-auto">
        <table class="w-full">
          <thead class="sticky top-0 z-10">
            <tr class="bg-subtle border-b border-border">
              <th class="w-10 px-3 py-2.5 text-center">
                <input
                  type="checkbox"
                  :checked="selectedIds.length === users.length && users.length > 0"
                  class="w-3.5 h-3.5 rounded border-border cursor-pointer"
                  @change="toggleSelectAll"
                />
              </th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">用户名</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">昵称</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">部门</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">手机号</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">邮箱</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">状态</th>
              <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">创建时间</th>
              <th class="text-right text-[11px] uppercase tracking-wide text-muted-foreground px-3 py-2.5 font-medium">操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="loading">
              <td colspan="9" class="py-8 text-center text-[12px] text-muted-foreground">加载中...</td>
            </tr>
            <tr v-else-if="!users.length">
              <td colspan="9" class="py-8 text-center text-[12px] text-muted-foreground">暂无数据</td>
            </tr>
            <tr
              v-else
              v-for="row in users"
              :key="row.userId"
              class="border-b border-border last:border-0 hover:bg-subtle"
            >
              <td class="px-3 py-2 text-center">
                <input
                  type="checkbox"
                  :checked="selectedIds.includes(row.userId)"
                  class="w-3.5 h-3.5 rounded border-border cursor-pointer"
                  @change="toggleSelect(row.userId)"
                />
              </td>
              <td class="px-3 py-2 text-[12px] text-foreground font-medium">{{ row.userName }}</td>
              <td class="px-3 py-2 text-[12px] text-foreground">{{ row.nickName }}</td>
              <td class="px-3 py-2 text-[12px] text-muted-foreground">{{ row.deptName }}</td>
              <td class="px-3 py-2 text-[12px] font-mono-tnum text-foreground">{{ row.phoneNumber || '-' }}</td>
              <td class="px-3 py-2 text-[12px] text-muted-foreground max-w-[160px] truncate">{{ row.email }}</td>
              <td class="px-3 py-2">
                <input
                  type="checkbox"
                  :checked="row.status === 1"
                  class="w-8 h-4 cursor-pointer appearance-none rounded-full transition-colors"
                  :style="{
                    backgroundColor: row.status === 1 ? 'var(--color-primary)' : 'var(--color-muted-foreground)',
                  }"
                  @change="() => { row.status = row.status === 1 ? 0 : 1; handleStatusChange(row) }"
                />
              </td>
              <td class="px-3 py-2 text-[12px] text-muted-foreground">{{ row.createTime.slice(0, 10) }}</td>
              <td class="px-3 py-2 text-right">
                <div class="flex items-center justify-end gap-1">
                  <button
                    class="h-6 px-2 text-[11px] text-primary hover:bg-primary-soft rounded cursor-pointer border-0 bg-transparent flex items-center gap-1"
                    @click="openEdit(row)"
                  >
                    <Edit :size="11" /> 编辑
                  </button>
                  <button
                    class="h-6 px-2 text-[11px] text-danger hover:bg-danger/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1"
                    @click="handleDelete(row)"
                  >
                    <Trash2 :size="11" /> 删除
                  </button>
                  <button
                    class="h-6 px-2 text-[11px] text-warning hover:bg-warning/10 rounded cursor-pointer border-0 bg-transparent flex items-center gap-1"
                    @click="openResetPwd(row)"
                  >
                    <KeyRound :size="11" /> 重置密码
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination -->
      <div class="flex items-center justify-between px-4 py-2.5 border-t border-border bg-card">
        <span class="text-[12px] text-muted-foreground">共 {{ total }} 条</span>
        <div class="flex items-center gap-1">
          <button
            :disabled="query.pageNum <= 1"
            class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80"
            @click="() => { query.pageNum--; loadUsers() }"
          >
            上一页
          </button>
          <span class="h-7 px-3 flex items-center text-[12px] text-foreground">{{ query.pageNum }}</span>
          <button
            :disabled="query.pageNum * query.pageSize >= total"
            class="h-7 px-2 text-[12px] text-foreground bg-muted rounded border border-border disabled:opacity-40 cursor-pointer hover:bg-muted/80"
            @click="() => { query.pageNum++; loadUsers() }"
          >
            下一页
          </button>
        </div>
      </div>
    </div>
  </div>

  <!-- User form dialog -->
  <Dialog v-model:open="dialogOpen">
    <DialogContent class="w-[600px]">
      <DialogHeader>
        <DialogTitle>{{ isEdit ? '编辑用户' : '新增用户' }}</DialogTitle>
      </DialogHeader>
      <div class="grid grid-cols-2 gap-4 py-2">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">用户名 <span class="text-danger">*</span></label>
          <input v-model="formData.userName" :disabled="isEdit" placeholder="登录用户名" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring disabled:bg-muted disabled:text-muted-foreground" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">昵称 <span class="text-danger">*</span></label>
          <input v-model="formData.nickName" placeholder="用户昵称" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">邮箱 <span class="text-danger">*</span></label>
          <input v-model="formData.email" placeholder="邮箱地址" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">手机号</label>
          <input v-model="formData.phoneNumber" placeholder="手机号码" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div v-if="!isEdit">
          <label class="block text-[12px] font-medium text-foreground mb-1">密码 <span class="text-danger">*</span></label>
          <input v-model="formData.password" type="password" placeholder="登录密码" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">性别</label>
          <select v-model="formData.sex" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option value="1">男</option>
            <option value="0">女</option>
            <option value="2">未知</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">职位</label>
          <select v-model="formData.postIds[0]" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option :value="undefined">请选择职位</option>
            <option v-for="p in posts" :key="p.postId" :value="p.postId">{{ p.postName }}</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">角色</label>
          <select v-model="formData.roleIds[0]" class="w-full h-8 px-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring">
            <option :value="undefined">请选择角色</option>
            <option v-for="r in roles" :key="r.roleId" :value="r.roleId">{{ r.roleName }}</option>
          </select>
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">状态</label>
          <div class="flex gap-4">
            <label v-for="opt in [{ v: 1, l: '正常' }, { v: 0, l: '停用' }]" :key="opt.v" class="flex items-center gap-1.5 cursor-pointer">
              <input v-model="formData.status" type="radio" :value="opt.v" class="w-3.5 h-3.5" />
              <span class="text-[12px] text-foreground">{{ opt.l }}</span>
            </label>
          </div>
        </div>
        <div class="col-span-2">
          <label class="block text-[12px] font-medium text-foreground mb-1">备注</label>
          <textarea v-model="formData.remark" rows="2" class="w-full px-2.5 py-1.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring resize-none" />
        </div>
      </div>
      <p v-if="submitError" class="text-[12px] text-danger mt-1">{{ submitError }}</p>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="dialogOpen = false">取消</button>
        <button :disabled="submitting" class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSubmit">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>

  <!-- Reset password dialog -->
  <Dialog v-model:open="resetPwdOpen">
    <DialogContent class="w-[400px]">
      <DialogHeader>
        <DialogTitle>重置密码</DialogTitle>
      </DialogHeader>
      <div class="space-y-3 py-2">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">用户名</label>
          <input :value="pwdForm.userName" disabled class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-muted text-[12px] text-muted-foreground" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">新密码</label>
          <input v-model="pwdForm.password" type="password" placeholder="请输入新密码" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
      </div>
      <DialogFooter>
        <button class="h-8 px-4 bg-muted text-foreground text-[12px] rounded-[var(--radius)] hover:bg-muted/80 cursor-pointer border border-border" @click="resetPwdOpen = false">取消</button>
        <button class="h-8 px-4 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] hover:opacity-90 cursor-pointer border-0" @click="handleResetPwd">确定</button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
