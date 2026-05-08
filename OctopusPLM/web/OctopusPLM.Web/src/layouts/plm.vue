<script setup lang="ts">
import AppShell from '@/components/app/AppShell.vue'
import PageHeader from '@/components/app/PageHeader.vue'
import { useRoute } from 'vue-router'
import { computed } from 'vue'

const route = useRoute()
const title = computed(() => route.meta['title'] as string | undefined)
</script>

<template>
  <AppShell>
    <div class="p-5">
      <PageHeader v-if="title" :title="title" class="mb-4" />
      <router-view />
    </div>
  </AppShell>
</template>
