# 离散制造通用排程实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不破坏现有服装 MES 能力的前提下，建立可用于离散制造的产品、工艺、资源、工序任务和规则排程能力。

**Architecture:** 先在 MES 内建立通用领域模型和状态机，再以纯 C# 排程内核实现资源日历、前置关系、产能和冲突检测；API 层只负责编排和权限，排程策略通过接口注入。现有 `MesGarmentSchedule` 通过兼容适配器映射为资源日历下的天级排程，不直接重写旧流程。

**Tech Stack:** .NET 8/10、ASP.NET Core、EF Core、SQLite、xUnit、WebApplicationFactory、Vue 3、TypeScript。

## Global Constraints

- 后端依赖方向保持 `Api → Infrastructure/Persistence → Domain/Service`。
- 所有关键业务规则先写失败测试，再写最小实现。
- 默认策略为交期优先、最早可用资源、满足工序前置关系。
- 已开工任务和冻结区间不可被自动重排。
- 现有服装排程 API 和历史数据必须继续可读。
- 新增接口必须保留操作日志、鉴权、限流和统一异常处理。
- 第一阶段不引入外部优化器；排程器通过接口为后续 OR-Tools 留出替换点。

---

### Task 1: 建立通用生产主数据模型

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/MesDbContext.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/MesDbSeeder.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingMasterDataTests.cs`

**Interfaces:**
- Produces entities `MesProduct`, `MesBom`, `MesBomItem`, `MesRouting`, `MesRoutingOperation`, `MesWorkCenter`, `MesResource`, `MesShift`, `MesResourceCalendar`.
- Resource kinds are `equipment`, `team`, and `subcontractor`; routing operations reference required capability rather than a concrete machine.

- [ ] **Step 1: Write failing integration tests**

Cover creation and persistence of a product version, a routing with ordered operations, a work center, an equipment resource, and a calendar exception. Assert unique codes, required relationships, and decimal quantities.

- [ ] **Step 2: Run the focused tests**

Run `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter FullyQualifiedName~SchedulingMasterDataTests`. Expected: FAIL because the new entities and DbSets do not exist.

- [ ] **Step 3: Add entities, DbSets, indexes, and relationships**

Use `mes_` table names, generated long keys, unique product/resource/work-center codes, and restrict or set-null delete behavior where historical scheduling records must survive master-data changes.

- [ ] **Step 4: Seed one deterministic fixture**

Add one product, one two-operation routing, one work center, two resources, one normal shift, and one maintenance interval. Keep the fixture idempotent and separate from the existing garment seed.

- [ ] **Step 5: Re-run focused tests**

Expected: all `SchedulingMasterDataTests` pass and existing MES tests remain green.

### Task 2: Add production orders and operation-task state machine

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/MesDbContext.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/ProductionOrderService.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/ProductionOrderTests.cs`

**Interfaces:**
- `ProductionOrderService.CreateAsync`, `ExplodeOperationsAsync`, `TransitionAsync`.
- Produces `MesProductionOrder` and `MesOperationTask` with task dependencies and traceable source order/product/routing IDs.

- [ ] **Step 1: Write failing tests**

Test that an order expands into routing operations in sequence, quantity is preserved, duplicate expansion is rejected, and illegal transitions such as `completed → draft` fail with a domain error.

- [ ] **Step 2: Run focused tests and verify RED**

Run `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter FullyQualifiedName~ProductionOrderTests`. Expected: missing service/entity failures.

- [ ] **Step 3: Implement order expansion and explicit transitions**

Make transition methods validate the current state and append an audit record or transition timestamp; do not expose a generic status setter to controllers.

- [ ] **Step 4: Verify GREEN and regression coverage**

Run the focused tests and the existing `WorkOrderTests`/`GarmentScheduleTests`.

### Task 3: Implement resource calendars, capacity, and hard-constraint detection

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/ResourceCalendarService.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/CapacityCalculator.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/ConflictDetector.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingConstraintTests.cs`

**Interfaces:**
- `ResourceCalendarService.GetAvailableSlotsAsync(resourceId, from, to)` returns non-overlapping available intervals.
- `CapacityCalculator.CalculateDuration(operation, quantity, resourceCount)` returns required minutes.
- `ConflictDetector.Validate(candidateSlots, dependencies, frozenSlots)` returns typed conflicts with code, message, and affected IDs.

- [ ] **Step 1: Write failing tests**

Cover lunch breaks, maintenance intervals, holidays, resource overlap, missing capability, predecessor finish time, frozen slots, and split work across two shifts.

- [ ] **Step 2: Run tests and confirm RED**

Run `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter FullyQualifiedName~SchedulingConstraintTests`. Expected: missing scheduling service failures.

- [ ] **Step 3: Implement pure interval and duration logic**

Keep date/time calculations timezone-consistent with MES configuration, use half-open intervals `[start, end)`, and never silently move a task across a frozen interval.

- [ ] **Step 4: Verify GREEN**

Run focused tests plus all current MES integration tests.

### Task 4: Add versioned rule-based scheduling engine

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/ISchedulingStrategy.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/DueDateFirstSchedulingStrategy.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/SchedulingService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/Entities.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Persistence/MesDbContext.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingEngineTests.cs`

