<script setup lang="ts">
import '@/composables/useTheme' // 模块加载时立即读取 localStorage 并应用 .dark class
</script>

<template>
  <router-view />
</template>
