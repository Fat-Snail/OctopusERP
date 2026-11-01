<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { oidcService } from '@/services/oidcService'
import { useUserStore } from '@/store/user'

const router = useRouter()
const userStore = useUserStore()
const error = ref('')

onMounted(async () => {
  try {
    await oidcService.handleCallback()
    userStore.loadFromOidc()
    router.replace('/home')
  } catch (e) {
    error.value = (e as Error).message || '登录回调失败'
  }
})
</script>

<template>
  <div class="flex items-center justify-center h-dvh bg-background">
    <div v-if="error" class="text-center px-6">
      <p class="text-danger text-[15px] font-medium mb-2">登录失败</p>
      <p class="text-muted-foreground text-[13px] mb-4">{{ error }}</p>
      <button
        class="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-[14px] cursor-pointer border-0"
        @click="$router.replace('/login')"
      >
        重新登录
      </button>
    </div>
    <div v-else class="text-center">
      <div class="w-10 h-10 border-2 border-primary border-t-transparent rounded-full animate-spin mx-auto mb-3" />
      <p class="text-muted-foreground text-[14px]">正在登录...</p>
    </div>
  </div>
</template>
