# PLM Material Library and Category BOM Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a real PLM material library, initialize a versioned generic MES BOM template for every active leaf category, and replace free-text manufacturing BOM entry with searchable material selection, row deletion, template extraction, and independent-copy application.

**Architecture:** PLM owns material and template master data. Category templates form a per-category version chain with one current published version; applying a template copies server-resolved material snapshots into a draft manufacturing version. MES continues to consume only released manufacturing payloads, preserving the existing PLM→MES boundary.

**Tech Stack:** ASP.NET Core 10, Entity Framework Core 10, SQLite, xUnit, Vue 3, TypeScript, Element Plus, Axios, Vite.

## Global Constraints

- Create templates only for active leaf categories; parent categories never receive templates.
- Initialize `通用 MES 生产 BOM / V1.0` idempotently without generating fake material rows.
- A category retains history but has at most one `current` template with `IsLatest=true`.
- Applying a template creates an independent manufacturing BOM copy; later template changes never overwrite it.
- BOM writes trust `MaterialId`; the server resolves code, name, specification, and unit snapshots.
- Released manufacturing versions and existing MES snapshots remain immutable.
- Material and product searches are server-paged and must not load a ten-thousand-plus catalog into the browser.
- Preserve unrelated dirty workspace files and commit only task-scoped changes.

---

### Task 1: Material and Template Persistence Model

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Core/Entities/Material.cs`
- Create: `OctopusPLM/src/OctopusPLM.Core/Entities/CategoryBomTemplate.cs`
- Create: `OctopusPLM/src/OctopusPLM.Core/Entities/MaterialTemplateAudit.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Core/Entities/ManufacturingVersion.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbContext.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbSeeder.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialTemplateSchemaTests.cs`

**Interfaces:**
- Produces `Material`, `CategoryBomTemplate`, `CategoryBomTemplateItem`, and `MaterialTemplateAudit` entities.
- Extends `ManufacturingBomItem` with `MaterialId`, `MaterialSpecification`, and `MaterialUnit` snapshots.
- Produces SQLite-compatible schema self-healing for existing local databases.

- [ ] **Step 1: Write the failing schema test**

Create an in-memory SQLite context, call the compatibility seeder, and assert tables `plm_material`, `plm_category_bom_template`, `plm_category_bom_template_item`, `plm_material_template_audit` and the three new manufacturing BOM columns exist. Also assert duplicate `MaterialCode` and duplicate `(CategoryId, Version)` inserts fail.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test OctopusPLM/tests/OctopusPLM.Api.Tests/OctopusPLM.Api.Tests.csproj --filter FullyQualifiedName~MaterialTemplateSchemaTests
```

Expected: FAIL because the entities/tables do not exist.

- [ ] **Step 3: Implement entities and mappings**

Use these public shapes:

```csharp
public sealed class Material {
    public long MaterialId { get; set; }
    public string MaterialCode { get; set; } = "";
    public string MaterialName { get; set; } = "";
    public string? Specification { get; set; }
    public string BaseUnit { get; set; } = "件";
    public string MaterialType { get; set; } = "raw_material";
    public string Status { get; set; } = "active";
    public long CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CategoryBomTemplate {
    public long TemplateId { get; set; }
    public long CategoryId { get; set; }
    public string TemplateName { get; set; } = "通用 MES 生产 BOM";
    public string Version { get; set; } = "V1.0";
    public string Status { get; set; } = "draft";
    public bool IsLatest { get; set; }
    public long? SourceManufacturingVersionId { get; set; }
    public List<CategoryBomTemplateItem> Items { get; set; } = [];
}
```

Map restrictive material references, cascading template-item deletion, unique material code, unique category/version, and a filtered or service-enforced single-current constraint compatible with SQLite.
`MaterialTemplateAudit` stores action, entity type/ID, operator ID/name, source ID, affected manufacturing version ID, detail JSON, and timestamp; audit rows are append-only.

- [ ] **Step 4: Add legacy SQLite self-healing**

Extend `PlmDbSeeder` to create missing tables/indexes and add nullable `MaterialId`, `MaterialSpecification`, and `MaterialUnit` columns. Do not overwrite existing free-text BOM rows.

