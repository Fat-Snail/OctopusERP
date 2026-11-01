<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, Search, Phone, Mail, ChevronRight } from '@lucide/vue'
import { getDeptTree, getUsers } from '@/api/contact'
import type { ContactDeptNode, ContactUser } from '@/api/contact/types'

const router = useRouter()
const deptTree = ref<ContactDeptNode[]>([])
const users = ref<ContactUser[]>([])
const loading = ref(true)
const selectedDept = ref<number | null>(null)
const keyword = ref('')
const detailUser = ref<ContactUser | null>(null)

async function loadUsers() {
  loading.value = true
  try {
    const res = await getUsers({ deptId: selectedDept.value ?? undefined, keyword: keyword.value || undefined })
    users.value = res.rows
  } finally {
    loading.value = false
  }
}

function selectDept(deptId: number) {
  selectedDept.value = selectedDept.value === deptId ? null : deptId
  loadUsers()
}

function initials(user: ContactUser) {
  return (user.nickName || user.userName).slice(0, 1).toUpperCase()
}

onMounted(async () => {
  const [tree] = await Promise.all([getDeptTree(), loadUsers()])
  deptTree.value = tree
})
</script>

<template>
  <div class="min-h-full">
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4 gap-3">
        <button
          class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-muted border-0 bg-transparent cursor-pointer"
          @click="router.push('/home')"
        >
          <ArrowLeft :size="20" class="text-foreground" />
        </button>
        <h1 class="text-[16px] font-semibold text-foreground">通讯录</h1>
      </div>
      <div class="px-4 pb-3">
        <div class="relative">
          <Search :size="15" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
          <input
            v-model="keyword"
            placeholder="搜索姓名/用户名"
            class="w-full h-9 pl-9 pr-3 rounded-lg border border-border bg-muted text-[13px] text-foreground focus:outline-none focus:ring-2 focus:ring-primary"
            @keyup.enter="loadUsers"
          />
        </div>
      </div>
    </div>

    <div class="flex" style="height: calc(100dvh - 120px)">
      <!-- Dept sidebar -->
      <div class="w-24 bg-muted border-r border-border overflow-y-auto shrink-0">
        <button
          :class="['w-full py-3 text-[12px] text-center border-b border-border cursor-pointer border-0 transition-colors', !selectedDept ? 'bg-background text-primary font-medium' : 'text-muted-foreground hover:bg-background/60']"
          @click="selectDept(0)"
        >
          全部
        </button>
        <button
          v-for="dept in deptTree"
          :key="dept.deptId"
          :class="['w-full py-3 px-2 text-[11px] text-center border-b border-border/50 cursor-pointer border-0 transition-colors leading-snug', selectedDept === dept.deptId ? 'bg-background text-primary font-medium' : 'text-muted-foreground hover:bg-background/60']"
          @click="selectDept(dept.deptId)"
        >
          {{ dept.deptName }}
          <span class="block text-[10px] text-muted-foreground">{{ dept.userCount }}人</span>
        </button>
      </div>

      <!-- User list -->
      <div class="flex-1 overflow-y-auto bg-background">
        <div v-if="loading" class="py-12 text-center text-muted-foreground text-[12px]">加载中...</div>
        <div v-else-if="!users.length" class="py-12 text-center text-muted-foreground text-[12px]">暂无成员</div>
        <div v-else class="divide-y divide-border">
          <div
            v-for="user in users"
            :key="user.umcUserId"
            class="flex items-center gap-3 px-4 py-3 cursor-pointer hover:bg-muted/30 transition-colors"
            @click="detailUser = user"
          >
            <div class="w-10 h-10 rounded-full bg-primary-soft flex items-center justify-center shrink-0">
              <span class="text-primary-soft-fg font-semibold text-[15px]">{{ initials(user) }}</span>
            </div>
            <div class="flex-1 min-w-0">
              <p class="text-[14px] font-medium text-foreground">{{ user.nickName }}</p>
              <p class="text-[11px] text-muted-foreground truncate">{{ user.depts.map(d => d.deptName).join(' / ') }}</p>
            </div>
            <ChevronRight :size="16" class="text-muted-foreground shrink-0" />
          </div>
        </div>
      </div>
    </div>

    <!-- User detail sheet -->
    <Teleport to="body">
      <div v-if="detailUser" class="fixed inset-0 z-50">
        <div class="absolute inset-0 bg-black/40" @click="detailUser = null" />
        <div class="absolute bottom-0 left-0 right-0 bg-background rounded-t-2xl p-5 pb-10">
          <div class="flex items-center gap-4 mb-5">
            <div class="w-14 h-14 rounded-full bg-primary-soft flex items-center justify-center">
              <span class="text-primary-soft-fg font-semibold text-[22px]">{{ initials(detailUser) }}</span>
            </div>
            <div>
              <p class="text-[18px] font-semibold text-foreground">{{ detailUser.nickName }}</p>
              <p class="text-[12px] text-muted-foreground">{{ detailUser.userName }}</p>
            </div>
          </div>
          <div class="space-y-3 mb-5">
            <div class="flex items-center gap-3">
              <span class="text-[12px] text-muted-foreground w-16">部门</span>
              <span class="text-[13px] text-foreground">{{ detailUser.depts.map(d => d.deptName).join(' / ') || '—' }}</span>
            </div>
            <div v-if="detailUser.phoneNumber" class="flex items-center gap-3">
              <span class="text-[12px] text-muted-foreground w-16">手机</span>
              <a :href="`tel:${detailUser.phoneNumber}`" class="text-[13px] text-primary no-underline">{{ detailUser.phoneNumber }}</a>
            </div>
            <div v-if="detailUser.email" class="flex items-center gap-3">
              <span class="text-[12px] text-muted-foreground w-16">邮箱</span>
              <a :href="`mailto:${detailUser.email}`" class="text-[13px] text-primary no-underline truncate">{{ detailUser.email }}</a>
            </div>
          </div>
          <div class="flex gap-3">
            <a
              v-if="detailUser.phoneNumber"
              :href="`tel:${detailUser.phoneNumber}`"
              class="flex-1 h-11 bg-primary-soft text-[var(--primary-soft-fg)] text-[14px] font-medium rounded-xl flex items-center justify-center gap-2 no-underline"
            >
              <Phone :size="16" /> 拨打
            </a>
            <a
              v-if="detailUser.email"
              :href="`mailto:${detailUser.email}`"
              class="flex-1 h-11 bg-muted text-foreground text-[14px] font-medium rounded-xl flex items-center justify-center gap-2 no-underline"
            >
              <Mail :size="16" /> 邮件
            </a>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
