# PLM → MES 制造版本同步完整模块 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成可验签、可重试、事务一致、版本不可变、订单可追溯且能在界面完整操作的 PLM → MES 制造版本同步模块。

**Architecture:** PLM 发布端发送签名事件，MES 接收端幂等入账后拉取完整制造载荷，复算哈希并在单一事务中保存不可变快照。失败任务进入数据库驱动的退避重试状态机；生产订单固定引用快照，界面通过服务端分页完成十万级商品浏览、对账、重试和版本切换。

**Tech Stack:** .NET 10、ASP.NET Core、EF Core、SQLite、BackgroundService、xUnit、FluentAssertions、Vue 3、TypeScript、Vite。

## Global Constraints

- 商品、制造版本、BOM、工艺路线和工序全部来自 PLM，MES 不生成替代数据。
- 同步必须校验 HMAC-SHA256、五分钟时间窗口、事件 ID 幂等和 SHA-256 载荷哈希。
- 快照及子项必须在同一事务中成功或全部回滚。
- 自动重试间隔固定为 1、5、15、30、60 分钟，五次失败后进入 `manual_action`。
- 已开工订单不得切换快照；未开工订单切换必须留审计。
- 被订单引用的快照不得物理删除。
- 商品搜索必须使用服务端分页，单页最多 50 条。
- 每个生产代码变更先完成 RED → GREEN 测试循环。

---

### Task 1: 固化同步状态机、快照和错误历史模型

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/MesDbContext.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmSyncPersistenceTests.cs`

**Interfaces:**
- Produces: `MesManufacturingSnapshot`、`MesSnapshotBomItem`、`MesSnapshotOperation`、`MesPlmSyncAttempt`、`MesProductionOrderVersionAudit`。
- Extends: `MesPlmProductSyncRecord` with `SnapshotId`, `NextRetryAt`, `LeaseUntil`, `LastAttemptAt`, `RequiresManualAction`.

- [x] **Step 1: Write failing persistence tests**

Create SQLite integration tests asserting unique `(PlmProductId, PlmManufacturingVersionId, PayloadHash)`, cascade-owned snapshot children, append-only attempts and restricted deletion when an order references a snapshot.

- [x] **Step 2: Run the tests and verify RED**

Run: `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter FullyQualifiedName~PlmSyncPersistenceTests`

Expected: compilation failure because snapshot and attempt entities do not exist.

- [x] **Step 3: Implement entities and EF mappings**

Use `mes_manufacturing_snapshot`, `mes_snapshot_bom_item`, `mes_snapshot_operation`, `mes_plm_sync_attempt`, and `mes_production_order_version_audit`. Configure `DeleteBehavior.Restrict` from orders to snapshots and unique indexes for event IDs and snapshot source identity.

- [x] **Step 4: Run focused and schema compatibility tests**

Run focused tests plus `--filter FullyQualifiedName~MesSchemaCompatibilityTests`; require zero failures.

- [x] **Step 5: Commit**

Commit message: `feat(mes): add immutable manufacturing snapshot model`.

### Task 2: 提取制造载荷校验与哈希验证

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/ManufacturingPayload.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/ManufacturingPayloadValidator.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/ManufacturingPayloadHasher.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/ManufacturingPayloadValidationTests.cs`

**Interfaces:**
- Produces: `Validate(ManufacturingPayload): IReadOnlyList<ManufacturingPayloadIssue>`.
- Produces: `ComputeHash(ManufacturingPayload): string` using the same canonical ordering as PLM.

- [x] **Step 1: Write failing validation tests**

Cover missing product code/name, empty BOM/routing, missing material code/name, invalid quantity/loss rate, missing operation code/name/capability, invalid cycle/setup time, reordered collections producing the same hash, and a declared hash mismatch.

- [x] **Step 2: Run tests and verify RED**

Run the focused test class; expected missing validator/hasher types.

- [x] **Step 3: Implement typed payload, validator and canonical hasher**

Return stable paths such as `bomItems[0].materialCode` and `operations[0].requiredCapability`. Sort BOM by `LineNo` and operations by `Sequence` before JSON serialization and SHA-256 hashing.

- [x] **Step 4: Run tests and verify GREEN**

Require all focused tests to pass without network access.

- [x] **Step 5: Commit**

Commit message: `feat(mes): validate plm manufacturing payloads`.

