<script setup lang="ts">
import { ref, onMounted, onUnmounted, h, computed, watch } from "vue";
import { useRouter } from "vue-router";
import {
  useTable,
  coreFeatures,
  columnVisibilityFeature,
  columnSizingFeature,
  createColumnHelper,
  FlexRender
} from "@tanstack/vue-table";
import type { ColumnVisibilityState } from "@tanstack/vue-table";

import {
  Plus,
  Pencil,
  Trash2,
  SendHorizontal,
  ScanSearch,
  CheckCircle2,
  XCircle,
  Upload,
  ArrowDownToLine,
  FileText,
  Loader2,
  Database,
  X,
  Settings2,
  ChevronDown,
  ChevronUp,
  ImageOff
} from "@lucide/vue";
import ButtonVue from "@/components/ui/button.vue";
import InputVue from "@/components/ui/input.vue";
import BadgeVue from "@/components/ui/badge.vue";
import { getCategoryTree } from "@/api/category";
import type { CategoryTreeNode } from "@/api/category/types";
import {
  getProductList,
  deleteProduct,
  submitProduct,
  approveProduct,
  rejectProduct,
  publishProduct,
  discontinueProduct,
  getProductStats,
  importFrom1688
} from "@/api/product";
import type {
  ProductListRow,
  ProductStatsResponse,
  Import1688Result
} from "@/api/product/types";

const features = {
  ...coreFeatures,
  columnVisibilityFeature,
  columnSizingFeature
};

const router = useRouter();
const loading = ref(false);
const list = ref<ProductListRow[]>([]);
const page = ref(1);
const size = ref(20);
const total = ref(0);
const stats = ref<ProductStatsResponse | null>(null);

// ── Search state ──────────────────────────────────────────────────────────────
const keyword = ref("");
const statusFilter = ref("");
const categoryFilter = ref<number | "">("");
const minPriceFilter = ref<number | "">("");
const maxPriceFilter = ref<number | "">("");
const createdFromFilter = ref("");
const createdToFilter = ref("");
const advancedOpen = ref(false);

// ── Category tree (for filter) ────────────────────────────────────────────────
const categoryTree = ref<CategoryTreeNode[]>([]);

function flattenCats(nodes: CategoryTreeNode[]): CategoryTreeNode[] {
  const result: CategoryTreeNode[] = [];
  function walk(list: CategoryTreeNode[]) {
    for (const n of list) {
      result.push(n);
      if (n.children?.length) walk(n.children);
    }
  }
  walk(nodes);
  return result;
}
const flatCats = computed(() => flattenCats(categoryTree.value));

// ── Column customization ──────────────────────────────────────────────────────
const STORAGE_KEY = "plm-product-list-cols-v2";

interface ColDef {
  key: string;
  label: string;
  defaultOn: boolean;
  pinned?: boolean;
}
const ALL_COL_DEFS: ColDef[] = [
  { key: "mainImage", label: "商品图片", defaultOn: false },
  { key: "categoryName", label: "类目", defaultOn: true },
  { key: "skuCount", label: "SKU数", defaultOn: true },
  { key: "price", label: "价格区间", defaultOn: true },
  { key: "totalStock", label: "库存", defaultOn: true },
  { key: "createdByName", label: "创建人", defaultOn: true },
  { key: "sourceChannel", label: "来源渠道", defaultOn: false },
  { key: "createdAt", label: "创建时间", defaultOn: false },
  { key: "updatedAt", label: "更新时间", defaultOn: true }
];

function loadColVisibility(): ColumnVisibilityState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) return JSON.parse(raw) as ColumnVisibilityState;
  } catch {
    /* ignore */
  }
  return Object.fromEntries(ALL_COL_DEFS.map(c => [c.key, c.defaultOn]));
}

const colVisibility = ref<ColumnVisibilityState>(loadColVisibility());
const colPickerOpen = ref(false);

function toggleCol(key: string) {
  colVisibility.value = {
    ...colVisibility.value,
    [key]: !colVisibility.value[key]
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(colVisibility.value));
}

function resetCols() {
  colVisibility.value = Object.fromEntries(
    ALL_COL_DEFS.map(c => [c.key, c.defaultOn])
  );
  localStorage.setItem(STORAGE_KEY, JSON.stringify(colVisibility.value));
}

// Close picker when clicking outside
function onDocClick(e: MouseEvent) {
  const target = e.target as Element;
  if (colPickerOpen.value && !target.closest("[data-col-picker]")) {
    colPickerOpen.value = false;
  }
}

// ── Review dialog ─────────────────────────────────────────────────────────────
const reviewDialog = ref<{
  visible: boolean;
  type: "approve" | "reject" | "discontinue" | null;
  productId: number;
  comment: string;
  loading: boolean;
}>({ visible: false, type: null, productId: 0, comment: "", loading: false });

function openReviewDialog(
  type: "approve" | "reject" | "discontinue",
  id: number
) {
  reviewDialog.value = {
    visible: true,
    type,
    productId: id,
    comment: "",
    loading: false
  };
}

