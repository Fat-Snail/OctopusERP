<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft } from '@lucide/vue'
import { post } from '@/utils/http'

const router = useRouter()
const form = ref({ oldPassword: '', newPassword: '', confirmPassword: '' })
const loading = ref(false)
const error = ref('')
const success = ref('')

async function handleSubmit() {
  if (!form.value.oldPassword || !form.value.newPassword || !form.value.confirmPassword) {
    error.value = '请填写所有字段'; return
  }
  if (form.value.newPassword !== form.value.confirmPassword) {
    error.value = '两次密码不一致'; return
  }
  if (form.value.newPassword.length < 6) {
    error.value = '新密码至少 6 位'; return
  }
  loading.value = true
  error.value = ''
  try {
    await post('/account/profile/updatePwd', form.value)
    success.value = '密码修改成功'
    form.value = { oldPassword: '', newPassword: '', confirmPassword: '' }
    setTimeout(() => router.back(), 1500)
  } catch (e: unknown) {
    error.value = (e as Error).message || '修改失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-dvh bg-subtle">
    <!-- Header -->
    <div class="sticky top-0 z-10 bg-background border-b border-border flex items-center h-14 px-4 gap-3">
      <button class="bg-transparent border-0 p-0 cursor-pointer" @click="router.back()">
        <ArrowLeft :size="20" class="text-foreground" />
      </button>
      <h1 class="text-[17px] font-semibold text-foreground">修改密码</h1>
    </div>

    <div class="px-4 py-6 space-y-4">
      <div v-for="(placeholder, key) in { oldPassword: '当前密码', newPassword: '新密码（至少6位）', confirmPassword: '确认新密码' }" :key="key">
        <label class="text-[13px] text-muted-foreground block mb-1">{{ placeholder }}</label>
        <input
          v-model="form[key as keyof typeof form]"
          type="password"
          :placeholder="placeholder"
          class="w-full h-12 px-4 rounded-xl border border-border bg-background text-[15px] outline-none focus:border-[hsl(var(--primary))]"
        />
      </div>
      <p v-if="error" class="text-[13px] text-danger">{{ error }}</p>
      <p v-if="success" class="text-[13px] text-success">{{ success }}</p>
      <button
        class="w-full h-12 rounded-xl bg-[hsl(var(--primary))] text-white text-[16px] font-semibold disabled:opacity-60"
        :disabled="loading"
        @click="handleSubmit"
      >
        {{ loading ? '提交中...' : '确认修改' }}
      </button>
    </div>
  </div>
</template>