- [ ] **Step 5: Run focused and full PLM tests**

Expected: focused test PASS and existing 31 tests remain green.

- [ ] **Step 6: Commit**

```bash
git add OctopusPLM/src/OctopusPLM.Core/Entities OctopusPLM/src/OctopusPLM.Infrastructure/Persistence OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialTemplateSchemaTests.cs
git commit -m "feat(plm): add material and bom template model"
```

### Task 2: Material Library API

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Api/Services/MaterialService.cs`
- Create: `OctopusPLM/src/OctopusPLM.Api/Controllers/MaterialsController.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Program.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialApiTests.cs`

**Interfaces:**
- Produces `GET /api/materials`, `POST /api/materials`, `PUT /api/materials/{id}`, `DELETE /api/materials/{id}`, and `GET /api/materials/{id}/references`.
- Search returns `{ rows, total, page, pageSize }` and matches code or name case-insensitively.

- [ ] **Step 1: Write failing API tests**

Cover: paged code/name search; uppercase/lowercase duplicate code rejection; inactive material excluded when `selectableOnly=true`; unreferenced delete succeeds; referenced material delete returns code 409 with manufacturing/template reference counts; ordinary authenticated users can view while only `admin`/`editor` roles can mutate; successful mutations append audit rows.

- [ ] **Step 2: Run tests and verify RED**

Expected: requests return 404 because `/api/materials` does not exist.

- [ ] **Step 3: Implement `MaterialService`**

Expose:

```csharp
Task<PagedMaterials> SearchAsync(string? keyword, string? type, string? status,
    bool selectableOnly, int page, int pageSize, CancellationToken ct);
Task<Material> CreateAsync(MaterialWriteRequest request, long userId, CancellationToken ct);
Task UpdateAsync(long id, MaterialWriteRequest request, CancellationToken ct);
Task<MaterialReferences> GetReferencesAsync(long id, CancellationToken ct);
Task DeleteAsync(long id, CancellationToken ct);
```

Clamp page size to 100. Normalize material codes with `Trim().ToUpperInvariant()`. Reject code changes after first reference; permit name/specification/unit maintenance.

- [ ] **Step 4: Implement controller and explicit business responses**

Return the project-standard `{ code, msg, data }`; use 400 for validation, 404 for missing records, and 409 for duplicate/referenced conflicts.
Register `MaterialView` as authenticated and `MaterialManage` as role `admin` or `editor`; apply policies per action using the standard mapped role claim.

- [ ] **Step 5: Run focused and full tests**

Expected: all material tests and existing PLM tests PASS.

- [ ] **Step 6: Commit**

```bash
git add OctopusPLM/src/OctopusPLM.Api/Services/MaterialService.cs OctopusPLM/src/OctopusPLM.Api/Controllers/MaterialsController.cs OctopusPLM/src/OctopusPLM.Api/Program.cs OctopusPLM/tests/OctopusPLM.Api.Tests/MaterialApiTests.cs
git commit -m "feat(plm): expose material library api"
```

### Task 3: Leaf-Category Template Initialization and Version Lifecycle

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Api/Services/CategoryBomTemplateService.cs`
- Create: `OctopusPLM/src/OctopusPLM.Api/Controllers/BomTemplatesController.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Controllers/CategoryController.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Program.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbSeeder.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/CategoryBomTemplateTests.cs`

**Interfaces:**
- Produces idempotent `InitializeLeafCategoriesAsync`.
- Produces list/detail/save/new-version/publish endpoints.
- Publishing is transactional and leaves exactly one current template per category.

- [ ] **Step 1: Write failing lifecycle tests**

Create an active parent, two active leaves, and one inactive leaf. Assert initialization creates only two `V1.0` drafts; a second call creates zero. Assert creating a new leaf through the category API creates its V1.0 draft. Publish leaf A V1.0, create V1.1, publish it, then assert V1.0 is `historical`, V1.1 is `current`, exactly one row has `IsLatest=true`, and each mutation appends an audit row.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: FAIL because template initialization and lifecycle services do not exist.

- [ ] **Step 3: Implement initialization**