async function submitReview() {
  const d = reviewDialog.value;
  d.loading = true;
  try {
    if (d.type === "approve")
      await approveProduct(d.productId, { comment: d.comment || undefined });
    else if (d.type === "reject")
      await rejectProduct(d.productId, { comment: d.comment });
    else if (d.type === "discontinue")
      await discontinueProduct(d.productId, {
        comment: d.comment || undefined
      });
    d.visible = false;
    await Promise.all([loadData(), loadStats()]);
  } catch (e: unknown) {
    console.error("操作失败:", (e as Error).message);
  } finally {
    d.loading = false;
  }
}

// ── Status config ─────────────────────────────────────────────────────────────
const statusVariant: Record<
  string,
  "default" | "info" | "warning" | "success" | "danger"
> = {
  draft: "info",
  pending_review: "warning",
  approved: "success",
  rejected: "danger",
  active: "success",
  discontinued: "info"
};

const statusOptions = [
  { value: "", label: "全部状态" },
  { value: "draft", label: "草稿" },
  { value: "pending_review", label: "待审核" },
  { value: "approved", label: "已通过" },
  { value: "rejected", label: "已驳回" },
  { value: "active", label: "已上架" },
  { value: "discontinued", label: "已下架" }
];

const sourceChannelLabel: Record<string, string> = {
  "1688": "1688",
  taobao: "淘宝",
  tmall: "天猫"
};

// ── Columns ───────────────────────────────────────────────────────────────────
const columnHelper = createColumnHelper<typeof features, ProductListRow>();

const columns = columnHelper.columns([
  columnHelper.accessor("productName", {
    id: "productName",
    header: "商品名称",
    cell: info =>
      h(
        "span",
        { class: "text-[12px] font-medium text-foreground" },
        info.getValue()
      ),
    size: 220,
    enableHiding: false
  }),
  columnHelper.accessor("mainImage", {
    id: "mainImage",
    header: "图片",
    cell: info => {
      const url = info.getValue();
      if (!url)
        return h(
          "div",
          {
            class: "w-9 h-9 rounded bg-muted flex items-center justify-center"
          },
          h(ImageOff, { size: 14, class: "text-muted-foreground" })
        );
      return h("img", {
        src: url,
        alt: "",
        class: "w-9 h-9 object-cover rounded border border-border",
        onError: (e: Event) => {
          (e.target as HTMLImageElement).style.display = "none";
        }
      });
    },
    size: 64
  }),
  columnHelper.accessor("categoryName", {
    id: "categoryName",
    header: "类目",
    cell: info =>
      h(
        "span",
        { class: "text-[12px] text-muted-foreground" },
        info.getValue()
      ),
    size: 120
  }),
  columnHelper.accessor("skuCount", {
    id: "skuCount",
    header: "SKU数",
    cell: info =>
      h(
        "span",
        { class: "font-mono-tnum text-[12px] text-right block" },
        String(info.getValue())
      ),
    size: 70
  }),
  columnHelper.display({
    id: "price",
    header: "价格区间",
    cell: ({ row }) => {
      const r = row.original;
      if (r.minPrice > 0) {
        return h(
          "span",
          { class: "font-mono-tnum text-[12px]" },
          `¥${r.minPrice} ~ ¥${r.maxPrice}`
        );
      }
      return h(
        "span",
        { class: "text-[12px] text-muted-foreground" },
        "未设置"
      );
    },
    size: 160
  }),
  columnHelper.accessor("totalStock", {
    id: "totalStock",
    header: "库存",
    cell: info =>
      h(
        "span",
        { class: "font-mono-tnum text-[12px] text-right block" },
        String(info.getValue())
      ),
    size: 80
  }),
  columnHelper.accessor("createdByName", {
    id: "createdByName",
    header: "创建人",
    cell: info =>
      h(
        "span",
        { class: "text-[12px] text-muted-foreground" },
        info.getValue() || "-"
      ),
    size: 90
  }),
  columnHelper.accessor("sourceChannel", {
    id: "sourceChannel",
    header: "来源渠道",
    cell: info => {
      const ch = info.getValue();
      if (!ch)
        return h(
          "span",
          { class: "text-[12px] text-muted-foreground" },
          "手动录入"
        );
      return h(
        BadgeVue,
        { variant: "info", class: "text-[10px]" },
        () => sourceChannelLabel[ch] ?? ch
      );
    },
    size: 90
  }),
  columnHelper.accessor("createdAt", {
    id: "createdAt",
    header: "创建时间",
    cell: info =>
      h(
        "span",
        { class: "text-[11px] text-muted-foreground" },
        formatDate(info.getValue())
      ),
    size: 160
  }),
  columnHelper.accessor("updatedAt", {
    id: "updatedAt",
    header: "更新时间",
    cell: info =>
      h(
        "span",
        { class: "text-[11px] text-muted-foreground" },
        formatDate(info.getValue())
      ),
    size: 160
  }),
  columnHelper.accessor("status", {
    id: "status",
    header: "状态",
    cell: ({ row }) => {
      const r = row.original;
      const variant = statusVariant[r.status] ?? "info";
      return h(BadgeVue, { variant }, () => r.statusLabel);
    },
    size: 90,
    enableHiding: false
  }),
  columnHelper.display({
    id: "actions",
    header: "操作",
    cell: ({ row }) => {
      const r = row.original;
      const btns = [];

      if (r.status === "draft" || r.status === "rejected") {
        btns.push(
          h(
            "button",
            {
              class:
                "text-primary text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                router.push(`/plm/product/edit/${r.productId}`);
              }
            },
            [h(Pencil, { size: 12, class: "inline mr-0.5" }), "编辑"]
          )
        );
      }

      if (r.status === "draft" || r.status === "rejected") {
        btns.push(
          h(
            "button",
            {
              class:
                "text-[color:var(--info)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                handleSubmit(r.productId);
              }
            },
            [h(SendHorizontal, { size: 12, class: "inline mr-0.5" }), "提交"]
          )
        );
      }

      if (r.status === "pending_review") {
        btns.push(
          h(
            "button",
            {
              class:
                "text-[color:var(--success)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                openReviewDialog("approve", r.productId);
              }
            },
            [h(CheckCircle2, { size: 12, class: "inline mr-0.5" }), "通过"]
          )
        );
        btns.push(
          h(
            "button",
            {
              class:
                "text-[color:var(--danger)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                openReviewDialog("reject", r.productId);
              }
            },
            [h(XCircle, { size: 12, class: "inline mr-0.5" }), "驳回"]
          )
        );
      }

      if (r.status === "approved") {
        btns.push(
          h(
            "button",
            {
              class:
                "text-[color:var(--success)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                handlePublish(r.productId);
              }
            },
            [h(Upload, { size: 12, class: "inline mr-0.5" }), "上架"]
          )
        );
      }

      if (r.status === "active") {
        btns.push(
          h(
            "button",
            {
              class:
                "text-[color:var(--warning)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0 mr-1.5",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                openReviewDialog("discontinue", r.productId);
              }
            },
            [h(ArrowDownToLine, { size: 12, class: "inline mr-0.5" }), "下架"]
          )
        );
      }

      if (r.status === "draft") {
        btns.push(
          h(
            "button",
            {
              class:
                "text-[color:var(--danger)] text-[11px] hover:underline cursor-pointer border-0 bg-transparent p-0",
              onClick: (e: MouseEvent) => {
                e.stopPropagation();
                handleDelete(r.productId);
              }
            },
            [h(Trash2, { size: 12, class: "inline mr-0.5" }), "删除"]
          )
        );
      }

      return h("div", { class: "flex items-center flex-wrap gap-0.5" }, btns);
    },
    size: 200,
    enableHiding: false
  })
]);

