# PLM → MES 商品同步管理 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 MES 只使用可追溯的 PLM 已发布制造版本，并提供同步台账、失败重试、版本快照和订单引用闭环。

**Architecture:** PLM 是产品、制造版本、BOM 和工艺路线的唯一权威源；PLM 发布后通知 MES，MES 拉取完整版本并保存不可变快照。MES 的资源、日历、生产订单和排程仍由 MES 管理，订单通过快照 ID 固定制造版本。

**Tech Stack:** ASP.NET Core、EF Core、SQLite、Vue 3、TypeScript、HTTP/HMAC、xUnit。

## Global Constraints

- MES 不得生成、补造或静默回退产品、BOM、工艺路线和工序数据。
- 只有 PLM `released` 制造版本允许同步和创建生产订单。
- 已被订单引用的制造快照不可覆盖或删除。
- 所有同步请求必须幂等、可追溯、可重试。
- PLM 不可用时保留既有快照可读，但禁止把本地数据标记为 PLM 新数据。

---

### Task 1: 建立 PLM 商品同步台账

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/MesDbContext.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Services/PlmProductSyncService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/MesProductController.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmProductSyncTests.cs`

**Interfaces:**
- Produces `MesPlmProductSyncRecord` and `GET /api/mes-products/sync-records`.
- Sync states: `pending`, `synced`, `failed`, `obsolete`.

- [ ] Write a failing test proving a successful sync stores PLM product ID, source version, status and timestamp.
- [ ] Write a failing test proving a failed sync stores the error without creating a fake `MesProduct`.
- [ ] Add the entity, unique index `(PlmProductId, PlmVersion)`, and immutable source fields.
- [ ] Update `SyncAsync` to create/update the ledger atomically.
- [ ] Add list and retry endpoints, then run focused and full MES tests.

### Task 2: 在 PLM 建立制造版本、BOM 与工艺路线发布模型

**Files:**
- Modify: `OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbContext.cs`
- Create: `OctopusPLM/src/OctopusPLM.Infrastructure/Entities/ManufacturingEntities.cs`
- Create: `OctopusPLM/src/OctopusPLM.Api/Controllers/ManufacturingController.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/ManufacturingVersionTests.cs`

**Interfaces:**
- `GET /api/manufacturing/products/{productId}/versions/{versionId}`.
- `POST /api/manufacturing/versions/{versionId}/release`.

- [ ] Test draft → released validation requiring at least one BOM item and one routing operation.
- [ ] Implement versioned BOM, routing, operation and capability entities.
- [ ] Return one signed, deterministic manufacturing payload with `payloadHash`.
- [ ] Test released versions are immutable and obsolete versions remain readable.

### Task 3: 实现发布事件、幂等同步与失败重试

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Controllers/PlmIntegrationController.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmManufacturingSyncService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmManufacturingSyncTests.cs`

**Interfaces:**
- `POST /api/integration/plm/manufacturing-released` with `eventId` idempotency.
- `POST /api/mes-products/sync-records/{id}/retry`.

- [ ] Test signature, timestamp, event ID and payload hash validation.
- [ ] Test duplicate delivery creates one snapshot only.
- [ ] Persist product/BOM/routing/operation snapshots in one transaction.
- [ ] Persist failure reason and retry count without partial snapshots.

### Task 4: 让生产订单固定引用 PLM 制造快照

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Services/ProductionOrderService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/ProductionOrderController.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/ProductionOrderSnapshotTests.cs`

**Interfaces:**
- `MesProductionOrder.ManufacturingSnapshotId` is required for new generic orders.

- [ ] Reject orders referencing draft, failed or obsolete snapshots.
- [ ] Expand tasks only from the referenced immutable routing snapshot.
- [ ] Keep started orders on the old version when PLM releases a new version.
- [ ] Allow only unstarted orders to switch versions with an audit record.

### Task 5: 增加 MES 同步管理界面与订单选择器

**Files:**
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/PlmSyncManagementView.vue`
- Modify: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/SchedulingBoardView.vue`
- Modify: `OctopusMES/web/OctopusMES.Web/src/router/index.ts`
- Modify: `OctopusMES/web/OctopusMES.Web/src/components/app/SubMenu.vue`

**Interfaces:**
- Display source system, PLM IDs, version, sync status/time, hash, error and retry action.

- [ ] Build sync ledger table with status filters and retry action.
- [ ] Replace product/routing IDs with one released manufacturing-version selector.
- [ ] Show BOM version, routing version and operation count before order creation.
- [ ] Disable creation when PLM is unavailable or snapshot is not `synced`.
- [ ] Run TypeScript checks and production build.

### Task 6: 验收与上线保护

**Files:**
- Modify: `docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md`

- [ ] Verify no runtime path returns `DEMO-*` or `TEST_FIXTURE` as PLM data.
- [ ] Verify a PLM release can be traced through snapshot, order, tasks and schedule plan.
- [ ] Verify PLM outage shows a blocking error and does not create local substitute data.
- [ ] Run PLM build, MES full tests and MES frontend production build.