Define a leaf as an active category with no active child. For each leaf missing any template, insert `通用 MES 生产 BOM / V1.0 / draft / IsLatest=false` with no fake items. Return `{ leafCount, createdCount, existingCount }`.

- [ ] **Step 4: Implement version creation and publication transaction**

`CreateNewVersionAsync(sourceId, version)` copies items into a new draft. `PublishAsync(id)` validates non-empty items and active materials, updates any prior current row to historical, and marks the selected draft current/latest in one transaction.

- [ ] **Step 5: Add startup idempotent initialization**

After normal category seeding, call the same service-compatible domain routine so existing and newly seeded leaf categories receive V1.0 drafts. Do not fill their items.

- [ ] **Step 6: Integrate new-leaf creation and permissions**

After a category create/update makes a category an active leaf, idempotently ensure its V1.0 template. Register `BomTemplateView` for authenticated users and `BomTemplateManage`, `BomTemplateApply`, and `BomTemplateExtract` for `admin`/`editor`.

- [ ] **Step 7: Run focused and full tests, then commit**

```bash
git add OctopusPLM/src/OctopusPLM.Api/Services/CategoryBomTemplateService.cs OctopusPLM/src/OctopusPLM.Api/Controllers/BomTemplatesController.cs OctopusPLM/src/OctopusPLM.Api/Controllers/CategoryController.cs OctopusPLM/src/OctopusPLM.Api/Program.cs OctopusPLM/src/OctopusPLM.Infrastructure/Persistence/PlmDbSeeder.cs OctopusPLM/tests/OctopusPLM.Api.Tests/CategoryBomTemplateTests.cs
git commit -m "feat(plm): version category bom templates"
```

### Task 4: Template Extraction, Application, and Trusted BOM Snapshots

**Files:**
- Create: `OctopusPLM/src/OctopusPLM.Api/Services/ManufacturingBomService.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Controllers/ManufacturingController.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Api/Services/ManufacturingReleaseValidator.cs`
- Modify: `OctopusPLM/src/OctopusPLM.Core/Entities/ManufacturingVersion.cs`
- Test: `OctopusPLM/tests/OctopusPLM.Api.Tests/ManufacturingBomTemplateApiTests.cs`

**Interfaces:**
- BOM request becomes `BomItemRequest(int LineNo, long MaterialId, decimal Quantity, decimal LossRate)`.
- Produces extract/apply endpoints and server-resolved BOM snapshots.
- Preserves released manufacturing payload compatibility.

- [ ] **Step 1: Write failing integration tests**

Assert create/update resolves material code/name/spec/unit from `MaterialId` even if the client sends no display text; duplicate material IDs and inactive materials are rejected. Assert applying a current template replaces only a draft BOM, creates independent rows, and later template edits do not change those rows. Assert extraction copies rows to a new category template draft. Assert released versions reject apply/update.

- [ ] **Step 2: Run focused tests and verify RED**

Expected: FAIL because existing requests still trust free-text material fields.

- [ ] **Step 3: Implement trusted snapshot resolution**

Move BOM replacement into `ManufacturingBomService.ReplaceDraftBomAsync`. Load all requested materials in one query, require active status, reject duplicates, and write snapshot values from the database. Keep operations handling unchanged.

- [ ] **Step 4: Implement extraction and application transactions**

Extraction derives `CategoryId` from the version's product, validates all BOM rows have `MaterialId`, and creates the requested template version. Application verifies category equality and draft status, then replaces the BOM in one transaction.

- [ ] **Step 5: Implement historical free-text compatibility**

At read time expose `materialMatchStatus` as `matched` or `unmatched`. New draft release validation adds `MaterialId`/active-material issues; already released historical versions remain readable and are not mutated.

- [ ] **Step 6: Run PLM and MES sync regression tests**

Run PLM full tests plus MES tests for manufacturing payload hashing/snapshot sync. Expected: existing payload fields remain identical and all tests PASS.

- [ ] **Step 7: Commit**

