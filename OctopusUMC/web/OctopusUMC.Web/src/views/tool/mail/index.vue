<script setup lang="ts">
import { ref, reactive } from 'vue'
import { Send } from 'lucide-vue-next'
import { post } from '@/utils/http'

const submitting = ref(false)
const submitError = ref('')
const submitSuccess = ref(false)

const formData = reactive({
  to: '',
  subject: '',
  content: '',
})

async function handleSend() {
  if (!formData.to.trim()) { submitError.value = '请输入收件人邮箱'; return }
  if (!formData.subject.trim()) { submitError.value = '请输入邮件主题'; return }
  if (!formData.content.trim()) { submitError.value = '请输入邮件内容'; return }
  submitError.value = ''
  submitSuccess.value = false
  submitting.value = true
  try {
    await post('/system/mail/send', formData)
    submitSuccess.value = true
    Object.assign(formData, { to: '', subject: '', content: '' })
  } catch { submitError.value = '发送失败，请检查邮件配置' } finally { submitting.value = false }
}
</script>

<template>
  <div class="max-w-2xl">
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <div class="px-4 py-3 border-b border-border">
        <span class="text-[13px] font-semibold text-foreground">发送邮件</span>
      </div>
      <div class="p-4 space-y-4">
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">收件人 <span class="text-danger">*</span></label>
          <input v-model="formData.to" type="email" placeholder="多个收件人用英文逗号分隔" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">邮件主题 <span class="text-danger">*</span></label>
          <input v-model="formData.subject" placeholder="请输入邮件主题" class="w-full h-8 px-2.5 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring" />
        </div>
        <div>
          <label class="block text-[12px] font-medium text-foreground mb-1">邮件内容 <span class="text-danger">*</span></label>
          <textarea v-model="formData.content" rows="8" placeholder="请输入邮件内容" class="w-full px-2.5 py-2 rounded-[var(--radius)] border border-input bg-background text-[12px] focus:outline-none focus:ring-1 focus:ring-ring resize-y" />
        </div>
        <p v-if="submitError" class="text-[12px] text-danger">{{ submitError }}</p>
        <p v-if="submitSuccess" class="text-[12px] text-success">邮件发送成功！</p>
        <div class="flex justify-end">
          <button :disabled="submitting" class="h-8 px-5 bg-primary text-primary-foreground text-[12px] rounded-[var(--radius)] flex items-center gap-1.5 hover:opacity-90 disabled:opacity-50 cursor-pointer border-0" @click="handleSend">
            <Send :size="13" /> {{ submitting ? '发送中...' : '发送' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