**Interfaces:**
- `ISchedulingStrategy.BuildPriority(task, context)` returns a comparable priority score.
- `SchedulingService.SimulateAsync(planRequest)` returns candidate slots, unscheduled tasks, conflicts, and explanation codes.
- `SchedulingService.PublishAsync(planId)` creates an immutable published version; `FreezeAsync(planId, until)` protects slots from automatic rescheduling.

- [ ] **Step 1: Write failing tests**

Assert due-date ordering, earliest compatible resource selection, predecessor sequencing, deterministic results for equal inputs, unscheduled-task explanations, plan versioning, publish protection, and freeze protection.

- [ ] **Step 2: Run tests and confirm RED**

Run `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter FullyQualifiedName~SchedulingEngineTests`. Expected: missing scheduling engine failures.

- [ ] **Step 3: Implement the strategy boundary and default algorithm**

Schedule ready tasks by weighted priority, scan compatible resources by earliest available slot, split only when allowed by the operation, and return conflicts instead of dropping tasks.

- [ ] **Step 4: Implement plan lifecycle**

Persist draft/simulation/published/frozen/archived states, record strategy name and weights as JSON, and reject edits to published slots unless a new plan version is created.

- [ ] **Step 5: Verify GREEN and regression coverage**

Run focused scheduling tests and all MES tests.

### Task 5: Expose scheduling APIs and preserve garment compatibility

**Files:**
- Create: `OctopusMES/src/OctopusMES.Api/Controllers/SchedulingController.cs`
- Create: `OctopusMES/src/OctopusMES.Api/Services/Scheduling/GarmentScheduleAdapter.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/GarmentScheduleController.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Services/GarmentScheduleService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Program.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingApiTests.cs`

**Interfaces:**
- `POST /api/scheduling/simulate`
- `POST /api/scheduling/plans`
- `POST /api/scheduling/plans/{id}/publish`
- `POST /api/scheduling/plans/{id}/freeze`
- `GET /api/scheduling/gantt`
- `GET /api/scheduling/conflicts`
- Existing garment endpoints remain available and use the adapter for new writes.

- [ ] **Step 1: Write failing API integration tests**

Test validation errors, authenticated access, operation logging, rate limiting, simulation response shape, publish/freeze transitions, and existing garment create/list behavior after the adapter is registered.

- [ ] **Step 2: Run focused API tests and confirm RED**

Run `dotnet test OctopusMES/tests/OctopusMES.Api.Tests/OctopusMES.Api.Tests.csproj --filter FullyQualifiedName~SchedulingApiTests`. Expected: missing endpoints and adapter behavior.

- [ ] **Step 3: Implement thin controllers and service registration**

Controllers validate request DTOs and delegate to services; all business rules remain in scheduling services. Add the operation log attributes used by existing MES controllers.

- [ ] **Step 4: Implement garment mapping**

Map a garment team to a team resource, a date to a day-level slot, and old schedule quantities/status to a compatibility projection. Preserve old response fields and IDs.

- [ ] **Step 5: Verify GREEN and run all MES tests**

Run the full MES test project and verify no existing garment or webhook tests regress.

### Task 6: Add frontend planning views and operational metrics

**Files:**
- Modify: `OctopusMES/web/OctopusMES.Web/src/api/mes.ts`
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/GanttView.vue`
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/ResourceLoadView.vue`
- Create: `OctopusMES/web/OctopusMES.Web/src/views/scheduling/schedulingTypes.ts`
- Test: frontend project tests for request mapping and date/slot rendering

**Interfaces:**
- Consume scheduling API DTOs and display plan version, resource rows, task bars, frozen markers, and conflict explanations.
- Preserve the existing garment calendar route while adding a generic scheduling route.

- [ ] **Step 1: Write failing component/data-mapping tests**

Cover conversion of API slots to resource rows, conflict rendering, freeze markers, and empty/unscheduled states.

- [ ] **Step 2: Implement typed API clients and views**

Use strict TypeScript types; no `any`, no duplicated DTO definitions, and no direct business-rule calculation in Vue components.

- [ ] **Step 3: Verify frontend tests and build**

Run the MES web test command and `npm run build` from `OctopusMES/web/OctopusMES.Web`.

### Task 7: Add cross-system events and reporting

**Files:**
- Modify: `OctopusMES/src/OctopusMES.Api/Services/WebhookSender.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Services/MesStatsService.cs`
- Modify: `OctopusMES/src/OctopusMES.Api/Controllers/StatsController.cs`
- Test: `OctopusMES/tests/OctopusMES.Api.Tests/SchedulingIntegrationTests.cs`

**Interfaces:**
- Emit order released, material shortage, operation completed, delay, and production completed events.
- Provide capacity load, schedule adherence, lateness, and resource utilization reports.

- [ ] **Step 1: Write failing integration tests**

Assert event payload IDs are traceable to production order, operation task, schedule plan, and resource; assert report totals match persisted slots and reports.

- [ ] **Step 2: Implement events and metrics**

Reuse existing HMAC webhook conventions and UMC notification patterns. Keep cross-system calls asynchronous and failure-tolerant.

- [ ] **Step 3: Run full MES regression and document rollout**

Run backend and frontend tests, update CLAUDE.md/TODO.md with rollout status, and verify the compatibility mode remains available.