const table = useTable({
  features,
  get data() {
    return list.value;
  },
  columns,
  state: {
    get columnVisibility() {
      return colVisibility.value;
    }
  },
  onColumnVisibilityChange: updater => {
    colVisibility.value =
      typeof updater === "function" ? updater(colVisibility.value) : updater;
    localStorage.setItem(STORAGE_KEY, JSON.stringify(colVisibility.value));
  }
});

// ── Helpers ───────────────────────────────────────────────────────────────────
function formatDate(val: string) {
  if (!val) return "-";
  const d = new Date(val);
  if (Number.isNaN(d.getTime())) return val;
  return d.toLocaleString("zh-CN", {
    timeZone: "Asia/Shanghai",
    hour12: false
  });
}

async function loadStats() {
  try {
    stats.value = await getProductStats();
  } catch {
    /* ignore */
  }
}

async function loadData() {
  loading.value = true;
  try {
    const res = await getProductList({
      page: page.value,
      size: size.value,
      keyword: keyword.value || undefined,
      status: statusFilter.value || undefined,
      categoryId: categoryFilter.value || undefined,
      minPrice:
        minPriceFilter.value !== "" ? Number(minPriceFilter.value) : undefined,
      maxPrice:
        maxPriceFilter.value !== "" ? Number(maxPriceFilter.value) : undefined,
      createdFrom: createdFromFilter.value || undefined,
      createdTo: createdToFilter.value || undefined
    });
    list.value = res.rows;
    total.value = res.total;
  } catch (e: unknown) {
    console.error("加载商品列表失败:", (e as Error).message);
  } finally {
    loading.value = false;
  }
}

function search() {
  page.value = 1;
  loadData();
}

function reset() {
  keyword.value = "";
  statusFilter.value = "";
  categoryFilter.value = "";
  minPriceFilter.value = "";
  maxPriceFilter.value = "";
  createdFromFilter.value = "";
  createdToFilter.value = "";
  page.value = 1;
  loadData();
}

async function handleSubmit(id: number) {
  try {
    await submitProduct(id);
    loadData();
    loadStats();
  } catch (e: unknown) {
    console.error("提交失败:", (e as Error).message);
  }
}

