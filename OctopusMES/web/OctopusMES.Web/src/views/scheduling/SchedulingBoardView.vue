<script setup lang="ts">
import { computed, ref, onMounted, watch } from "vue";
import PageHeader from "@/components/app/PageHeader.vue";
import { createAuthedHttp } from "@/utils/http";
import {
  addLocalDays,
  buildDayWindow,
  buildSchedulingWindow,
  formatDateTitle,
  formatLocalTime,
  localDateValue,
} from "./schedulingTime";

type ScheduleSlot = {
  scheduleSlotId?: number;
  taskId: number;
  taskCode?: string;
  taskName?: string;
  resourceId: number;
  resourceCode?: string;
  resourceName?: string;
  resourceKind?: string;
  start: string;
  end: string;
  status?: string;
};
type UnscheduledTask = { taskId: number; code: string; message: string };
type Conflict = { code: string; message: string; taskId?: number; resourceId?: number };
type PlanPayload = {
  schedulePlanId: number;
  planCode: string;
  productionOrderId: number;
  productionOrderCode: string;
  status: "simulation" | "draft" | "published" | "frozen";
  strategy: string;
  publishedAt?: string;
  frozenUntil?: string;
  slots: ScheduleSlot[];
  unscheduled?: UnscheduledTask[];
  conflicts?: Conflict[];
};

const hours = Array.from({ length: 24 }, (_, i) => `${String(i).padStart(2, "0")}:00`);
const colors = ["bg-blue-500", "bg-indigo-500", "bg-emerald-500", "bg-cyan-500", "bg-violet-500"];
const selectedDate = ref(localDateValue());
const selected = ref<ScheduleSlot | null>(null);
const slots = ref<ScheduleSlot[]>([]);
const unscheduled = ref<UnscheduledTask[]>([]);
const conflicts = ref<Conflict[]>([]);
const currentOrder = ref<{ id: number; code: string; operationCount: number } | null>(null);
const currentPlan = ref<PlanPayload | null>(null);
const planLoading = ref(false);
const actionMessage = ref("请先创建生产订单，或选择日期查看已保存计划。");
const showUnscheduled = ref(false);
const scheduleHttp = createAuthedHttp("/api/scheduling");
const orderHttp = createAuthedHttp("/api/production-orders");
const productHttp = createAuthedHttp("/api/mes-products");

const planStatus = computed(() => currentPlan.value?.status ?? "none");
const scheduledCount = computed(() => slots.value.length);
const resources = computed(() => {
  const map = new Map<number, { id: number; code: string; name: string; kind: string; color: string }>();
  for (const slot of slots.value) {
    if (!map.has(slot.resourceId)) {
      map.set(slot.resourceId, {
        id: slot.resourceId,
        code: slot.resourceCode || `RES-${slot.resourceId}`,
        name: slot.resourceName || `资源 ${slot.resourceId}`,
        kind: slot.resourceKind || "resource",
        color: colors[map.size % colors.length],
      });
    }
  }
  return [...map.values()];
});
const dayTitle = computed(() => formatDateTitle(selectedDate.value));
const freezeDate = computed(() => addLocalDays(selectedDate.value, 1));

