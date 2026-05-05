# Read-Only Data Lists Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an ungated read-only `DONNEES` area with tabbed live-filtered lists for articles, vendeurs, and ventes.

**Architecture:** Add a focused `DataListService` that projects read-only data into page models. Add one Razor Page at `/donnees` with server-selected tabs and client-side live filtering for the currently rendered table. Reuse existing display helpers for prices, item statuses, sale payment labels, and sale buyer display.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite, vanilla JavaScript, xUnit smoke and service tests.

---

### Task 1: Read-Only Data Projection

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/DataListService.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/DataListModels.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/DataListServiceTests.cs`

- [ ] **Step 1: Write failing service tests**

Add tests that seed sellers, items, and a sale, then verify:

- article rows include all item statuses, not only `in_stock`
- seller search text contains seller code and name only
- sale search text contains sale id, buyer details, payment label, and item reference/title, but not receipt details or cheque traceability

- [ ] **Step 2: Run the new service tests and verify RED**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter DataListServiceTests -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.worktrees\data-lists\.artifacts\data-list-red
```

Expected: build or test failure because `DataListService` does not exist.

- [ ] **Step 3: Implement projections**

Create `DataListService` methods:

- `ListItemsAsync()`
- `ListSellersAsync()`
- `ListSalesAsync()`

Each method returns sorted read-only rows and a precomputed lower-case `SearchText` for client filtering.

- [ ] **Step 4: Run the service tests and verify GREEN**

Run the same filtered test command and expect the new tests to pass.

### Task 2: Data Lists Page And Navigation

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Data/Index.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Data/Index.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

- [ ] **Step 1: Write failing smoke tests**

Add smoke coverage for:

- `/donnees` renders without admin unlock
- main navigation includes `DONNEES` and marks it active on `/donnees`
- the default Articles tab renders all statuses and has a status filter
- the Vendeurs tab exposes search text built from code/name only
- the Ventes tab exposes search text built from sale id, buyer, payment method, and item reference/title

- [ ] **Step 2: Run focused smoke tests and verify RED**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "FullyQualifiedName~WebApplicationSmokeTests" -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.worktrees\data-lists\.artifacts\data-page-red
```

Expected: new smoke tests fail because `/donnees` and the nav link do not exist.

- [ ] **Step 3: Implement the page**

Add `/donnees` with `onglet=articles|vendeurs|ventes`, defaulting to `articles`. Render French tab labels `Articles`, `Vendeurs`, and `Ventes`. Keep the page read-only: no create/edit/correction actions.

- [ ] **Step 4: Update navigation**

Add a `DONNEES` main-navigation item after `CAISSE` and before `RAPPROCHEMENT`; mark active for `/donnees`.

- [ ] **Step 5: Run focused smoke tests and verify GREEN**

Run the same smoke test command and expect the new smoke tests to pass.

### Task 3: Client Filtering And Documentation

**Files:**
- Modify: `src/AssociationSaleTool.Web/wwwroot/js/site.js`
- Modify: `docs/features.md`
- Modify: `docs/decisions.md`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

- [ ] **Step 1: Write failing smoke coverage for filter hooks**

Assert the page markup includes `data-data-list-root`, `data-data-list-search`, `data-data-list-row`, and article status filter hooks.

- [ ] **Step 2: Implement JavaScript filtering**

Add `initializeDataList(root)` to filter rows by free text and, for article rows, by selected status.

- [ ] **Step 3: Update canonical docs**

Document the ungated read-only data lists in `docs/features.md` and add a lightweight decision `D-038 Expose Ungated Read-Only Data Lists`.

- [ ] **Step 4: Run full verification**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.worktrees\data-lists\.artifacts\data-lists-full
```

Expected: all tests pass.