async function handlePublish(id: number) {
  if (!confirm("确定上架该商品？")) return;
  try {
    await publishProduct(id);
    await Promise.all([loadData(), loadStats()]);
  } catch (e: unknown) {
    console.error("上架失败:", (e as Error).message);
  }
}

async function handleDelete(id: number) {
  if (!confirm("确定删除该商品？")) return;
  try {
    await deleteProduct(id);
    await Promise.all([loadData(), loadStats()]);
  } catch (e: unknown) {
    console.error("删除失败:", (e as Error).message);
  }
}

const totalPages = computed(() => Math.ceil(total.value / size.value));

const statsCards = computed(() => {
  const s = stats.value;
  if (!s) return [];
  return [
    { label: "全部", value: s.total, filter: "" },
    { label: "草稿", value: s.draft, filter: "draft" },
    { label: "待审核", value: s.pendingReview, filter: "pending_review" },
    { label: "已通过", value: s.approved, filter: "approved" },
    { label: "已驳回", value: s.rejected, filter: "rejected" },
    { label: "已上架", value: s.active, filter: "active" },
    { label: "已下架", value: s.discontinued, filter: "discontinued" }
  ];
});

const reviewDialogTitle = computed(() => {
  const t = reviewDialog.value.type;
  if (t === "approve") return "审核通过";
  if (t === "reject") return "驳回审核";
  if (t === "discontinue") return "下架商品";
  return "";
});

// ── 1688 import dialog ────────────────────────────────────────────────────────
const importDialog = ref(false);
const importInput = ref("");
const importParsed = ref<string[]>([]);
const importParseError = ref("");
const importLoading = ref(false);
const importResult = ref<Import1688Result | null>(null);

function parseImportInput() {
  importParseError.value = "";
  importParsed.value = [];
  const raw = importInput.value.trim();
  if (!raw) {
    importParseError.value = "请粘贴 JSON 内容";
    return;
  }
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      importParsed.value = parsed.map(item => JSON.stringify(item));
    } else {
      importParsed.value = [raw];
    }
  } catch {
    const lines = raw
      .split("\n")
      .map(l => l.trim())
      .filter(Boolean);
    const valid: string[] = [];
    for (const line of lines) {
      try {
        JSON.parse(line);
        valid.push(line);
      } catch {
        /* skip */
      }
    }
    if (valid.length === 0) {
      importParseError.value = "JSON 格式有误，无法解析";
      return;
    }
    importParsed.value = valid;
  }
}

function onImportFileChange(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    importInput.value = reader.result as string;
    parseImportInput();
  };
  reader.readAsText(file);
}

async function doImport() {
  parseImportInput();
  if (importParsed.value.length === 0) return;
  importLoading.value = true;
  importResult.value = null;
  try {
    importResult.value = await importFrom1688(importParsed.value);
    await Promise.all([loadData(), loadStats()]);
  } catch (e: unknown) {
    importParseError.value = (e as Error).message;
  } finally {
    importLoading.value = false;
  }
}

function openImportDialog() {
  importDialog.value = true;
  importInput.value = "";
  importParsed.value = [];
  importParseError.value = "";
  importLoading.value = false;
  importResult.value = null;
}

// ── Vector scan ───────────────────────────────────────────────────────────────
interface ScanStatus {
  state: "idle" | "scanning" | "done" | "error";
  total: number;
  processed: number;
  success: number;
  fail: number;
  error: string | null;
}

const scanVisible = ref(false);
const scanStatus = ref<ScanStatus>({
  state: "idle",
  total: 0,
  processed: 0,
  success: 0,
  fail: 0,
  error: null
});
let scanEs: EventSource | null = null;

function closeScanPanel() {
  scanEs?.close();
  scanEs = null;
  scanVisible.value = false;
}

function startScanSSE() {
  scanEs?.close();
  scanEs = new EventSource("/api/vector/scan/stream");
  scanEs.onmessage = (e: MessageEvent) => {
    const data = JSON.parse(e.data as string) as ScanStatus;
    scanStatus.value = data;
    if (data.state === "done" || data.state === "error") {
      scanEs?.close();
      scanEs = null;
    }
  };
  scanEs.onerror = () => {
    scanEs?.close();
    scanEs = null;
  };
}

async function handleScanAll() {
  scanStatus.value = {
    state: "idle",
    total: 0,
    processed: 0,
    success: 0,
    fail: 0,
    error: null
  };
  scanVisible.value = true;

  try {
    const modelRes = await fetch("/api/vector/model/status");
    const modelData = (await modelRes.json()) as { modelLoaded: boolean };
    if (!modelData.modelLoaded) {
      scanStatus.value = {
        ...scanStatus.value,
        state: "error",
        error: "CLIP 模型未加载，请先前往「系统 → 模型管理」下载模型"
      };
      return;
    }
  } catch {
    /* ignore */
  }

  try {
    const res = await fetch("/api/vector/scan/start", { method: "POST" });
    const body = (await res.json()) as {
      code?: number;
      Code?: number;
      msg?: string;
      Msg?: string;
    };
    const code = body.code ?? body.Code;
    const msg = body.msg ?? body.Msg;
    if (code !== 200 && code !== 409) {
      scanStatus.value = {
        ...scanStatus.value,
        state: "error",
        error: msg ?? "启动扫描失败"
      };
      return;
    }
  } catch (e: unknown) {
    scanStatus.value = {
      ...scanStatus.value,
      state: "error",
      error: (e as Error).message || "网络错误"
    };
    return;
  }

  startScanSSE();
}

