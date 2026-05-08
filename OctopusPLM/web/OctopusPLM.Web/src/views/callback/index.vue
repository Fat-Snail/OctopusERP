<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { oidcService } from '@/services/oidcService'

const router = useRouter()
const errorMsg = ref('')

onMounted(async () => {
  try {
    const hasCode = window.location.search.includes('code=')
    const hasState = window.location.search.includes('state=')
    if (!hasCode || !hasState) {
      errorMsg.value = `缺少 OIDC 参数: code=${hasCode}, state=${hasState}`
      return
    }
    await oidcService.handleCallback()
    router.replace('/plm/product')
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e)
    errorMsg.value = `登录失败: ${msg}`
    console.error('OIDC callback error:', e)
  }
})
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-background">
    <div class="text-center">
      <div v-if="errorMsg" class="text-[13px] text-danger">{{ errorMsg }}</div>
      <template v-else>
        <div class="w-10 h-10 border-2 border-primary border-t-transparent rounded-full animate-spin mx-auto mb-4" />
        <p class="text-[13px] text-muted-foreground">正在处理登录...</p>
      </template>
    </div>
  </div>
</template>