### Task 3: 实现幂等、事务化制造快照同步

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/PlmManufacturingClient.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/PlmManufacturingSyncService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Services/PlmProductSyncService.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmManufacturingSnapshotTests.cs`

**Interfaces:**
- Produces: `SyncAsync(long syncRecordId, CancellationToken): Task<MesManufacturingSnapshot>`.
- Consumes: the typed validator and hasher from Task 2.

- [x] **Step 1: Write failing service tests**

Test successful full snapshot creation, duplicate payload reuse, changed hash creating a new snapshot, declared/computed hash mismatch rejection, validation failure with field paths, and database failure leaving no partial snapshot children.

- [x] **Step 2: Run focused tests and verify RED**

Expected: service does not exist and current service writes mutable generic product/BOM/routing rows.

- [x] **Step 3: Implement the PLM client and transaction boundary**

Fetch only `/api/manufacturing/products/{productId}/versions/{versionId}`. Begin one EF transaction, validate and hash before writes, create snapshot root and children, update ledger/event, commit, then return the immutable snapshot.

- [x] **Step 4: Persist failure attempts outside the rolled-back snapshot transaction**

Record error code, full field issue JSON, attempt number, start/end timestamps and retryability without creating fake products or partial children.

- [x] **Step 5: Run focused and full MES tests**

Run the new class and the entire MES test project; require zero failures.

- [x] **Step 6: Commit**

Commit message: `feat(mes): sync plm snapshots atomically`.

### Task 4: 完成事件安全、幂等回执和自动重试

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/PlmIntegrationController.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/PlmEventAuthenticator.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/PlmSyncRetryPolicy.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/PlmSyncRetryWorker.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Program.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/appsettings.json`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmManufacturingSyncTests.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmSyncRetryWorkerTests.cs`

**Interfaces:**
- Produces: `Authenticate(ReleaseEvent, headers, utcNow): PlmEventAuthenticationResult`.
- Produces: retry delays `[1, 5, 15, 30, 60]` minutes and atomic lease acquisition.

- [x] **Step 1: Add failing event tests**

Cover valid signature, invalid signature, missing fields, stale/future timestamp, duplicate event returning original result, and invalid 64-character hexadecimal hash.

- [x] **Step 2: Implement the isolated authenticator and thin controller**

Use fixed-time signature comparison. The controller authenticates, inserts the event idempotently, creates a pending ledger and invokes the sync service.

- [x] **Step 3: Add failing retry policy and worker tests**

Use a fake clock to assert 1/5/15/30/60-minute schedules, transition to `manual_action` after five failures, only due records are selected, and an active lease prevents double processing.

- [x] **Step 4: Implement policy, worker and registration**

Register one hosted service. Poll at a configurable short interval; atomically change due rows to `syncing` with a lease before network calls.

- [x] **Step 5: Run focused and full tests**

Require all event/retry tests and full MES regression to pass.

- [x] **Step 6: Commit**

Commit message: `feat(mes): retry plm sync jobs safely`.

### Task 5: 固定生产订单快照并支持受控版本切换

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Services/ProductionOrderService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/ProductionOrderController.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/ProductionOrderSnapshotTests.cs`

**Interfaces:**
- Produces: `CreateFromSnapshotAsync(CreateProductionOrderRequest, long userId)`.
- Produces: `SwitchSnapshotAsync(long orderId, long snapshotId, string reason, long userId)`.

- [x] **Step 1: Write failing order snapshot tests**

Reject pending/failed/manual-action/obsolete snapshots; expand tasks only from immutable snapshot operations; default to the newest synced snapshot; preserve old version after start; permit draft switch and persist old/new IDs, reason and operator.

- [x] **Step 2: Run focused tests and verify RED**

Expected: current order stores mutable product/routing references and has no switch audit.

- [x] **Step 3: Implement snapshot-bound creation and expansion**

Require snapshot ID for generic orders. Copy operation identity and planning attributes from the snapshot into operation tasks so later PLM changes cannot alter an order.

- [x] **Step 4: Implement guarded version switching**

Allow only `draft` and unstarted `planned` orders. Replace unstarted tasks from the target snapshot in one transaction and append the audit record.

- [x] **Step 5: Run focused and full MES tests**

Require zero failures, including existing production order and scheduling tests.

- [x] **Step 6: Commit**

Commit message: `feat(mes): bind production orders to plm snapshots`.