// ── Lifecycle ─────────────────────────────────────────────────────────────────
onMounted(async () => {
  document.addEventListener("click", onDocClick);
  loadData();
  loadStats();
  try {
    categoryTree.value = await getCategoryTree();
  } catch {
    /* ignore */
  }
});

onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  scanEs?.close();
});
</script>

<template>
  <div class="space-y-3">
    <!-- Stats bar -->
    <div v-if="stats" class="flex items-center gap-2 flex-wrap">
      <button
        v-for="card in statsCards"
        :key="card.filter"
        class="flex items-center gap-1.5 px-3 py-1.5 rounded-[var(--radius)] border text-[12px] transition-colors"
        :class="
          statusFilter === card.filter
            ? 'bg-primary text-primary-foreground border-primary'
            : 'bg-card border-border text-muted-foreground hover:border-primary hover:text-foreground'
        "
        @click="
          statusFilter = card.filter;
          search();
        "
      >
        <span class="font-mono-tnum font-semibold text-[13px]">{{
          card.value
        }}</span>
        <span>{{ card.label }}</span>
      </button>
    </div>

    <!-- Search bar -->
    <div
      class="bg-card border border-border rounded-[var(--radius-lg)] p-3 space-y-2"
    >
      <!-- Basic row -->
      <div class="flex items-center gap-2 flex-wrap">
        <InputVue
          v-model="keyword"
          placeholder="搜索商品名称..."
          class="w-[200px]"
          @keyup.enter="search"
        />
        <select
          v-model="categoryFilter"
          class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          @change="search"
        >
          <option value="">全部类目</option>
          <option
            v-for="cat in flatCats"
            :key="cat.categoryId"
            :value="cat.categoryId"
            :style="{ paddingLeft: cat.level * 12 + 'px' }"
          >
            {{ " ".repeat(cat.level * 2) }}{{ cat.name }}
          </option>
        </select>
        <select
          v-model="statusFilter"
          class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-card px-3 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          @change="search"
        >
          <option
            v-for="opt in statusOptions"
            :key="opt.value"
            :value="opt.value"
          >
            {{ opt.label }}
          </option>
        </select>
        <ButtonVue variant="outline" size="sm" @click="search">搜索</ButtonVue>
        <ButtonVue variant="ghost" size="sm" @click="reset">重置</ButtonVue>
        <button
          class="ml-auto flex items-center gap-1 text-[12px] text-muted-foreground hover:text-foreground transition-colors cursor-pointer border-0 bg-transparent p-0"
          @click="advancedOpen = !advancedOpen"
        >
          {{ advancedOpen ? "收起" : "高级筛选" }}
          <ChevronUp v-if="advancedOpen" :size="13" />
          <ChevronDown v-else :size="13" />
        </button>
      </div>

      <!-- Advanced filters -->
      <div
        v-if="advancedOpen"
        class="flex items-center gap-4 flex-wrap pt-1 border-t border-border"
      >
        <div class="flex items-center gap-2">
          <span class="text-[12px] text-muted-foreground whitespace-nowrap"
            >价格区间</span
          >
          <input
            v-model="minPriceFilter"
            type="number"
            placeholder="最低价"
            class="w-[90px] h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-2 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          <span class="text-[12px] text-muted-foreground">~</span>
          <input
            v-model="maxPriceFilter"
            type="number"
            placeholder="最高价"
            class="w-[90px] h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-2 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-[12px] text-muted-foreground whitespace-nowrap"
            >创建时间</span
          >
          <input
            v-model="createdFromFilter"
            type="date"
            class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-2 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
          <span class="text-[12px] text-muted-foreground">~</span>
          <input
            v-model="createdToFilter"
            type="date"
            class="h-[var(--row-h)] rounded-[var(--radius)] border border-input bg-background px-2 text-[12px] text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          />
        </div>
        <ButtonVue variant="outline" size="sm" @click="search">应用</ButtonVue>
      </div>
    </div>

    <!-- Toolbar -->
    <div class="flex items-center gap-2 flex-wrap">
      <ButtonVue
        variant="outline"
        size="sm"
        @click="router.push('/plm/product/image-search')"
      >
        <ScanSearch :size="13" />
        以图搜商品
      </ButtonVue>
      <ButtonVue variant="outline" size="sm" @click="handleScanAll">
        <Database :size="13" />
        一键向量化
      </ButtonVue>

      <!-- Column picker -->
      <div class="relative" data-col-picker>
        <ButtonVue
          variant="outline"
          size="sm"
          :class="colPickerOpen ? 'border-primary text-primary' : ''"
          @click.stop="colPickerOpen = !colPickerOpen"
        >
          <Settings2 :size="13" />
          列设置
        </ButtonVue>
        <div
          v-if="colPickerOpen"
          class="absolute left-0 top-full mt-1 z-30 w-[180px] bg-card border border-border rounded-[var(--radius-lg)] shadow-lg p-2"
          @click.stop
        >
          <div class="text-[11px] text-muted-foreground px-2 py-1 mb-1">
            显示列
          </div>
          <label
            v-for="col in ALL_COL_DEFS"
            :key="col.key"
            class="flex items-center gap-2 px-2 py-1.5 rounded-[var(--radius)] hover:bg-subtle cursor-pointer text-[12px]"
          >
            <input
              type="checkbox"
              :checked="!!colVisibility[col.key]"
              class="w-3.5 h-3.5 accent-primary"
              @change="toggleCol(col.key)"
            />
            {{ col.label }}
          </label>
          <div class="border-t border-border mt-1 pt-1">
            <button
              class="w-full text-left px-2 py-1 text-[11px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent"
              @click="resetCols"
            >
              恢复默认
            </button>
          </div>
        </div>
      </div>

      <ButtonVue variant="outline" size="sm" @click="openImportDialog">
        <FileText :size="13" />
        1688导入
      </ButtonVue>
      <ButtonVue size="sm" @click="router.push('/plm/product/create')">
        <Plus :size="13" />
        新建物料
      </ButtonVue>
      <ButtonVue
        variant="outline"
        size="sm"
        @click="router.push('/plm/category/manage')"
      >
        类目管理
      </ButtonVue>
      <ButtonVue
        variant="outline"
        size="sm"
        @click="router.push('/plm/category-model')"
      >
        类目模型
      </ButtonVue>
      <span class="ml-auto text-[12px] text-muted-foreground"
        >共 {{ total }} 条</span
      >
    </div>

    <!-- Table -->
    <div
      class="border border-border rounded-[var(--radius-lg)] overflow-hidden bg-card"
    >
      <div
        v-if="loading"
        class="h-40 flex items-center justify-center text-[12px] text-muted-foreground"
      >
        加载中...
      </div>
      <table v-else class="w-full text-[12px]">
        <thead>
          <tr
            v-for="headerGroup in table.getHeaderGroups()"
            :key="headerGroup.id"
          >
            <th
              v-for="header in headerGroup.headers"
              :key="header.id"
              class="text-left text-[11px] uppercase tracking-[0.04em] text-muted-foreground bg-subtle px-3 py-2 font-medium border-b border-border"
              :style="{ width: header.getSize() + 'px' }"
            >
              <FlexRender
                v-if="!header.isPlaceholder"
                :render="header.column.columnDef.header"
                :props="header.getContext()"
              />
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="table.getRowModel().rows.length === 0">
            <td
              :colspan="table.getVisibleFlatColumns().length"
              class="text-center py-10 text-muted-foreground text-[12px]"
            >
              暂无数据
            </td>
          </tr>
          <tr
            v-for="row in table.getRowModel().rows"
            :key="row.id"
            class="border-b border-border last:border-0 hover:bg-subtle transition-colors cursor-pointer"
            @click="
              router.push(`/plm/product/detail/${row.original.productId}`)
            "
          >
            <td
              v-for="cell in row.getVisibleCells()"
              :key="cell.id"
              class="px-3 py-[7px] h-[var(--row-h)]"
            >
              <FlexRender
                :render="cell.column.columnDef.cell"
                :props="cell.getContext()"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div
      class="flex items-center justify-end text-[12px] text-muted-foreground gap-1"
    >
      <ButtonVue
        variant="outline"
        size="sm"
        :disabled="page <= 1"
        @click="page > 1 && (page--, loadData())"
      >
        上一页
      </ButtonVue>
      <span class="px-2 tabular-nums">{{ page }} / {{ totalPages || 1 }}</span>
      <ButtonVue
        variant="outline"
        size="sm"
        :disabled="page >= totalPages"
        @click="page < totalPages && (page++, loadData())"
      >
        下一页
      </ButtonVue>
    </div>

    <!-- 1688 Import dialog -->
    <Teleport to="body">
      <div
        v-if="importDialog"
        class="fixed inset-0 z-50 flex items-center justify-center"
        @click.self="importDialog = false"
      >
        <div class="absolute inset-0 bg-black/50" />
        <div
          class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[600px] max-h-[80vh] flex flex-col"
        >
          <div
            class="px-6 py-4 border-b border-border flex items-center justify-between shrink-0"
          >
            <h3 class="text-[14px] font-semibold flex items-center gap-2">
              <FileText :size="16" class="text-primary" />
              从1688导入商品
            </h3>
            <button
              class="text-muted-foreground hover:text-foreground transition-colors cursor-pointer border-0 bg-transparent p-0"
              @click="importDialog = false"
            >
              ✕
            </button>
          </div>

          <div class="flex-1 overflow-y-auto px-6 py-4 space-y-4">
            <div v-if="!importResult" class="space-y-4">
              <p class="text-[12px] text-muted-foreground leading-relaxed">
                粘贴1688商品 API 返回的
                JSON，支持单个对象、对象数组或每行一条（JSONL）。已导入过的商品会自动跳过。
              </p>
              <textarea
                v-model="importInput"
                class="w-full h-[200px] rounded-[var(--radius)] border border-input bg-background px-3 py-2 text-[11px] text-foreground font-mono resize-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                placeholder="粘贴 JSON"
                @input="
                  importParsed = [];
                  importParseError = '';
                  importResult = null;
                "
              />
              <div class="flex items-center gap-3">
                <label
                  class="inline-flex items-center gap-1.5 cursor-pointer px-3 h-[var(--row-h)] rounded-[var(--radius)] border border-input text-[12px] text-muted-foreground hover:text-foreground hover:border-primary transition-colors"
                >
                  <Upload :size="12" />
                  选择 JSON 文件
                  <input
                    type="file"
                    accept=".json,.jsonl,.txt"
                    class="hidden"
                    @change="onImportFileChange"
                  />
                </label>
              </div>
              <div
                v-if="importParsed.length > 0"
                class="flex items-center gap-2 text-[12px] text-success"
              >
                <CheckCircle2 :size="13" />
                已解析 {{ importParsed.length }} 条商品
              </div>
              <p v-if="importParseError" class="text-[12px] text-danger">
                {{ importParseError }}
              </p>
            </div>

            <div v-else class="space-y-4">
              <div class="grid grid-cols-4 gap-3">
                <div
                  class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center"
                >
                  <div class="text-[18px] font-mono-tnum font-semibold">
                    {{ importResult.total }}
                  </div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">
                    合计
                  </div>
                </div>
                <div
                  class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center"
                >
                  <div
                    class="text-[18px] font-mono-tnum font-semibold text-[var(--success)]"
                  >
                    {{ importResult.imported }}
                  </div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">
                    导入成功
                  </div>
                </div>
                <div
                  class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center"
                >
                  <div
                    class="text-[18px] font-mono-tnum font-semibold text-muted-foreground"
                  >
                    {{ importResult.skipped }}
                  </div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">
                    已跳过
                  </div>
                </div>
                <div
                  class="rounded-[var(--radius)] border border-border bg-subtle p-3 text-center"
                >
                  <div
                    class="text-[18px] font-mono-tnum font-semibold text-[var(--danger)]"
                  >
                    {{ importResult.failed }}
                  </div>
                  <div class="text-[11px] text-muted-foreground mt-0.5">
                    失败
                  </div>
                </div>
              </div>
              <div
                class="border border-border rounded-[var(--radius)] overflow-hidden"
              >
                <table class="w-full text-[11px]">
                  <thead>
                    <tr class="border-b border-border bg-subtle">
                      <th
                        class="text-left px-3 py-2 text-muted-foreground font-medium"
                      >
                        商品名称
                      </th>
                      <th
                        class="text-left px-3 py-2 text-muted-foreground font-medium w-[80px]"
                      >
                        状态
                      </th>
                      <th
                        class="text-left px-3 py-2 text-muted-foreground font-medium"
                      >
                        备注
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      v-for="item in importResult.items"
                      :key="item.sourceId"
                      class="border-b border-border last:border-0"
                    >
                      <td
                        class="px-3 py-2 max-w-[240px] truncate"
                        :title="item.subject"
                      >
                        {{ item.subject || `ID:${item.sourceId}` }}
                      </td>
                      <td class="px-3 py-2">
                        <BadgeVue
                          :variant="
                            item.status === 'imported'
                              ? 'success'
                              : item.status === 'skipped'
                                ? 'default'
                                : 'danger'
                          "
                          class="text-[10px]"
                        >
                          {{
                            item.status === "imported"
                              ? "已导入"
                              : item.status === "skipped"
                                ? "已跳过"
                                : "失败"
                          }}
                        </BadgeVue>
                      </td>
                      <td
                        class="px-3 py-2 text-muted-foreground truncate max-w-[160px]"
                      >
                        {{
                          item.status === "imported"
                            ? `商品ID: ${item.productId}`
                            : item.error || "-"
                        }}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          <div
            class="px-6 py-4 border-t border-border shrink-0 flex justify-between items-center"
          >
            <button
              v-if="importResult"
              class="text-[12px] text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent p-0 underline"
              @click="
                importResult = null;
                importParsed = [];
                importInput = '';
              "
            >
              重新导入
            </button>
            <div v-else class="text-[11px] text-muted-foreground">
              {{
                importParsed.length > 0
                  ? `${importParsed.length} 条待导入`
                  : "请粘贴 JSON 后解析"
              }}
            </div>
            <div class="flex gap-2">
              <ButtonVue
                variant="outline"
                size="sm"
                @click="importDialog = false"
                >{{ importResult ? "关闭" : "取消" }}</ButtonVue
              >
              <ButtonVue
                v-if="!importResult"
                size="sm"
                :disabled="importLoading"
                @click="doImport"
              >
                <Loader2 v-if="importLoading" :size="13" class="animate-spin" />
                {{ importLoading ? "导入中..." : "开始导入" }}
              </ButtonVue>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Review dialog -->
    <Teleport to="body">
      <div
        v-if="reviewDialog.visible"
        class="fixed inset-0 z-50 flex items-center justify-center"
        @click.self="reviewDialog.visible = false"
      >
        <div class="absolute inset-0 bg-black/50" />
        <div
          class="relative bg-card border border-border rounded-[var(--radius-lg)] shadow-xl w-[420px] p-6 space-y-4"
        >
          <h3 class="text-[14px] font-semibold">{{ reviewDialogTitle }}</h3>
          <div class="space-y-1">
            <label class="text-[12px] text-muted-foreground">
              {{
                reviewDialog.type === "reject"
                  ? "驳回原因（必填）"
                  : "备注（可选）"
              }}
            </label>
            <textarea
              v-model="reviewDialog.comment"
              class="w-full h-24 rounded-[var(--radius)] border border-input bg-background px-3 py-2 text-[12px] text-foreground resize-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
              :placeholder="
                reviewDialog.type === 'reject'
                  ? '请填写驳回原因...'
                  : '可填写备注说明...'
              "
            />
          </div>
          <div class="flex justify-end gap-2">
            <ButtonVue
              variant="outline"
              size="sm"
              @click="reviewDialog.visible = false"
              >取消</ButtonVue
            >
            <ButtonVue
              size="sm"
              :disabled="
                reviewDialog.loading ||
                (reviewDialog.type === 'reject' && !reviewDialog.comment.trim())
              "
              @click="submitReview"
            >
              {{ reviewDialog.loading ? "处理中..." : "确认" }}
            </ButtonVue>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Vector scan panel -->
    <Teleport to="body">
      <div
        v-if="scanVisible"
        class="fixed bottom-6 right-6 z-50 w-[340px] bg-card border border-border rounded-[var(--radius-lg)] shadow-xl p-4 space-y-3"
      >
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2 text-[13px] font-semibold">
            <Database :size="14" class="text-primary" />
            一键向量化
          </div>
          <button
            v-if="scanStatus.state !== 'scanning'"
            class="text-muted-foreground hover:text-foreground cursor-pointer border-0 bg-transparent p-0"
            @click="closeScanPanel"
          >
            <X :size="14" />
          </button>
        </div>

        <p
          v-if="scanStatus.state === 'idle'"
          class="text-[12px] text-muted-foreground"
        >
          正在连接，请稍候...
        </p>

        <template v-else-if="scanStatus.state === 'scanning'">
          <div class="flex items-center justify-between text-[12px]">
            <span class="text-muted-foreground flex items-center gap-1.5">
              <Loader2 :size="12" class="animate-spin" />
              正在处理...
            </span>
            <span class="font-mono-tnum text-foreground"
              >{{ scanStatus.processed }} / {{ scanStatus.total }}</span
            >
          </div>
          <div class="h-1.5 rounded-full bg-muted overflow-hidden">
            <div
              class="h-full rounded-full bg-primary transition-all duration-300"
              :style="{
                width:
                  (scanStatus.total > 0
                    ? (scanStatus.processed / scanStatus.total) * 100
                    : 0) + '%'
              }"
            />
          </div>
          <div class="flex gap-4 text-[11px] text-muted-foreground">
            <span class="text-success">✓ {{ scanStatus.success }} 成功</span>
            <span v-if="scanStatus.fail > 0" class="text-danger"
              >✗ {{ scanStatus.fail }} 失败</span
            >
          </div>
        </template>

        <template v-else-if="scanStatus.state === 'done'">
          <div class="flex items-center gap-2 text-[12px]">
            <CheckCircle2 :size="14" class="text-success shrink-0" />
            <span
              >扫描完成：<span class="font-medium text-success">{{
                scanStatus.success
              }}</span>
              成功<template v-if="scanStatus.fail > 0">
                /
                <span class="font-medium text-danger">{{
                  scanStatus.fail
                }}</span>
                失败</template
              ></span
            >
          </div>
          <div class="h-1.5 rounded-full bg-success/20">
            <div class="h-full rounded-full bg-success" style="width: 100%" />
          </div>
        </template>

        <div
          v-else-if="scanStatus.state === 'error'"
          class="flex items-start gap-2 text-[12px]"
        >
          <XCircle :size="14" class="text-danger shrink-0 mt-0.5" />
          <span class="text-danger">{{ scanStatus.error || "扫描失败" }}</span>
        </div>
      </div>
    </Teleport>
  </div>
</template>