function apiError(error: unknown, fallback: string): string {
  const value = error as { response?: { data?: { msg?: string } }; message?: string };
  return value.response?.data?.msg || value.message || fallback;
}
function applyPlan(data: PlanPayload) {
  currentPlan.value = data;
  currentOrder.value = {
    id: data.productionOrderId,
    code: data.productionOrderCode,
    operationCount: data.slots.length,
  };
  slots.value = data.slots ?? [];
  unscheduled.value = data.unscheduled ?? [];
  conflicts.value = data.conflicts ?? [];
}
function applySimulation(data: { slots?: ScheduleSlot[]; unscheduled?: UnscheduledTask[]; conflicts?: Conflict[] }) {
  currentPlan.value = null;
  slots.value = data.slots ?? [];
  unscheduled.value = data.unscheduled ?? [];
  conflicts.value = data.conflicts ?? [];
}
async function loadPlansForDate() {
  planLoading.value = true;
  selected.value = null;
  try {
    const window = buildDayWindow(selectedDate.value);
    const response = await scheduleHttp.get("/plans", { params: window });
    if (response.data?.code !== 200) throw new Error(response.data?.msg || "排程计划加载失败");
    const plan = (response.data?.data ?? [])[0] as PlanPayload | undefined;
    if (plan) {
      applyPlan(plan);
      actionMessage.value = `已加载真实计划 ${plan.planCode}。`;
    } else {
      currentPlan.value = null;
      slots.value = [];
      unscheduled.value = [];
      conflicts.value = [];
      actionMessage.value = "该日期暂无真实排程计划。";
    }
  } catch (error) {
    actionMessage.value = apiError(error, "MES 排程服务不可用");
  } finally {
    planLoading.value = false;
  }
}
async function simulatePlan() {
  if (!currentOrder.value) {
    actionMessage.value = "请先创建生产订单。";
    return;
  }
  planLoading.value = true;
  try {
    const response = await scheduleHttp.post("/simulate", {
      productionOrderId: currentOrder.value.id,
      ...buildSchedulingWindow(selectedDate.value),
    });
    if (response.data?.code !== 200) throw new Error(response.data?.msg || "模拟排程失败");
    applySimulation(response.data.data);
    actionMessage.value = `模拟完成：已排 ${slots.value.length} 道，未排 ${unscheduled.value.length} 道；结果尚未发布。`;
  } catch (error) {
    actionMessage.value = apiError(error, "模拟排程失败");
  } finally {
    planLoading.value = false;
  }
}
async function publishPlan() {
  if (!currentOrder.value || slots.value.length === 0) return;
  planLoading.value = true;
  try {
    const created = await scheduleHttp.post("/plans", {
      productionOrderId: currentOrder.value.id,
      ...buildSchedulingWindow(selectedDate.value),
    });
    if (created.data?.code !== 200) throw new Error(created.data?.msg || "计划创建失败");
    const id = created.data.data.schedulePlanId as number;
    const published = await scheduleHttp.post(`/plans/${id}/publish`);
    if (published.data?.code !== 200) throw new Error(published.data?.msg || "计划发布失败");
    applyPlan(published.data.data);
    actionMessage.value = `计划 ${currentPlan.value?.planCode} 已发布并保存。`;
  } catch (error) {
    actionMessage.value = apiError(error, "计划发布失败");
  } finally {
    planLoading.value = false;
  }
}
async function freezePlan() {
  if (!currentPlan.value || currentPlan.value.status !== "published") return;
  planLoading.value = true;
  try {
    const until = buildDayWindow(freezeDate.value).from;
    const response = await scheduleHttp.post(`/plans/${currentPlan.value.schedulePlanId}/freeze`, null, { params: { until } });
    if (response.data?.code !== 200) throw new Error(response.data?.msg || "计划冻结失败");
    applyPlan(response.data.data);
    actionMessage.value = `计划已冻结至 ${freezeDate.value} 00:00。`;
  } catch (error) {
    actionMessage.value = apiError(error, "计划冻结失败");
  } finally {
    planLoading.value = false;
  }
}
function left(start: string) {
  const value = new Date(start);
  return ((value.getHours() * 60 + value.getMinutes()) / 1440) * 100;
}
function width(start: string, end: string) {
  const dayStart = new Date(`${selectedDate.value}T00:00:00`).getTime();
  const dayEnd = dayStart + 24 * 60 * 60 * 1000;
  const clippedStart = Math.max(new Date(start).getTime(), dayStart);
  const clippedEnd = Math.min(new Date(end).getTime(), dayEnd);
  return (Math.max(0, clippedEnd - clippedStart) / (dayEnd - dayStart)) * 100;
}
const products = ref<
  Array<{
    productId: number;
    manufacturingVersionId?: number;
    productCode: string;
    productName: string;
    version?: string;
    bomVersion?: string;
    routingVersion?: string;
    status: string;
    bomItemCount?: number;
    operationCount?: number;
  }>
