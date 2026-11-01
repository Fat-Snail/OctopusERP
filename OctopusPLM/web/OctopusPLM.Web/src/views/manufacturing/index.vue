<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { ElMessage, ElMessageBox } from "element-plus";
import { createAuthedHttp } from "@/utils/plmHttp";
import { searchMaterials } from "@/api/material";
import type { MaterialRow } from "@/api/material/types";
import {
  hasIssue as containsIssue,
  issueFor as findIssue,
  issueSummary,
  type ValidationIssue,
  validateDraft
} from "./validation";

type Product = { productId: number; productName: string; status: string; categoryId: number; categoryName?: string };
type VersionRow = {
  manufacturingVersionId: number;
  productId: number;
  productName: string;
  version: string;
  status: string;
  bomVersion: string;
  routingVersion: string;
  bomItemCount: number;
  operationCount: number;
  payloadHash?: string;
  releasedAt?: string;
  mesSyncStatus: string;
  lastMesSyncAt?: string;
  mesSyncError?: string;
  mesNotifyCount: number;
  releaseReady: boolean;
  validationIssues: ValidationIssue[];
};
type BomItem = {
  lineNo: number;
  materialId?: number;
  materialQuery?: string;
  materialCode: string;
  materialName: string;
  materialSpecification?: string;
  materialUnit?: string;
  quantity: number;
  lossRate: number;
};
type Operation = {
  sequence: number;
  operationCode: string;
  operationName: string;
  requiredCapability: string;
  standardCycleSec: number;
  setupMinutes: number;
  isSplittable: boolean;
};
const productHttp = createAuthedHttp("/api/product");
const manufacturingHttp = createAuthedHttp("/api/manufacturing");
const templateHttp = createAuthedHttp("/api/bom-templates");
const products = ref<Product[]>([]);
const versions = ref<VersionRow[]>([]);
const materialOptions = ref<MaterialRow[]>([]);
const materialLoading = ref(false);
const activeMaterialRow = ref<number | null>(null);
const templates = ref<any[]>([]);
let materialSearchSequence = 0;
const saving = ref(false);
const checkingId = ref<number | null>(null);
const publishingId = ref<number | null>(null);
const confirmingId = ref<number | null>(null);
const notifyConfirmId = ref<number | null>(null);
const editingId = ref<number | null>(null);
const showForm = ref(false);
const form = ref({
  productId: "",
  version: "V1.0",
  bomVersion: "BOM-V1.0",
  routingVersion: "ROUTE-V1.0",
  bomItems: [
    { lineNo: 10, materialCode: "", materialName: "", quantity: 1, lossRate: 0 }
  ] as BomItem[],
  operations: [
    {
      sequence: 10,
      operationCode: "",
      operationName: "",
      requiredCapability: "",
      standardCycleSec: 60,
      setupMinutes: 0,
      isSplittable: true
    }
  ] as Operation[]
});
const formIssues = computed(() => validateDraft(form.value));
const currentTemplate = computed(() => templates.value.find(x => x.status === "current" && x.isLatest));
const draftTemplate = computed(() => templates.value.find(x => x.status === "draft"));
function issueFor(path: string) {
  return findIssue(formIssues.value, path);
}
function hasIssue(path: string) {
  return containsIssue(formIssues.value, path);
}
function fieldClass(path: string) {
  return hasIssue(path)
    ? "border-red-500 bg-red-50 focus:border-red-600"
    : "border-border";
}
function resetForm() {
  editingId.value = null;
  form.value = {
    productId: "",
    version: "V1.0",
    bomVersion: "BOM-V1.0",
    routingVersion: "ROUTE-V1.0",
    bomItems: [
      {
        lineNo: 10,
        materialCode: "",
        materialName: "",
        quantity: 1,
        lossRate: 0
      }
    ],
    operations: [
      {
        sequence: 10,
        operationCode: "",
        operationName: "",
        requiredCapability: "",
        standardCycleSec: 60,
        setupMinutes: 0,
        isSplittable: true
      }
    ]
  };
}
function toggleCreateForm() {
  if (showForm.value) {
    showForm.value = false;
    resetForm();
    return;
  }
  resetForm();
  showForm.value = true;
}
async function editDraft(row: VersionRow) {
  try {
    const r = await manufacturingHttp.get(
      `/versions/${row.manufacturingVersionId}`
    );
    if (r.data?.code !== 200) throw new Error(r.data?.msg || "读取草稿失败");
    const data = r.data.data;
    editingId.value = row.manufacturingVersionId;
    form.value = {
      productId: String(data.productId),
      version: data.version,
      bomVersion: data.bomVersion,
      routingVersion: data.routingVersion,
      bomItems: data.bomItems.map((x: BomItem) => ({ ...x, materialQuery: x.materialId ? `${x.materialCode} · ${x.materialName}` : "" })),
      operations: data.operations
    };
    materialOptions.value = data.bomItems.filter((x: BomItem) => x.materialId).map((x: BomItem) => ({
      materialId: x.materialId!, materialCode: x.materialCode, materialName: x.materialName,
      specification: x.materialSpecification, baseUnit: x.materialUnit || "", materialType: "", status: "active"
    }));
    await loadTemplates();
    showForm.value = true;
    window.scrollTo({ top: 0, behavior: "smooth" });
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : "读取草稿失败");
  }
}
async function load() {
  const [p, v] = await Promise.all([
    productHttp.get("/list", {
      params: { page: 1, size: 200, status: "active" }
    }),
    manufacturingHttp.get("/versions")
  ]);
  products.value = p.data?.data?.rows ?? [];
  versions.value = v.data?.data ?? [];
}
function addBom() {
  form.value.bomItems.push({
    lineNo: (form.value.bomItems.length + 1) * 10,
    materialCode: "",
    materialName: "",
    quantity: 1,
    lossRate: 0
  });
}
function removeBom(index: number) { form.value.bomItems.splice(index, 1); }
function normalizeBomLines() { form.value.bomItems.forEach((item, index) => item.lineNo = (index + 1) * 10); }
async function searchMaterialOptions(keyword: string, rowIndex: number) {
  activeMaterialRow.value = rowIndex;
  const sequence = ++materialSearchSequence;
  if (!keyword.trim()) { materialOptions.value = []; return; }
  await new Promise<void>(resolve => window.setTimeout(resolve, 250));
  if (sequence !== materialSearchSequence) return;
  materialLoading.value = true;
  try { const result = await searchMaterials({ keyword, selectableOnly: true, page: 1, pageSize: 30 }); if (sequence === materialSearchSequence) materialOptions.value = result.rows; }
  finally { if (sequence === materialSearchSequence) materialLoading.value = false; }
}
function selectMaterial(item: BomItem, materialId?: number) {
  if (!materialId) { item.materialCode = ""; item.materialName = ""; item.materialSpecification = ""; item.materialUnit = ""; return; }
  if (form.value.bomItems.some(x => x !== item && x.materialId === materialId)) { item.materialId = undefined; return ElMessage.warning("该物料已在 BOM 中"); }
  const material = materialOptions.value.find(x => x.materialId === materialId); if (!material) return;
  item.materialId = material.materialId;
  item.materialCode = material.materialCode; item.materialName = material.materialName;
  item.materialSpecification = material.specification; item.materialUnit = material.baseUnit;
  item.materialQuery = `${material.materialCode} · ${material.materialName}`;
  activeMaterialRow.value = null;
}
async function loadTemplates() {
  const product = products.value.find(x => String(x.productId) === form.value.productId);
  if (!product) return templates.value = [];
  const r = await templateHttp.get("", { params: { categoryId: product.categoryId } });
  templates.value = r.data?.data ?? [];
}
function applyCurrentTemplate() {
  const current = currentTemplate.value;
  if (!current) return ElMessage.warning("该类目暂无已发布的当前 BOM 模板");
  form.value.bomItems = current.items.map((x: any) => ({ lineNo:x.lineNo, materialId:x.materialId, materialQuery:`${x.material.materialCode} · ${x.material.materialName}`, materialCode:x.material.materialCode, materialName:x.material.materialName, materialSpecification:x.material.specification, materialUnit:x.material.baseUnit, quantity:x.defaultQuantity, lossRate:x.defaultLossRate }));
  ElMessage.success(`已套用 ${current.templateName} ${current.version}`);
}
async function initializeTemplates() {
  const r = await templateHttp.post("/initialize-leaf-categories");
  if (r.data?.code !== 200) return ElMessage.error(r.data?.msg || "初始化失败");
  await loadTemplates();
  ElMessage.success(`初始化完成：新增 ${r.data.data.createdCount} 个，已有 ${r.data.data.existingCount} 个`);
}
async function createTemplateVersion() {
  const source = draftTemplate.value || currentTemplate.value || templates.value[0];
  if (!source) return ElMessage.warning("请先初始化类目 BOM 模板");
  const { value } = await ElMessageBox.prompt("请输入新模板版本，例如 V1.1", "新建模板版本", { inputValue: "V1.1", confirmButtonText: "创建草稿", cancelButtonText: "取消", inputPattern: /^V\d+(\.\d+)*$/, inputErrorMessage: "版本格式应类似 V1.1" });
  const r = await templateHttp.post(`/${source.templateId}/new-version`, { version: value });
  if (r.data?.code !== 200) return ElMessage.error(r.data?.msg || "创建失败");
  await loadTemplates(); ElMessage.success(r.data.msg);
}
async function saveBomAsTemplate() {
  const draft = draftTemplate.value;
  if (!draft) return ElMessage.warning("当前类目没有草稿模板，请先初始化或新建版本");
  const validItems = form.value.bomItems.filter(x => x.materialId);
  if (!validItems.length || validItems.length !== form.value.bomItems.length) return ElMessage.warning("请先为全部 BOM 行选择有效物料");
  const r = await templateHttp.put(`/${draft.templateId}`, {
    templateName: draft.templateName || "通用 MES 生产 BOM",
    items: validItems.map(x => ({ lineNo: x.lineNo, materialId: x.materialId, defaultQuantity: x.quantity, defaultLossRate: x.lossRate }))
  });
  if (r.data?.code !== 200) return ElMessage.error(r.data?.msg || "保存模板失败");
  await loadTemplates(); ElMessage.success("当前 BOM 已保存到类目模板草稿（与商品 BOM 独立）");
}
async function publishTemplate() {
  const draft = draftTemplate.value;
  if (!draft) return ElMessage.warning("没有可发布的模板草稿");
  await ElMessageBox.confirm(`发布 ${draft.templateName} ${draft.version} 后，它将成为该类目唯一的当前模板。`, "确认发布模板", { type: "warning", confirmButtonText: "发布", cancelButtonText: "取消" });
  const r = await templateHttp.post(`/${draft.templateId}/publish`);
  if (r.data?.code !== 200) return ElMessage.error(r.data?.msg || "发布失败");
  await loadTemplates(); ElMessage.success(r.data.msg);
}
function addOperation() {
  form.value.operations.push({
    sequence: (form.value.operations.length + 1) * 10,
    operationCode: "",
    operationName: "",
    requiredCapability: "",
    standardCycleSec: 60,
    setupMinutes: 0,
    isSplittable: true
  });
}
async function saveDraft() {
  if (!form.value.productId) return ElMessage.warning("请选择 PLM 商品");
  saving.value = true;
  try {
    const payload = {
      ...form.value,
      productId: Number(form.value.productId)
    };
    const r = editingId.value
      ? await manufacturingHttp.put(`/versions/${editingId.value}`, payload)
      : await manufacturingHttp.post("/versions", payload);
    if (r.data?.code !== 200) throw new Error(r.data?.msg);
    ElMessage.success(r.data.msg);
    showForm.value = false;
    resetForm();
    await load();
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : "创建失败");
  } finally {
    saving.value = false;
  }
}
async function release(row: VersionRow) {
  try {
    if (confirmingId.value !== row.manufacturingVersionId) {
      checkingId.value = row.manufacturingVersionId;
      const detail = await manufacturingHttp.get(
        `/versions/${row.manufacturingVersionId}`
      );
      if (detail.data?.code !== 200)
        throw new Error(detail.data?.msg || "制造版本检查失败");
      const data = detail.data?.data;
      const issues: ValidationIssue[] = data?.validationIssues ?? [];
      if (issues.length) {
        ElMessage.warning(`发布前请补齐：${issueSummary(issues)}`);
        await editDraft(row);
        return;
      }
      confirmingId.value = row.manufacturingVersionId;
      ElMessage.warning("发布后不可修改，请再次点击按钮确认发布");
      window.setTimeout(() => {
        if (confirmingId.value === row.manufacturingVersionId)
          confirmingId.value = null;
      }, 8000);
      return;
    }

    confirmingId.value = null;
    publishingId.value = row.manufacturingVersionId;
    const r = await manufacturingHttp.post(
      `/versions/${row.manufacturingVersionId}/release`
    );
    if (r.data?.code !== 200) throw new Error(r.data?.msg || "发布失败");
    if (r.data?.data?.mesSyncStatus === "synced") {
      ElMessage.success(r.data?.msg || "制造版本已发布并同步 MES");
    } else {
      ElMessage.warning(
        r.data?.msg || "制造版本已发布，但 MES 尚未确认；请重新通知 MES"
      );
    }
    await load();
  } catch (e) {
    const message =
      e instanceof Error ? e.message : "发布失败，请检查网络或登录状态";
    ElMessage.error(
      message === "Request failed with status code 401"
        ? "登录状态已失效，请重新登录后发布"
        : message
    );
  } finally {
    checkingId.value = null;
    publishingId.value = null;
  }
}
async function notifyMes(row: VersionRow) {
  const force = row.mesSyncStatus === "synced";
  if (notifyConfirmId.value !== row.manufacturingVersionId) {
    notifyConfirmId.value = row.manufacturingVersionId;
    ElMessage.warning(
      force
        ? "该版本已同步，是否强制更新 MES？请再次点击确认"
        : "将重新通知 MES，请再次点击确认"
    );
    window.setTimeout(() => {
      if (notifyConfirmId.value === row.manufacturingVersionId)
        notifyConfirmId.value = null;
    }, 8000);
    return;
  }
  notifyConfirmId.value = null;
  publishingId.value = row.manufacturingVersionId;
  try {
    const r = await manufacturingHttp.post(
      `/versions/${row.manufacturingVersionId}/notify-mes`,
      {},
      { params: { force } }
    );
    if (r.data?.code !== 200) throw new Error(r.data?.msg || "MES 更新失败");
    ElMessage.success(r.data.msg);
    await load();
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : "MES 更新失败");
  } finally {
    publishingId.value = null;
  }
}
function versionStatus(v: VersionRow) {
  if (v.status === "draft") {
    if (!v.releaseReady) return "资料不完整";
    return versions.value.some(
      x => x.productId === v.productId && x.status === "released"
    )
      ? "待发布更新"
      : "草稿";
  }
  return "已发布";
}
onMounted(load);
</script>

