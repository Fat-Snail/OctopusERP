<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ArrowLeft, SendHorizontal } from '@lucide/vue'
import { portalApi } from '@/api/mock'
import type { ChatMessage } from '@/api/types'

const router = useRouter()
const messages = ref<ChatMessage[]>([])
const input = ref('')
const sending = ref(false)
const bottomRef = ref<HTMLDivElement | null>(null)

onMounted(async () => {
  messages.value = await portalApi.getChatHistory()
  await scrollToBottom()
})

async function scrollToBottom() {
  await nextTick()
  bottomRef.value?.scrollIntoView({ behavior: 'smooth' })
}

async function onSend() {
  const text = input.value.trim()
  if (!text || sending.value) return
  sending.value = true
  input.value = ''
  messages.value.push(await portalApi.sendChatMessage(text))
  await scrollToBottom()

  // Mock：模拟客服回复
  setTimeout(async () => {
    const now = new Date()
    const pad = (n: number) => String(n).padStart(2, '0')
    messages.value.push({
      id: `r${Date.now()}`,
      from: 'agent',
      text: '收到～您的专属客户经理会尽快跟进，也可以直接拨打电话联系：0571-8888-6666（Mock）',
      time: `${pad(now.getHours())}:${pad(now.getMinutes())}`,
    })
    await scrollToBottom()
    sending.value = false
  }, 600)
}
</script>

<template>
  <div class="h-dvh flex flex-col bg-subtle">
    <header class="flex-none bg-background px-5 pt-6 pb-3 border-b border-border flex items-center gap-3">
      <button class="border-0 bg-transparent cursor-pointer p-1 -ml-1" @click="router.back()">
        <ArrowLeft :size="20" />
      </button>
      <div>
        <h1 class="text-base font-semibold">在线客服</h1>
        <p class="text-[11px] text-muted-foreground">专属客户经理 · 小章</p>
      </div>
    </header>

    <div class="flex-1 overflow-y-auto px-4 py-4 space-y-3">
      <div
        v-for="m in messages"
        :key="m.id"
        :class="['flex', m.from === 'me' ? 'justify-end' : 'justify-start']"
      >
        <div class="max-w-[78%]">
          <div :class="[
            'px-3.5 py-2.5 rounded-2xl text-sm leading-relaxed',
            m.from === 'me' ? 'bg-primary text-primary-foreground rounded-br-md' : 'bg-card rounded-bl-md shadow-sm',
          ]">
            {{ m.text }}
          </div>
          <p :class="['text-[10px] text-muted-foreground mt-1', m.from === 'me' ? 'text-right' : '']">{{ m.time }}</p>
        </div>
      </div>
      <div ref="bottomRef" />
    </div>

    <div class="flex-none bg-background border-t border-border p-3 safe-area-inset-bottom">
      <div class="flex gap-2">
        <input
          v-model="input"
          type="text"
          placeholder="输入消息…"
          class="flex-1 h-11 rounded-xl border border-border bg-background px-3.5 text-sm outline-none focus:border-primary"
          @keyup.enter="onSend"
        />
        <button
          :disabled="!input.trim() || sending"
          class="w-11 h-11 rounded-xl bg-primary text-primary-foreground flex items-center justify-center border-0 cursor-pointer active:opacity-80 disabled:opacity-40"
          @click="onSend"
        >
          <SendHorizontal :size="18" />
        </button>
      </div>
    </div>
  </div>
</template>