>([]);
const plmAvailable = ref(false);
const catalogMessage = ref("正在连接 PLM…");
const productKeyword = ref("");
const productPage = ref(1);
const productTotal = ref(0);
const productHasMore = ref(false);
const productSearching = ref(false);
const productDropdownOpen = ref(false);
let searchTimer: ReturnType<typeof setTimeout> | undefined;
const orderForm = ref({
  code: "",
  manufacturingKey: "",
  qty: 100,
  dueDate: addLocalDays(localDateValue(), 7),
});
const selectedManufacturing = ref<(typeof products.value)[number] | null>(null);
const orderMessage = ref("");
const orderSaving = ref(false);
async function createOrder() {
  orderSaving.value = true;
  orderMessage.value = "";
  try {
    if (!plmAvailable.value || !orderForm.value.manufacturingKey)
      throw new Error("请先选择 PLM 已发布产品");
    const selected = selectedManufacturing.value;
    if (!selected) throw new Error("请选择 PLM 已发布制造版本");
    const synced = await productHttp.post(
      `/${selected.productId}/sync?manufacturingVersionId=${selected.manufacturingVersionId}`,
    );
    if (synced.data?.code !== 200)
      throw new Error(synced.data?.msg || "PLM 产品同步失败");
    const snapshotId = synced.data?.data?.manufacturingSnapshotId;
    if (!snapshotId) throw new Error("MES 未返回不可变制造快照，请在同步管理中查看错误");
    const created = await orderHttp.post("/from-snapshot", {
      productionOrderCode: orderForm.value.code,
      manufacturingSnapshotId: snapshotId,
      plannedQty: orderForm.value.qty,
      dueDate: orderForm.value.dueDate,
      priority: 0,
    });
    const id = created.data?.data?.productionOrderId;
    if (!id) throw new Error(created.data?.msg || "创建失败");
    const exploded = await orderHttp.post(`/${id}/explode`);
    if (exploded.data?.code !== 200) throw new Error(exploded.data?.msg || "工序展开失败");
    const operationCount = exploded.data?.data?.length ?? 0;
    currentOrder.value = { id, code: created.data.data.productionOrderCode, operationCount };
    currentPlan.value = null;
    slots.value = [];
    unscheduled.value = [];
    conflicts.value = [];
    orderMessage.value = `${created.data.msg}，已展开 ${operationCount} 道工序。`;
    actionMessage.value = `生产订单 ${currentOrder.value.code} 已就绪，请点击模拟排程。`;
  } catch (e) {
    orderMessage.value = apiError(e, "创建失败，请检查产品和工艺路线。");
  } finally {
    orderSaving.value = false;
  }
}
async function searchProducts(reset = true) {
  productSearching.value = true;
  try {
    const next = reset ? 1 : productPage.value + 1;
    const r = await productHttp.get("/catalog/search", {
      params: {
        keyword: productKeyword.value.trim(),
        page: next,
        pageSize: 20,
      },
    });
    const d = r.data?.data;
    plmAvailable.value = r.data?.code === 200 && d?.available === true;
    products.value = reset
      ? (d?.rows ?? [])
      : [...products.value, ...(d?.rows ?? [])];
    productPage.value = d?.page ?? next;
    productTotal.value = d?.total ?? 0;
    productHasMore.value = d?.hasMore ?? false;
    catalogMessage.value = plmAvailable.value
      ? `找到 ${productTotal.value} 个 PLM 商品`
      : (r.data?.msg ?? "PLM 商品搜索失败");
  } catch {
    products.value = [];
    plmAvailable.value = false;
    catalogMessage.value = "PLM 服务不可用，禁止创建生产订单";
  } finally {
    productSearching.value = false;
  }
}
function chooseProduct(p: (typeof products.value)[number]) {
  if (!p.manufacturingVersionId) {
    orderMessage.value = `${p.productName} 尚未在 PLM 发布制造版本，请先维护 BOM 和工艺路线。`;
    return;
  }
  selectedManufacturing.value = p;
  orderForm.value.manufacturingKey = `${p.productId}:${p.manufacturingVersionId}`;
  productDropdownOpen.value = false;
  productKeyword.value = `${p.productCode} · ${p.productName} · ${p.version}`;
}
watch(productKeyword, () => {
  if (!productDropdownOpen.value) return;
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => searchProducts(true), 350);
});
watch(selectedDate, loadPlansForDate);
onMounted(async () => {
  await Promise.all([searchProducts(true), loadPlansForDate()]);
});
</script>

