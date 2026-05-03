<script setup lang="ts">
import { ref, onMounted } from 'vue'
import AppShell from '@/components/app/AppShell.vue'
import { useOaUserStore } from '@/store/modules/oaUser'
import { getPendingApprovals } from '@/api/approval'
import { getUnreadCount } from '@/api/notice'

const oaUserStore = useOaUserStore()
const pendingCount = ref(0)
const unreadCount = ref(0)

onMounted(async () => {
  await oaUserStore.fetchCurrentUser()

  try {
    const data = await getPendingApprovals()
    pendingCount.value = data.total
  } catch { /* ignore */ }

  try {
    unreadCount.value = await getUnreadCount()
  } catch { /* ignore */ }
})
</script>

<template>
  <AppShell :pending-count="pendingCount" :unread-count="unreadCount">
    <router-view />
  </AppShell>
</template>
