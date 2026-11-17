<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Loader2 } from '@lucide/vue'
import { oidcService } from '@/services/oidcService'

const router = useRouter()
const error = ref('')

onMounted(async () => {
  try {
    await oidcService.handleCallback()
    router.replace('/home')
  } catch (e) {
    error.value = (e as Error).message ?? '登录回调处理失败'
  }
})

function backToLogin() {
  router.replace('/login')
}
</script>

<template>
  <div class="min-h-dvh bg-background flex flex-col items-center justify-center gap-4 px-8">
    <template v-if="!error">
      <Loader2 class="animate-spin text-primary" :size="32" />
      <p class="text-muted-foreground text-sm">正在完成登录…</p>
    </template>
    <template v-else>
      <p class="text-danger text-sm text-center leading-relaxed">{{ error }}</p>
      <button
        class="h-10 px-6 rounded-xl bg-primary text-primary-foreground text-sm cursor-pointer border-0"
        @click="backToLogin"
      >
        返回登录
      </button>
    </template>
  </div>
</template>
