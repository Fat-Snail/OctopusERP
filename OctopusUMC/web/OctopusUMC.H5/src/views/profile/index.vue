<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ChevronRight, LogOut, Key } from '@lucide/vue'
import { useUserStore } from '@/store/user'
import { put } from '@/utils/http'

const router = useRouter()
const store = useUserStore()

const initials = computed(() => (store.userName || 'U').slice(0, 1).toUpperCase())
const saving = ref(false)
const editing = ref<'nickName' | 'phoneNumber' | 'email' | null>(null)
const editValue = ref('')
const saveError = ref('')
const saveSuccess = ref('')

const info = reactive({ nickName: '', phoneNumber: '', email: '' })

onMounted(async () => {
  await store.fetchMe()
  info.nickName = store.userInfo?.nickName ?? ''
  info.phoneNumber = store.userInfo?.phoneNumber ?? ''
  info.email = store.userInfo?.email ?? ''
})

function startEdit(field: 'nickName' | 'phoneNumber' | 'email') {
  editing.value = field
  editValue.value = info[field]
  saveError.value = ''
  saveSuccess.value = ''
}

async function saveEdit() {
  if (!editing.value) return
  saving.value = true
  try {
    await put('/account/me', { ...info, [editing.value]: editValue.value })
    info[editing.value] = editValue.value
    if (store.userInfo) (store.userInfo as Record<string, unknown>)[editing.value] = editValue.value
    saveSuccess.value = '保存成功'
    editing.value = null
  } catch {
    saveError.value = '保存失败'
  } finally {
    saving.value = false
  }
}

async function handleLogout() {
  if (confirm('确认退出登录？')) {
    await store.logout()
    router.replace('/login')
  }
}

const fieldLabel: Record<string, string> = { nickName: '昵称', phoneNumber: '手机号', email: '邮箱' }
</script>

<template>
  <div class="min-h-full bg-subtle">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4">
        <h1 class="text-[17px] font-semibold text-foreground">我的</h1>
      </div>
    </div>

    <!-- User card -->
    <div class="bg-background border-b border-border py-6 flex items-center gap-4 px-5">
      <div class="w-14 h-14 rounded-full bg-primary-soft flex items-center justify-center shrink-0">
        <span class="text-primary-soft-fg font-semibold text-[22px]">{{ initials }}</span>
      </div>
      <div class="flex-1 min-w-0">
        <p class="text-[16px] font-semibold text-foreground">{{ store.userInfo?.nickName || store.userInfo?.userName }}</p>
        <p class="text-[12px] text-muted-foreground mt-0.5 truncate">{{ store.userInfo?.email }}</p>
      </div>
    </div>

    <!-- Edit fields -->
    <div class="mt-3">
      <p class="text-[11px] text-muted-foreground uppercase tracking-wide px-4 mb-2">个人资料</p>
      <div class="bg-background border-y border-border divide-y divide-border">
        <div
          v-for="field in (['nickName', 'phoneNumber', 'email'] as const)"
          :key="field"
          class="flex items-center px-4 py-3.5 gap-3"
        >
          <span class="text-[14px] text-muted-foreground w-16 shrink-0">{{ fieldLabel[field] }}</span>
          <template v-if="editing !== field">
            <span class="flex-1 text-[14px] text-foreground">{{ info[field] || '未设置' }}</span>
            <button class="text-[13px] text-primary" @click="startEdit(field)">编辑</button>
          </template>
          <template v-else>
            <input
              v-model="editValue"
              class="flex-1 text-[14px] border-b border-primary bg-transparent outline-none py-0.5"
              @keyup.enter="saveEdit"
            />
            <button class="text-[13px] text-muted-foreground mr-2" @click="editing = null">取消</button>
            <button class="text-[13px] text-primary" :disabled="saving" @click="saveEdit">保存</button>
          </template>
        </div>
      </div>
      <p v-if="saveSuccess" class="text-[12px] text-success px-4 mt-2">{{ saveSuccess }}</p>
      <p v-if="saveError" class="text-[12px] text-danger px-4 mt-2">{{ saveError }}</p>
    </div>

    <!-- Actions -->
    <div class="mt-3">
      <div class="bg-background border-y border-border divide-y divide-border">
        <button
          class="w-full flex items-center px-4 py-3.5 gap-3 bg-transparent border-0 text-left cursor-pointer"
          @click="router.push('/profile/pwd')"
        >
          <Key :size="18" class="text-muted-foreground" />
          <span class="flex-1 text-[14px] text-foreground">修改密码</span>
          <ChevronRight :size="16" class="text-muted-foreground" />
        </button>
        <button
          class="w-full flex items-center px-4 py-3.5 gap-3 bg-transparent border-0 text-left cursor-pointer"
          @click="handleLogout"
        >
          <LogOut :size="18" class="text-danger" />
          <span class="flex-1 text-[14px] text-danger">退出登录</span>
        </button>
      </div>
    </div>
  </div>
</template>