```bash
git add OctopusPLM/src/OctopusPLM.Api/Services/ManufacturingBomService.cs OctopusPLM/src/OctopusPLM.Api/Controllers/ManufacturingController.cs OctopusPLM/src/OctopusPLM.Api/Services/ManufacturingReleaseValidator.cs OctopusPLM/src/OctopusPLM.Core/Entities/ManufacturingVersion.cs OctopusPLM/tests/OctopusPLM.Api.Tests/ManufacturingBomTemplateApiTests.cs
git commit -m "feat(plm): apply templates to manufacturing bom"
```

### Task 5: Material Library Web Page

**Files:**
- Create: `OctopusPLM/web/OctopusPLM.Web/src/api/material/index.ts`
- Create: `OctopusPLM/web/OctopusPLM.Web/src/api/material/types.ts`
- Create: `OctopusPLM/web/OctopusPLM.Web/src/views/material/index.vue`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/router/index.ts`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/components/app/SubMenu.vue`

**Interfaces:**
- Produces `/plm/material` with server-side search, filtering, paging, create/edit, reference display, disable, and guarded delete.
- Supplies reusable material API types to the manufacturing selector.

- [ ] **Step 1: Add typed API client and compile to verify missing UI imports fail**

Define `MaterialRow`, `MaterialQuery`, `MaterialWriteRequest`, and `MaterialReferences`. Use the existing `plmHttp` wrapper and never request over 100 rows.

- [ ] **Step 2: Implement the page**

Display columns: 编码、名称、规格、单位、类型、状态、引用数、操作. Search must debounce 300 ms and query the server by code/name. Use an Element Plus drawer for create/edit and a reference dialog before disabling or deleting.

- [ ] **Step 3: Add route and menu entry**

Add `/plm/material` titled `物料库` under PLM navigation without changing unrelated menus.

- [ ] **Step 4: Run typecheck and production build**

```bash
cd OctopusPLM/web/OctopusPLM.Web
npm run typecheck
npm run build
```

Expected: both exit 0.

- [ ] **Step 5: Commit**

```bash
git add OctopusPLM/web/OctopusPLM.Web/src/api/material OctopusPLM/web/OctopusPLM.Web/src/views/material OctopusPLM/web/OctopusPLM.Web/src/router/index.ts OctopusPLM/web/OctopusPLM.Web/src/components/app/SubMenu.vue
git commit -m "feat(plm-web): add material library"
```

### Task 6: Usable Manufacturing BOM Editor and Template Management

**Files:**
- Create: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/MaterialPicker.vue`
- Create: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/BomEditor.vue`
- Create: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/TemplateManager.vue`
- Create: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/bomEditorModel.ts`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/index.vue`
- Modify: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/validation.ts`
- Modify: `OctopusPLM/web/OctopusPLM.Web/package.json`
- Modify: `OctopusPLM/web/OctopusPLM.Web/package-lock.json`
- Test: `OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing/bomEditorModel.test.ts`

**Interfaces:**
- `BomEditor` consumes `modelValue: BomItem[]` and emits `update:modelValue`.
- `MaterialPicker` performs remote code/name search and emits the selected `MaterialRow`.
- `TemplateManager` lists a category's version chain and exposes initialize/new-version/publish/extract/apply actions.

- [ ] **Step 1: Write framework-independent model tests**

Add Vitest as a development dependency and a `test` script, then create `bomEditorModel.test.ts`. Cover `removeBomItem`, duplicate selection detection, next ten-based line number, and `normalizeLineNumbers`.

- [ ] **Step 2: Run model tests and verify RED**

Expected: FAIL because `bomEditorModel.ts` does not exist.

- [ ] **Step 3: Implement the pure editor model**

Export:

```ts
export function removeBomItem(items: BomItem[], index: number): BomItem[];
export function nextLineNo(items: BomItem[]): number;
export function normalizeLineNumbers(items: BomItem[]): BomItem[];
export function hasDuplicateMaterial(items: BomItem[], materialId: number, exceptIndex?: number): boolean;
```

- [ ] **Step 4: Implement `MaterialPicker` and `BomEditor`**

Render a table with explicit headers: 行号、选择物料、编码、名称/规格、单位、用量、损耗率、操作. Selection auto-populates display fields. Add a red trash/delete button per row, `添加物料`, and `整理行号`. Empty BOM is allowed in drafts. Duplicate selection shows a visible warning and leaves the row unchanged.