<template>
  <div class="p-5 space-y-4">
    <div class="flex justify-between items-center">
      <div>
        <h1 class="text-xl font-semibold">制造版本管理</h1>
        <p class="text-sm text-muted-foreground mt-1">
          维护真实 PLM 商品的 BOM、工艺路线和标准工时，发布后同步到 MES。
        </p>
      </div>
      <button
        class="px-4 py-2 rounded bg-primary text-primary-foreground text-sm"
        @click="toggleCreateForm"
      >
        {{ showForm ? "关闭录入" : "新建制造版本" }}
      </button>
    </div>
    <div
      v-if="showForm"
      class="bg-card border border-border rounded-lg p-5 space-y-5"
    >
      <div class="grid grid-cols-4 gap-3">
        <select
          v-model="form.productId"
          :disabled="editingId !== null"
          class="border border-border rounded px-3 py-2"
          @change="loadTemplates"
        >
          <option value="">选择真实 PLM 商品</option>
          <option
            v-for="p in products"
            :key="p.productId"
            :value="String(p.productId)"
          >
            {{ p.productId }} · {{ p.productName }}
          </option></select
        ><input
          v-model="form.version"
          class="border border-border rounded px-3 py-2"
          placeholder="制造版本"
        /><input
          v-model="form.bomVersion"
          class="border border-border rounded px-3 py-2"
          placeholder="BOM 版本"
        /><input
          v-model="form.routingVersion"
          class="border border-border rounded px-3 py-2"
          placeholder="工艺路线版本"
        />
      </div>
      <div>
        <div v-if="form.productId" class="mb-4 border rounded-lg bg-muted/30 p-3 flex flex-wrap items-center justify-between gap-3">
          <div>
            <div class="font-medium text-sm">类目 BOM 模板</div>
            <div class="text-xs text-muted-foreground mt-1">
              当前：{{ currentTemplate ? `${currentTemplate.templateName} ${currentTemplate.version}（${currentTemplate.items.length} 项）` : "尚未发布" }}；
              草稿：{{ draftTemplate ? `${draftTemplate.version}（${draftTemplate.items.length} 项）` : "无" }}
            </div>
          </div>
          <div class="flex flex-wrap gap-3 text-sm">
            <button class="text-primary" @click="initializeTemplates">初始化全部叶子类目</button>
            <button class="text-primary" @click="createTemplateVersion">新建模板版本</button>
            <button class="text-primary" @click="saveBomAsTemplate">将当前 BOM 保存为模板草稿</button>
            <button class="text-primary" @click="publishTemplate">发布模板草稿</button>
          </div>
        </div>
        <div class="flex justify-between mb-2">
          <div><strong>BOM 明细</strong><span class="ml-3 text-xs text-muted-foreground">编码和名称来自 PLM 物料库</span></div>
          <div class="flex gap-4"><button class="text-primary text-sm" @click="applyCurrentTemplate">套用当前类目模板</button><button class="text-primary text-sm" @click="normalizeBomLines">整理行号</button><button class="text-primary text-sm" @click="addBom">+ 添加物料</button></div>
        </div>
        <div class="grid grid-cols-[70px_260px_150px_1fr_70px_100px_100px_60px] gap-2 px-2 py-2 bg-muted text-xs font-medium text-muted-foreground rounded-t">
          <span>行号</span><span>选择物料</span><span>物料编码</span><span>物料名称 / 规格</span><span>单位</span><span>用量</span><span>损耗率</span><span>操作</span>
        </div>
        <div
          v-for="(b, i) in form.bomItems"
          :key="i"
          class="grid grid-cols-[70px_260px_150px_1fr_70px_100px_100px_60px] gap-2 py-2 border-b items-start"
        >
          <input
            v-model.number="b.lineNo"
            type="number"
            class="border rounded px-2 py-1"
            placeholder="行号"
          />
          <div class="relative">
            <input v-model="b.materialQuery" class="w-full border rounded px-2 py-1.5" :class="fieldClass(`bomItems[${i}].materialId`)" placeholder="编码或名称搜索" @focus="activeMaterialRow=i" @input="b.materialId=undefined;searchMaterialOptions(b.materialQuery||'',i)" />
            <div v-if="activeMaterialRow===i && (materialLoading || materialOptions.length)" class="absolute z-20 top-full left-0 right-0 mt-1 bg-white border rounded shadow-lg max-h-64 overflow-auto">
              <div v-if="materialLoading" class="p-3 text-sm text-muted-foreground">搜索中…</div>
              <button v-for="m in materialOptions" :key="m.materialId" type="button" class="block w-full text-left px-3 py-2 hover:bg-muted border-b last:border-0" @click="selectMaterial(b,m.materialId)">
                <div class="font-mono text-sm">{{m.materialCode}}</div><div class="text-sm">{{m.materialName}} · {{m.specification||'无规格'}} · {{m.baseUnit}}</div>
              </button>
            </div>
            <p v-if="hasIssue(`bomItems[${i}].materialId`)" class="mt-1 text-xs text-red-600">{{issueFor(`bomItems[${i}].materialId`)}}</p>
          </div>
          <div class="font-mono text-sm px-2 py-1.5 bg-muted rounded min-h-8">{{ b.materialCode || '—' }}</div>
          <div class="px-2 py-1"><div>{{ b.materialName || '尚未选择物料' }}</div><div class="text-xs text-muted-foreground">{{ b.materialSpecification || '无规格' }}</div></div>
          <div class="px-2 py-1.5">{{ b.materialUnit || '—' }}</div>
          <div>
            <input
              v-model.number="b.quantity"
              type="number"
              min="0.0001"
              step="0.0001"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`bomItems[${i}].quantity`)"
              placeholder="用量"
            />
            <p
              v-if="hasIssue(`bomItems[${i}].quantity`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`bomItems[${i}].quantity`) }}
            </p>
          </div>
          <div>
            <input
              v-model.number="b.lossRate"
              type="number"
              min="0"
              step="0.01"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`bomItems[${i}].lossRate`)"
              placeholder="损耗率"
            />
            <p
              v-if="hasIssue(`bomItems[${i}].lossRate`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`bomItems[${i}].lossRate`) }}
            </p>
          </div>
          <button class="text-red-600 text-sm py-1.5" @click="removeBom(i)">删除</button>
        </div>
        <div v-if="!form.bomItems.length" class="border border-dashed rounded-b p-6 text-center text-sm text-muted-foreground">暂无 BOM 物料，点击“添加物料”或套用类目模板</div>
      </div>
      <div>
        <div class="flex justify-between mb-2">
          <strong>工艺路线</strong
          ><button class="text-primary text-sm" @click="addOperation">
            + 添加工序
          </button>
        </div>
        <div
          v-for="(o, i) in form.operations"
          :key="i"
          class="grid grid-cols-7 gap-2 mb-2"
        >
          <input
            v-model.number="o.sequence"
            type="number"
            class="border rounded px-2 py-1"
            placeholder="顺序"
          />
          <div>
            <input
              v-model="o.operationCode"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`operations[${i}].operationCode`)"
              placeholder="工序编码"
            />
            <p
              v-if="hasIssue(`operations[${i}].operationCode`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`operations[${i}].operationCode`) }}
            </p>
          </div>
          <div>
            <input
              v-model="o.operationName"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`operations[${i}].operationName`)"
              placeholder="工序名称"
            />
            <p
              v-if="hasIssue(`operations[${i}].operationName`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`operations[${i}].operationName`) }}
            </p>
          </div>
          <div>
            <input
              v-model="o.requiredCapability"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`operations[${i}].requiredCapability`)"
              placeholder="能力编码"
            />
            <p
              v-if="hasIssue(`operations[${i}].requiredCapability`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`operations[${i}].requiredCapability`) }}
            </p>
          </div>
          <div>
            <input
              v-model.number="o.standardCycleSec"
              type="number"
              min="1"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`operations[${i}].standardCycleSec`)"
              placeholder="秒/件"
            />
            <p
              v-if="hasIssue(`operations[${i}].standardCycleSec`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`operations[${i}].standardCycleSec`) }}
            </p>
          </div>
          <div>
            <input
              v-model.number="o.setupMinutes"
              type="number"
              min="0"
              class="w-full border rounded px-2 py-1"
              :class="fieldClass(`operations[${i}].setupMinutes`)"
              placeholder="换型分钟"
            />
            <p
              v-if="hasIssue(`operations[${i}].setupMinutes`)"
              class="mt-1 text-xs text-red-600"
            >
              {{ issueFor(`operations[${i}].setupMinutes`) }}
            </p>
          </div>
          <label class="text-sm flex items-center gap-1"
            ><input v-model="o.isSplittable" type="checkbox" />允许拆分</label
          >
        </div>
      </div>
      <button
        class="px-4 py-2 rounded bg-primary text-primary-foreground disabled:opacity-50"
        :disabled="saving"
        @click="saveDraft"
      >
        {{
          saving ? "保存中…" : editingId ? "保存草稿修改" : "保存制造版本草稿"
        }}
      </button>
    </div>
    <div class="bg-card border border-border rounded-lg overflow-x-auto">
      <table class="w-full text-sm">
        <thead class="bg-muted">
          <tr>
            <th class="p-3 text-left">商品</th>
            <th class="p-3">制造版本</th>
            <th class="p-3">BOM</th>
            <th class="p-3">工艺路线</th>
            <th class="p-3">状态</th>
            <th class="p-3">MES 同步</th>
            <th class="p-3">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="v in versions"
            :key="v.manufacturingVersionId"
            class="border-t border-border"
          >
            <td class="p-3">
              {{ v.productName }}
              <div class="text-xs text-muted-foreground">
                PLM ID {{ v.productId }}
              </div>
            </td>
            <td class="p-3 text-center">{{ v.version }}</td>
            <td class="p-3 text-center">
              {{ v.bomVersion }} · {{ v.bomItemCount }} 项
            </td>
            <td class="p-3 text-center">
              {{ v.routingVersion }} · {{ v.operationCount }} 道
            </td>
            <td class="p-3 text-center">
              <span
                :class="
                  v.status === 'released'
                    ? 'text-emerald-700'
                    : v.releaseReady
                      ? 'text-amber-700'
                      : 'font-medium text-red-600'
                "
                >{{ versionStatus(v) }}</span
              >
              <div
                v-if="v.status === 'draft' && !v.releaseReady"
                class="mx-auto mt-2 max-w-72 rounded border border-red-300 bg-red-50 px-3 py-2 text-xs leading-5 text-red-700"
              >
                {{ issueSummary(v.validationIssues) }}
              </div>
            </td>
            <td class="p-3 text-center">
              <span
                v-if="v.status !== 'released'"
                :class="v.releaseReady ? 'text-gray-500' : 'text-red-600'"
              >
                {{ v.releaseReady ? "未发布" : "未发布（资料不完整）" }}
              </span>
              <div v-else-if="v.mesSyncStatus === 'synced'">
                <span class="text-emerald-700">已同步</span>
                <div class="text-xs text-muted-foreground">
                  {{ v.lastMesSyncAt || "MES 已确认接收" }}
                </div>
              </div>
              <div v-else-if="v.mesSyncStatus === 'failed'">
                <span class="text-red-600">同步失败</span>
                <div
                  class="max-w-56 truncate text-xs text-red-500"
                  :title="v.mesSyncError"
                >
                  {{ v.mesSyncError || "MES 未确认接收" }}
                </div>
              </div>
              <span v-else class="text-amber-700">
                {{ v.mesSyncStatus === "syncing" ? "同步中" : "待同步" }}
              </span>
            </td>
            <td class="p-3 text-center">
              <div
                v-if="v.status === 'draft'"
                class="flex justify-center gap-2"
              >
                <button
                  class="rounded border border-border px-3 py-2"
                  @click="editDraft(v)"
                >
                  编辑草稿
                </button>
                <button
                  class="rounded px-4 py-2 text-white shadow-sm disabled:cursor-not-allowed disabled:opacity-50"
                  :class="
                    confirmingId === v.manufacturingVersionId
                      ? 'bg-amber-600'
                      : 'bg-primary'
                  "
                  :disabled="
                    checkingId === v.manufacturingVersionId ||
                    publishingId === v.manufacturingVersionId
                  "
                  @click="release(v)"
                >
                  {{
                    checkingId === v.manufacturingVersionId
                      ? "检查中…"
                      : publishingId === v.manufacturingVersionId
                        ? "发布中…"
                        : confirmingId === v.manufacturingVersionId
                          ? "再次点击确认发布"
                          : !v.releaseReady
                            ? "补齐发布资料"
                            : versionStatus(v) === "待发布更新"
                              ? "发布制造版本更新"
                              : "发布制造版本"
                  }}
                </button>
              </div>
              <div v-else class="flex flex-col items-center gap-1">
                <button
                  class="rounded px-3 py-2 text-white shadow-sm disabled:cursor-not-allowed disabled:opacity-50"
                  :class="
                    notifyConfirmId === v.manufacturingVersionId
                      ? 'bg-amber-600'
                      : v.mesSyncStatus === 'synced'
                        ? 'bg-slate-600'
                        : 'bg-primary'
                  "
                  :disabled="publishingId === v.manufacturingVersionId"
                  @click="notifyMes(v)"
                >
                  {{
                    publishingId === v.manufacturingVersionId
                      ? "更新中…"
                      : notifyConfirmId === v.manufacturingVersionId
                        ? "再次点击确认"
                        : v.mesSyncStatus === "synced"
                          ? "强制更新 MES"
                          : "重新通知 MES"
                  }}
                </button>
                <span class="text-xs text-muted-foreground">
                  已发布 {{ v.releasedAt }} · 已通知
                  {{ v.mesNotifyCount || 0 }} 次
                </span>
              </div>
            </td>
          </tr>
          <tr v-if="!versions.length">
            <td colspan="7" class="p-10 text-center text-muted-foreground">
              暂无制造版本，请点击“新建制造版本”
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
