<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, Eye, EyeOff } from '@lucide/vue'
import { put } from '@/utils/http'
import { useUserStore } from '@/store/modules/user'

const router = useRouter()
const userStore = useUserStore()
const saving = ref(false)
const showOld = ref(false)
const showNew = ref(false)
const showConfirm = ref(false)
const errorMsg = ref('')
const successMsg = ref('')

const form = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
})

async function handleSubmit() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!form.oldPassword) { errorMsg.value = '请输入当前密码'; return }
  if (form.newPassword.length < 8) { errorMsg.value = '新密码至少 8 位，含大小写字母和数字'; return }
  if (!/(?=.*[a-z])(?=.*[A-Z])(?=.*\d)/.test(form.newPassword)) {
    errorMsg.value = '新密码须含大小写字母和数字'
    return
  }
  if (form.newPassword !== form.confirmPassword) { errorMsg.value = '两次密码输入不一致'; return }
  saving.value = true
  try {
    await put('/system/user/profile/updatePwd', {
      oldPassword: form.oldPassword,
      newPassword: form.newPassword,
    })
    successMsg.value = '密码修改成功，请重新登录'
    setTimeout(async () => {
      await userStore.logout()
      router.push('/login')
    }, 1500)
  } catch (e: unknown) {
    errorMsg.value = (e as Error).message || '修改失败，请检查当前密码'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-subtle">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border">
      <div class="flex items-center h-14 px-4 gap-3">
        <button class="w-8 h-8 flex items-center justify-center rounded-full hover:bg-muted border-0 bg-transparent cursor-pointer" @click="router.back()">
          <ArrowLeft :size="20" class="text-foreground" />
        </button>
        <h1 class="text-[16px] font-semibold text-foreground">修改密码</h1>
      </div>
    </div>

    <div class="px-4 py-6 space-y-4 max-w-[480px] mx-auto">
      <!-- Old password -->
      <div>
        <label class="block text-[12px] font-medium text-foreground mb-1.5">当前密码 <span class="text-danger">*</span></label>
        <div class="relative">
          <input
            v-model="form.oldPassword"
            :type="showOld ? 'text' : 'password'"
            placeholder="请输入当前密码"
            class="w-full h-12 px-4 pr-11 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <button
            type="button"
            class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground border-0 bg-transparent p-0 cursor-pointer"
            @click="showOld = !showOld"
          >
            <Eye v-if="!showOld" :size="18" />
            <EyeOff v-else :size="18" />
          </button>
        </div>
      </div>

      <!-- New password -->
      <div>
        <label class="block text-[12px] font-medium text-foreground mb-1.5">新密码 <span class="text-danger">*</span></label>
        <div class="relative">
          <input
            v-model="form.newPassword"
            :type="showNew ? 'text' : 'password'"
            placeholder="请输入新密码"
            class="w-full h-12 px-4 pr-11 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <button
            type="button"
            class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground border-0 bg-transparent p-0 cursor-pointer"
            @click="showNew = !showNew"
          >
            <Eye v-if="!showNew" :size="18" />
            <EyeOff v-else :size="18" />
          </button>
        </div>
        <p class="text-[11px] text-muted-foreground mt-1">至少 8 位，含大小写字母和数字</p>
      </div>

      <!-- Confirm password -->
      <div>
        <label class="block text-[12px] font-medium text-foreground mb-1.5">确认新密码 <span class="text-danger">*</span></label>
        <div class="relative">
          <input
            v-model="form.confirmPassword"
            :type="showConfirm ? 'text' : 'password'"
            placeholder="请再次输入新密码"
            class="w-full h-12 px-4 pr-11 rounded-[var(--radius)] border border-input bg-background text-[14px] text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
          />
          <button
            type="button"
            class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground border-0 bg-transparent p-0 cursor-pointer"
            @click="showConfirm = !showConfirm"
          >
            <Eye v-if="!showConfirm" :size="18" />
            <EyeOff v-else :size="18" />
          </button>
        </div>
      </div>

      <p v-if="errorMsg" class="text-[13px] text-danger">{{ errorMsg }}</p>
      <p v-if="successMsg" class="text-[13px] text-success">{{ successMsg }}</p>

      <button
        :disabled="saving"
        class="w-full h-12 bg-primary text-primary-foreground text-[15px] font-medium rounded-[var(--radius)] hover:opacity-90 transition-opacity disabled:opacity-50 cursor-pointer border-0 mt-2"
        @click="handleSubmit"
      >
        <span v-if="saving" class="flex items-center justify-center gap-2">
          <span class="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
          修改中...
        </span>
        <span v-else>确 认 修 改</span>
      </button>
    </div>
  </div>
</template>