<template>
  <div class="p-6 space-y-5">
    <PageHeader
      title="通用生产排程"
      description="离散制造 · 交期优先 · 模拟计划"
    />
    <div class="bg-card border border-border rounded-lg p-4">
      <div class="font-medium mb-3">
        新增生产订单
        <span
          class="text-xs font-normal"
          :class="plmAvailable ? 'text-emerald-700' : 'text-red-600'"
          >数据来源：PLM · {{ catalogMessage }}</span
        >
      </div>
      <div class="grid grid-cols-4 gap-3">
        <input
          v-model="orderForm.code"
          class="border border-border rounded px-3 py-2 text-sm"
          placeholder="订单号（可选）"
        />
        <div class="relative">
          <input
            v-model="productKeyword"
            :disabled="!plmAvailable && !productSearching"
            class="w-full border border-border rounded px-3 py-2 text-sm disabled:opacity-50"
            placeholder="输入商品编码或名称搜索"
            @focus="
              productDropdownOpen = true;
              searchProducts(true);
            "
          />
          <div
            v-if="productDropdownOpen"
            class="absolute z-30 mt-1 w-full max-h-80 overflow-y-auto rounded border border-border bg-card shadow-lg"
          >
            <div
              v-if="productSearching && !products.length"
              class="p-4 text-sm text-muted-foreground"
            >
              正在搜索 PLM…
            </div>
            <button
              v-for="p in products"
              :key="`${p.productId}:${p.manufacturingVersionId || 0}`"
              class="block w-full border-0 border-b border-border bg-transparent px-3 py-2 text-left hover:bg-muted"
              @mousedown.prevent="chooseProduct(p)"
            >
              <div class="flex justify-between text-sm font-medium">
                <span>{{ p.productCode }} · {{ p.productName }}</span>
                <span
                  :class="
                    p.manufacturingVersionId
                      ? 'text-emerald-700'
                      : 'text-amber-700'
                  "
                  >{{
                    p.manufacturingVersionId ? "可排产" : "未发布制造版本"
                  }}</span
                >
              </div>
              <div class="text-xs text-muted-foreground">
                <template v-if="p.manufacturingVersionId">
                  制造版本 {{ p.version }} · BOM {{ p.bomVersion }} · 路线
                  {{ p.routingVersion }}
                </template>
                <template v-else>
                  请先在 PLM「制造版本管理」维护 BOM、工艺并发布
                </template>
              </div></button
            ><button
              v-if="productHasMore"
              class="w-full p-2 text-sm text-primary"
              :disabled="productSearching"
              @mousedown.prevent="searchProducts(false)"
            >
              加载更多（已显示 {{ products.length }} / {{ productTotal }}）
            </button>
            <div
              v-if="!productSearching && !products.length"
              class="p-4 text-sm text-muted-foreground"
            >
              没有匹配的 PLM 商品
            </div>
          </div>
        </div>
        <input
          v-model.number="orderForm.qty"
          type="number"
          min="1"
          class="border border-border rounded px-3 py-2 text-sm"
          placeholder="计划数量"
        /><input
          v-model="orderForm.dueDate"
          type="date"
          class="border border-border rounded px-3 py-2 text-sm"
        />
      </div>
      <div
        v-if="selectedManufacturing"
        class="mt-3 rounded bg-muted px-3 py-2 text-xs"
      >
        制造版本：{{ selectedManufacturing.version }} · BOM：{{
          selectedManufacturing.bomVersion
        }}
        · 工艺路线：{{ selectedManufacturing.routingVersion }} · 来源：PLM
        · 物料 {{ selectedManufacturing.bomItemCount || 0 }} 项 · 工序 {{ selectedManufacturing.operationCount || 0 }} 道
      </div>
      <div class="flex items-center gap-3 mt-3">
        <button
          class="px-3 py-1.5 rounded bg-primary text-primary-foreground text-sm disabled:opacity-40"
          :disabled="
            orderSaving || !plmAvailable || !orderForm.manufacturingKey
          "
          @click="createOrder"
        >
          {{ orderSaving ? "创建中…" : "创建并展开工序" }}</button
        ><button
          class="px-3 py-1.5 rounded border border-border text-sm"
          @click="searchProducts(true)"
        >
          重新连接 PLM</button
        ><span class="text-xs text-muted-foreground">{{ orderMessage }}</span>
      </div>
    </div>
    <div
      class="flex items-center justify-between bg-card border border-border rounded-lg px-4 py-3"
    >
      <div class="flex items-center gap-2">
        <input
          v-model="selectedDate"
          type="date"
          class="rounded border border-border px-3 py-1.5 text-sm"
          aria-label="排程日期"
        />
        <button
          class="px-3 py-1.5 rounded bg-primary text-primary-foreground text-sm disabled:opacity-40"
          :disabled="planLoading || !currentOrder || planStatus === 'frozen'"
          @click="simulatePlan"
        >
          {{ planLoading ? "处理中…" : "模拟排程" }}</button
        ><button
          class="px-3 py-1.5 rounded border border-border text-sm disabled:opacity-40"
          :disabled="planLoading || !!currentPlan || !currentOrder || slots.length === 0"
          @click="publishPlan"
        >
          发布计划</button
        ><button
          class="px-3 py-1.5 rounded border border-border text-sm disabled:opacity-40"
          :disabled="planStatus !== 'published'"
          @click="freezePlan"
        >
          冻结至 {{ freezeDate }}
        </button>
      </div>
      <div class="text-xs text-muted-foreground text-right">
        <div>
          当前订单：{{ currentOrder?.code || "未选择" }} · 计划：{{
            currentPlan?.planCode || "未保存模拟"
          }}
        </div>
        <div
          class="mt-1"
          :class="
            planStatus === 'frozen' ? 'text-amber-700' : 'text-emerald-700'
          "
        >
          状态：{{
            planStatus === "none"
              ? slots.length
                ? "模拟结果"
                : "暂无计划"
              : planStatus === "published"
                ? "已发布"
                : planStatus === "frozen"
                  ? "已冻结"
                  : "模拟计划"
          }}
          · {{ actionMessage }}
        </div>
      </div>
    </div>
    <div class="grid grid-cols-4 gap-3">
      <div
        v-for="item in [
          { label: '已排程工序', value: scheduledCount },
          { label: '使用资源', value: resources.length },
          { label: '未排程', value: unscheduled.length },
          { label: '冲突', value: conflicts.length },
        ]"
        :key="item.label"
        class="bg-card border border-border rounded-lg p-4"
      >
        <div class="text-xs text-muted-foreground">{{ item.label }}</div>
        <div class="text-2xl font-semibold mt-1">{{ item.value }}</div>
      </div>
    </div>
    <div class="bg-card border border-border rounded-lg overflow-hidden">
      <div class="px-4 py-3 border-b border-border flex justify-between">
        <span class="font-medium">{{ dayTitle }} · 资源甘特图</span
        ><span class="text-xs text-muted-foreground">00:00 — 24:00</span>
      </div>
      <div v-if="resources.length" class="flex">
        <div class="w-56 shrink-0 border-r border-border">
          <div
            class="h-10 border-b border-border px-4 flex items-center text-xs text-muted-foreground"
          >
            资源 / 工作中心
          </div>
          <div
            v-for="r in resources"
            :key="r.id"
            class="h-20 border-b border-border px-4 flex flex-col justify-center"
          >
            <span class="text-sm">{{ r.name }} / {{ r.code }}</span
            ><span class="text-xs text-muted-foreground">{{ r.kind }}</span>
          </div>
        </div>
        <div class="flex-1 overflow-x-auto">
          <div class="min-w-[1536px]">
          <div class="h-10 border-b border-border flex">
            <div
              v-for="h in hours"
              :key="h"
              class="flex-1 min-w-[64px] text-[11px] text-muted-foreground px-1 pt-2 border-r border-dashed border-border"
            >
              {{ h }}
            </div>
          </div>
          <div
            v-for="r in resources"
            :key="r.id"
            class="h-20 border-b border-border relative bg-[linear-gradient(to_right,transparent_0,transparent_calc(8.333%-1px),hsl(var(--border))_8.333%,transparent_calc(8.333%+1px))]"
            @click="selected = null"
          >
            <div
              v-for="t in slots.filter((x) => x.resourceId === r.id)"
              :key="t.scheduleSlotId || t.taskId"
              class="absolute top-3 h-14 rounded px-2 py-1 text-white text-xs shadow cursor-pointer overflow-hidden"
              :class="r.color"
              :style="{
                left: `${left(t.start)}%`,
                width: `${width(t.start, t.end)}%`,
              }"
              @click.stop="selected = t"
            >
              <div class="font-medium">{{ t.taskName || t.taskCode }}</div>
              <div class="opacity-80">{{ currentOrder?.code }}</div>
              <div class="opacity-80">
                {{ formatLocalTime(t.start) }}–{{ formatLocalTime(t.end) }}
              </div>
            </div>
          </div>
          </div>
        </div>
      </div>
      <div v-else class="px-4 py-12 text-center text-sm text-muted-foreground">
        {{ planLoading ? "正在加载真实排程…" : "该日期暂无真实排程计划" }}
      </div>
    </div>
    <div v-if="selected" class="bg-card border border-border rounded-lg p-4">
      <div class="flex justify-between">
        <span class="font-medium">工序详情：{{ selected.taskName || selected.taskCode }}</span
        ><button class="text-xs text-muted-foreground" @click="selected = null">
          关闭
        </button>
      </div>
      <div class="grid grid-cols-4 gap-3 mt-3 text-sm">
        <span>订单：{{ currentOrder?.code }}</span
        ><span>资源：{{ selected.resourceName }} / {{ selected.resourceCode }}</span
        ><span>状态：{{ selected.status || "planned" }}</span
        ><span>时间：{{ formatLocalTime(selected.start) }}–{{ formatLocalTime(selected.end) }}</span>
      </div>
    </div>
    <div class="bg-card border border-amber-200 rounded-lg">
      <button
        class="w-full px-4 py-3 flex justify-between text-left"
        @click="showUnscheduled = !showUnscheduled"
      >
        <span class="font-medium text-amber-700"
          >未排程任务（{{ unscheduled.length }}）</span
        ><span class="text-xs text-muted-foreground">{{
          showUnscheduled ? "收起" : "展开"
        }}</span>
      </button>
      <div
        v-if="showUnscheduled"
        class="border-t border-amber-100 divide-y divide-border"
      >
        <div
          v-for="u in unscheduled"
          :key="u.taskId"
          class="px-4 py-3 text-sm flex justify-between"
        >
          <span>{{ currentOrder?.code }} · 工序任务 {{ u.taskId }} · {{ u.code }}</span
          ><span class="text-amber-700">{{ u.message }}</span>
        </div>
      </div>
    </div>
  </div>
</template>
