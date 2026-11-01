<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ChevronRight, Camera } from '@lucide/vue'
import { useUserStore } from '@/store/modules/user'
import { put } from '@/utils/http'

const router = useRouter()
const userStore = useUserStore()
const saving = ref(false)
const editing = ref<'nickName' | 'phoneNumber' | 'email' | null>(null)
const editValue = ref('')
const saveError = ref('')
const saveSuccess = ref('')

const info = reactive({
  nickName: '',
  phoneNumber: '',
  email: '',
})

onMounted(async () => {
  await userStore.fetchMe()
  info.nickName = userStore.userInfo?.nickName ?? ''
  info.phoneNumber = userStore.userInfo?.phoneNumber ?? ''
  info.email = userStore.userInfo?.email ?? ''
})

function startEdit(field: 'nickName' | 'phoneNumber' | 'email') {
  editing.value = field
  editValue.value = info[field]
  saveError.value = ''
  saveSuccess.value = ''
}

async function saveEdit() {
  if (!editing.value) return
  saveError.value = ''
  saving.value = true
  try {
    const payload = { ...info, [editing.value]: editValue.value }
    await put('/account/me', payload)
    info[editing.value] = editValue.value
    if (userStore.userInfo) {
      (userStore.userInfo as Record<string, unknown>)[editing.value] = editValue.value
    }
    saveSuccess.value = '保存成功'
    editing.value = null
  } catch {
    saveError.value = '保存失败，请稍后重试'
  } finally {
    saving.value = false
  }
}

const fieldLabel: Record<string, string> = {
  nickName: '昵称',
  phoneNumber: '手机号',
  email: '邮箱',
}

function maskPhone(phone: string) {
  if (!phone) return '未设置'
  return phone.replace(/(\d{3})\d{4}(\d{4})/, '$1****$2')
}

function maskEmail(email: string) {
  if (!email) return '未设置'
  const [u, d] = email.split('@')
  return u.slice(0, 2) + '***@' + d
}

const initials = () => {
  const name = userStore.userInfo?.nickName || userStore.userInfo?.userName || 'U'
  return name.slice(0, 1).toUpperCase()
}
</script>

<template>
  <div class="min-h-screen bg-subtle">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4">
        <h1 class="text-[16px] font-semibold text-foreground">账号设置</h1>
      </div>
    </div>

    <!-- Avatar + name -->
    <div class="bg-background border-b border-border py-6 flex flex-col items-center gap-2">
      <div class="relative">
        <div class="w-16 h-16 rounded-full bg-primary-soft flex items-center justify-center">
          <span class="text-primary-soft-fg font-semibold text-[24px]">{{ initials() }}</span>
        </div>
        <div class="absolute bottom-0 right-0 w-6 h-6 bg-muted border border-border rounded-full flex items-center justify-center">
          <Camera :size="12" class="text-muted-foreground" />
        </div>
      </div>
      <p class="text-[16px] font-semibold text-foreground">{{ userStore.userInfo?.nickName || '—' }}</p>
      <p class="text-[13px] text-muted-foreground">{{ userStore.userInfo?.userName }}</p>
    </div>

    <!-- Basic info section -->
    <div class="mt-3">
      <p class="text-[11px] text-muted-foreground uppercase tracking-wide px-4 mb-2">基本信息</p>
      <div class="bg-background border-y border-border divide-y divide-border">
        <!-- Nickname -->
        <button
          class="w-full flex items-center justify-between px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="startEdit('nickName')"
        >
          <span class="text-[14px] text-foreground">昵称</span>
          <div class="flex items-center gap-2">
            <span class="text-[14px] text-muted-foreground">{{ info.nickName || '未设置' }}</span>
            <ChevronRight :size="16" class="text-muted-foreground" />
          </div>
        </button>
        <!-- Phone -->
        <button
          class="w-full flex items-center justify-between px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="startEdit('phoneNumber')"
        >
          <span class="text-[14px] text-foreground">手机号</span>
          <div class="flex items-center gap-2">
            <span class="text-[14px] text-muted-foreground">{{ maskPhone(info.phoneNumber) }}</span>
            <ChevronRight :size="16" class="text-muted-foreground" />
          </div>
        </button>
        <!-- Email -->
        <button
          class="w-full flex items-center justify-between px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="startEdit('email')"
        >
          <span class="text-[14px] text-foreground">邮箱</span>
          <div class="flex items-center gap-2">
            <span class="text-[14px] text-muted-foreground">{{ maskEmail(info.email) }}</span>
            <ChevronRight :size="16" class="text-muted-foreground" />
          </div>
        </button>
      </div>
    </div>

    <!-- Security section -->
    <div class="mt-3">
      <p class="text-[11px] text-muted-foreground uppercase tracking-wide px-4 mb-2">安全</p>
      <div class="bg-background border-y border-border">
        <button
          class="w-full flex items-center justify-between px-4 py-4 hover:bg-muted/50 transition-colors border-0 bg-transparent cursor-pointer text-left"
          @click="router.push('/h5/profile/pwd')"
        >
          <span class="text-[14px] text-foreground">修改密码</span>
          <ChevronRight :size="16" class="text-muted-foreground" />
        </button>
      </div>
    </div>

    <!-- Edit overlay -->
    <Teleport to="body">
      <div v-if="editing" class="fixed inset-0 z-50">
        <div class="absolute inset-0 bg-black/40" @click="editing = null" />
        <div class="absolute bottom-0 left-0 right-0 bg-background rounded-t-2xl p-6 pb-8">
          <h3 class="text-[16px] font-semibold text-foreground mb-4">修改{{ fieldLabel[editing] }}</h3>
          <input
            v-model="editValue"
            :placeholder="`请输入${fieldLabel[editing]}`"
            class="w-full h-12 px-4 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring mb-3"
            @keyup.enter="saveEdit"
          />
          <p v-if="saveError" class="text-[12px] text-danger mb-3">{{ saveError }}</p>
          <button
            :disabled="saving"
            class="w-full h-12 bg-primary text-primary-foreground text-[15px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0"
            @click="saveEdit"
          >
            {{ saving ? '保存中...' : '确 认' }}
          </button>
        </div>
      </div>
    </Teleport>

    <!-- Success toast -->
    <Teleport to="body">
      <div
        v-if="saveSuccess"
        class="fixed top-4 left-1/2 -translate-x-1/2 z-[60] bg-success text-white text-[13px] px-4 py-2 rounded-full shadow"
        @click="saveSuccess = ''"
      >
        {{ saveSuccess }}
      </div>
    </Teleport>
  </div>
</template>