### Task 6: 完成同步管理、对账和十万级目录 API

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Services/PlmSync/PlmReconciliationService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/MesProductController.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmSyncManagementApiTests.cs`

**Interfaces:**
- Produces paged `GET /api/mes-products/catalog/search?keyword=&page=&pageSize=`.
- Produces paged `GET /api/mes-products/sync-records?status=&keyword=&page=&pageSize=`.
- Produces detail, retry, reconcile, snapshot and related-order endpoints.

- [x] **Step 1: Write failing API tests**

Assert page size clamps to 50, code/name keyword forwarding, status filters, summary counts, error detail and attempt history, retry eligibility, snapshot detail, related orders, PLM outage blocking response and absence of local substitute rows.

- [x] **Step 2: Run tests and verify RED**

Expected: current list is limited to 500 unpaged records and client-side filtering.

- [x] **Step 3: Implement paged queries and reconciliation**

Return total/page/pageSize/hasMore with database-side filters. Keep PLM and MES counts explicit; never label local data as newly fetched PLM data during outages.

- [x] **Step 4: Add permission policies**

Map view, retry, force-sync and order-version-switch operations to separate authorization policies while preserving test authentication setup.

- [x] **Step 5: Run API and full backend tests**

Require zero failures.

- [x] **Step 6: Commit**

Commit message: `feat(mes): expose complete plm sync management api`.

### Task 7: 完成 MES 同步管理与生产订单界面

**Files:**
- Create: `OctopusMES/web/OctopusMES.Web/src/api/plmSync.ts`
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/plmSyncTypes.ts`
- Modify: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/PlmSyncManagementView.vue`
- Modify: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/SchedulingBoardView.vue`
- Test: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/__tests__/plmSyncMapping.test.ts`

**Interfaces:**
- Consumes paged API contracts from Task 6.
- Produces server-side search, paging, persistent errors, attempt drawer, snapshot drawer and version-switch confirmation.

- [x] **Step 1: Write failing mapping and static UI tests**

Cover status-to-copy/color mapping, retry countdown, red field issue formatting, debounce request parameters, latest-version priority, disabled reasons and absence of full-array client filtering.

- [x] **Step 2: Implement typed API client and types**

Define every request/response exactly once; prohibit `any`. Abort stale search requests and debounce input by 300 ms.

- [x] **Step 3: Rebuild PLM sync management view**

Add eight summary cards, status tabs, server pagination, detailed errors, retry history, next retry time and all approved row actions.

- [x] **Step 4: Update scheduling order selector**

Search by code/name remotely, prioritize newest synced snapshot, show BOM/routing/operation summary, block invalid snapshots and expose new-version switching only for unstarted orders.

- [x] **Step 5: Run frontend tests, type check and build**

The MES web package has no test script, so run `npm run build`; its `vue-tsc -b && vite build` command is the required type-check and production-build gate. Require exit code 0.

- [x] **Step 6: Commit**

Commit message: `feat(mes-web): complete plm sync operations`.

### Task 8: 指标、真实闭环验收与计划收尾

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Services/MesStatsService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/StatsController.cs`
- Modify: `docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md`
- Modify: `docs/superpowers/plans/2026-08-07-plm-mes-manufacturing-sync-complete.md`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/PlmSyncMetricsTests.cs`

**Interfaces:**
- Produces pending/retry/manual/failure-rate/average-duration/last-success metrics.
- Produces a user-facing acceptance checklist with exact page operations and expected states.

- [x] **Step 1: Write failing metric tests**

Seed records in every state and assert totals, failure rate, average duration, last success time and consecutive failures.

- [x] **Step 2: Implement metrics and existing notification integration**

Expose metrics through the existing stats API. Emit an existing-system notification after three consecutive failures for one manufacturing version, and whenever a record first enters `manual_action`; deduplicate notifications by sync-record ID and terminal attempt number.

- [x] **Step 3: Run all automated verification**

Run MES full tests, PLM full tests, MES frontend build, PLM frontend build and `git diff --check`.

- [x] **Step 4: Run the real local closed loop**

Using real PLM data: publish a complete manufacturing version, observe automatic MES sync, create an order from the snapshot, expand tasks, simulate and publish scheduling. Also verify duplicate notification, forced update, PLM outage, automatic retry, manual retry, validation errors and started-order protection.

- [x] **Step 5: Update acceptance and plan checklists**

Record actual IDs, timestamps, status transitions and verification commands. Mark an item complete only when direct evidence exists.

- [x] **Step 6: Commit**

Commit message: `docs: complete plm mes sync acceptance`.
