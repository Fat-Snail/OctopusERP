<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { get } from '@/utils/http'
import type { ServerInfo } from '@/api/monitor/types'

const serverInfo = ref<ServerInfo>({
  cpu: { cpuNum: 4, used: 35, sys: 10, free: 55 },
  mem: { total: 16384, used: 8397, free: 7987 },
  jvm: { total: 512, used: 256, free: 256, version: '.NET 8.0.0' },
  sysFiles: [],
})

function formatMB(mb: number): string {
  return mb > 1024 ? `${(mb / 1024).toFixed(1)} GB` : `${mb} MB`
}

function progressColor(pct: number) {
  if (pct > 90) return 'bg-danger'
  if (pct > 70) return 'bg-warning'
  return 'bg-primary'
}

onMounted(async () => {
  try { serverInfo.value = await get<ServerInfo>('/monitor/server') } catch { /* use defaults */ }
})
</script>

<template>
  <div class="space-y-4">
    <div class="grid grid-cols-3 gap-4">
      <!-- CPU -->
      <div class="bg-card border border-border rounded-lg p-4">
        <div class="text-[13px] font-semibold text-foreground mb-3">CPU 信息</div>
        <div class="space-y-2">
          <div v-for="item in [
            { l: 'CPU核数', v: `${serverInfo.cpu.cpuNum} 核` },
            { l: '用户使用', v: `${serverInfo.cpu.used}%` },
            { l: '系统使用', v: `${serverInfo.cpu.sys}%` },
            { l: '空闲', v: `${serverInfo.cpu.free}%` },
          ]" :key="item.l" class="flex justify-between text-[12px]">
            <span class="text-muted-foreground">{{ item.l }}</span>
            <span class="text-foreground font-medium font-mono-tnum">{{ item.v }}</span>
          </div>
        </div>
        <div class="mt-3 bg-muted rounded-full h-2 overflow-hidden">
          <div
            :class="['h-full rounded-full transition-all', progressColor(serverInfo.cpu.used + serverInfo.cpu.sys)]"
            :style="{ width: `${serverInfo.cpu.used + serverInfo.cpu.sys}%` }"
          />
        </div>
      </div>
      <!-- Memory -->
      <div class="bg-card border border-border rounded-lg p-4">
        <div class="text-[13px] font-semibold text-foreground mb-3">内存信息</div>
        <div class="space-y-2">
          <div v-for="item in [
            { l: '总内存', v: formatMB(serverInfo.mem.total) },
            { l: '已用', v: formatMB(serverInfo.mem.used) },
            { l: '空闲', v: formatMB(serverInfo.mem.free) },
          ]" :key="item.l" class="flex justify-between text-[12px]">
            <span class="text-muted-foreground">{{ item.l }}</span>
            <span class="text-foreground font-medium font-mono-tnum">{{ item.v }}</span>
          </div>
        </div>
        <div class="mt-3 bg-muted rounded-full h-2 overflow-hidden">
          <div
            :class="['h-full rounded-full transition-all', progressColor(Math.round(serverInfo.mem.used / serverInfo.mem.total * 100))]"
            :style="{ width: `${Math.round(serverInfo.mem.used / serverInfo.mem.total * 100)}%` }"
          />
        </div>
      </div>
      <!-- JVM / Runtime -->
      <div class="bg-card border border-border rounded-lg p-4">
        <div class="text-[13px] font-semibold text-foreground mb-3">运行时信息</div>
        <div class="space-y-2">
          <div v-for="item in [
            { l: '总内存', v: `${serverInfo.jvm.total} MB` },
            { l: '已用', v: `${serverInfo.jvm.used} MB` },
            { l: '空闲', v: `${serverInfo.jvm.free} MB` },
            { l: '版本', v: serverInfo.jvm.version },
          ]" :key="item.l" class="flex justify-between text-[12px]">
            <span class="text-muted-foreground">{{ item.l }}</span>
            <span class="text-foreground font-medium">{{ item.v }}</span>
          </div>
        </div>
        <div class="mt-3 bg-muted rounded-full h-2 overflow-hidden">
          <div
            :class="['h-full rounded-full transition-all', progressColor(Math.round(serverInfo.jvm.used / serverInfo.jvm.total * 100))]"
            :style="{ width: `${Math.round(serverInfo.jvm.used / serverInfo.jvm.total * 100)}%` }"
          />
        </div>
      </div>
    </div>

    <!-- Disk -->
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <div class="px-4 py-3 border-b border-border">
        <span class="text-[13px] font-semibold text-foreground">磁盘信息</span>
      </div>
      <table class="w-full">
        <thead>
          <tr class="bg-subtle border-b border-border">
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">盘符</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">类型</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">文件系统</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">总大小</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">可用</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">已用</th>
            <th class="text-left text-[11px] uppercase tracking-wide text-muted-foreground px-4 py-2.5 font-medium">使用率</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!serverInfo.sysFiles.length">
            <td colspan="7" class="py-6 text-center text-[12px] text-muted-foreground">暂无磁盘数据</td>
          </tr>
          <tr v-else v-for="row in serverInfo.sysFiles" :key="row.dirName" class="border-b border-border last:border-0 hover:bg-subtle">
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground">{{ row.dirName }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.typeName }}</td>
            <td class="px-4 py-2 text-[12px] text-muted-foreground">{{ row.sysTypeName }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-foreground">{{ row.total }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-success">{{ row.free }}</td>
            <td class="px-4 py-2 text-[12px] font-mono-tnum text-warning">{{ row.used }}</td>
            <td class="px-4 py-2">
              <div class="flex items-center gap-2">
                <div class="flex-1 bg-muted rounded-full h-1.5 overflow-hidden">
                  <div :class="['h-full rounded-full', progressColor(row.usage)]" :style="{ width: `${row.usage}%` }" />
                </div>
                <span class="text-[11px] font-mono-tnum text-muted-foreground w-9 text-right">{{ row.usage }}%</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