- [ ] **Step 5: Implement template controls**

After selecting a product, show category name, current template version, `套用当前模板`, `提取为类目模板`, and `管理模板版本`. Non-empty BOM replacement requires an Element Plus confirmation. Template rows use the same material picker and delete behavior.

- [ ] **Step 6: Integrate with manufacturing save/edit**

Submit only `lineNo`, `materialId`, `quantity`, and `lossRate` for BOM items. Existing unmatched historical rows render red with `待匹配物料` and require selecting a material before a new draft can release.

- [ ] **Step 7: Run model tests, typecheck, and build**

Expected: all pass with no TypeScript errors.

- [ ] **Step 8: Commit**

```bash
git add OctopusPLM/web/OctopusPLM.Web/src/views/manufacturing OctopusPLM/web/OctopusPLM.Web/package.json OctopusPLM/web/OctopusPLM.Web/package-lock.json
git commit -m "feat(plm-web): rebuild manufacturing bom editor"
```

### Task 7: Real Data Initialization and End-to-End Acceptance

**Files:**
- Modify: `docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md`
- Modify: `docs/superpowers/plans/2026-08-08-plm-material-library-category-bom-template.md`
- Test: existing PLM and MES test projects

**Interfaces:**
- Produces a reproducible acceptance record using real local PLM material/template/version IDs and the resulting MES snapshot/order IDs.

- [ ] **Step 1: Run all automated verification**

Run PLM full tests, MES full tests, PLM typecheck/build, MES build, and `git diff --check`. Stop on any failure.

- [ ] **Step 2: Start the local stack and verify health**

Run `./restart.sh`; confirm PLM API/web and MES API/web return HTTP 200.

- [ ] **Step 3: Initialize real material master data without invented relations**

Create or import the phone materials already represented by the approved Xiaomi manufacturing BOM, using their existing codes/names/specifications. Record the material IDs. Do not seed unrelated placeholder material.

- [ ] **Step 4: Initialize and version leaf-category templates**

Run leaf initialization, verify parent exclusion and idempotence, configure the phone category V1.0, create/publish V1.1, and verify exactly one current template while V1.0 remains historical.

- [ ] **Step 5: Exercise the browser workflow**

Open PLM manufacturing management, select the Xiaomi product, apply the current template, remove one row, re-add it through code/name search, normalize lines, save the draft, and extract a new template version. Confirm code/name/spec/unit relationships remain visible.

- [ ] **Step 6: Publish through MES**

Publish a complete manufacturing version, verify MES reconciliation reaches consistent/synced, create an order from the new immutable snapshot, expand operations, simulate, and publish a schedule.

- [ ] **Step 7: Verify independence and protection**

Change a template draft and confirm the already generated product BOM and MES snapshot do not change. Confirm released manufacturing versions reject BOM deletion or template reapplication.

- [ ] **Step 8: Record evidence and commit**

Update acceptance with actual IDs, counts, timestamps, state transitions, and verification command totals.

```bash
git add docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md docs/superpowers/plans/2026-08-08-plm-material-library-category-bom-template.md
git commit -m "docs: accept plm material and bom template flow"
```

## Completion Record — 2026-08-08

Tasks 1–7 are functionally complete on local `master`. The implementation kept the existing manufacturing page cohesive instead of introducing three thin wrapper components, and browser-level acceptance replaced the proposed Vitest-only editor helpers. During real UI verification, two integration defects were found and corrected: the unregistered Element Plus material controls were replaced with native page controls, and MES phone resources were upgraded idempotently from legacy `EX-*` capabilities to the production capability set.

Verification evidence is recorded in `docs/acceptance/discrete-manufacturing-scheduling-acceptance-checklist.md`. PLM tests pass 35/35, MES tests pass 113/113, both production web builds pass, and the real PLM → MES → production order → simulated schedule → published plan flow completed successfully. The only remaining repository-wide check is a pre-existing TypeScript failure in `src/views/category/manage/index.vue`, outside this plan's changed files.
