<script setup lang="ts">
import AppShell from '@/components/app/AppShell.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const pageTitle = computed(() => route.meta['title'] as string | undefined)
</script>

<template>
  <div class="h-screen overflow-hidden">
    <AppShell>
      <div class="flex flex-col h-full">
        <PageHeader v-if="pageTitle" :title="pageTitle" />
        <div class="flex-1 overflow-y-auto p-5">
          <router-view />
        </div>
      </div>
    </AppShell>
  </div>
</template>
