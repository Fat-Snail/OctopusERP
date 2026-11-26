<script setup lang="ts">
import { ArrowRight } from '@lucide/vue'
import { Wallet } from '@lucide/vue'
import { oidcService } from '@/services/oidcService'
function handleSSOLogin() { oidcService.login() }
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-[oklch(0.96_0.03_250)] to-[oklch(0.97_0.02_220)]">
    <div class="w-[400px] bg-card border border-border rounded-[var(--radius-lg)] shadow-sm p-8">
      <div class="text-center mb-8">
        <div class="w-14 h-14 bg-primary rounded-xl flex items-center justify-center mx-auto mb-4">
          <component :is="Wallet" :size="28" class="text-primary-foreground" />
        </div>
        <h1 class="text-[24px] font-bold text-foreground tracking-[-0.01em]">OctopusFIN</h1>
        <p class="text-[13px] text-muted-foreground mt-1">财务管理</p>
      </div>
      <div class="h-px bg-border mb-6" />
      <button
        class="w-full flex items-center justify-center gap-2 h-10 bg-primary text-primary-foreground rounded-[var(--radius)] text-[13px] font-medium hover:brightness-105 transition-all cursor-pointer border-0"
        @click="handleSSOLogin"
      >
        <ArrowRight :size="16" />
        使用 OctopusUMC 账号登录（SSO）
      </button>
      <p class="text-center text-[11px] text-muted-foreground mt-4 leading-relaxed">
        通过 OctopusUMC 统一用户中心进行身份认证<br>无需单独注册账号
      </p>
    </div>
  </div>
</template>
