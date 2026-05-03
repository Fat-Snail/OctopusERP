<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { oidcService } from '@/services/oidcService'

const router = useRouter()

onMounted(async () => {
  await oidcService.handleCallback()
  router.replace('/home')
})
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-background">
    <div class="text-center">
      <div class="w-10 h-10 border-2 border-primary border-t-transparent rounded-full animate-spin mx-auto mb-4" />
      <p class="text-[13px] text-muted-foreground">正在处理登录...</p>
    </div>
  </div>
</template>
