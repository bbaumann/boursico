# Intake Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable intake workflow with seller creation, seller listing/detail, seller-centric multi-item entry, both intake modes, and pre-sale correction.

**Architecture:** Keep the workflow inside the existing single Razor Pages web app. Add a focused intake service for seller and item operations, keep page models thin, and use EF Core through the existing `AppDbContext` for persistence.

**Tech Stack:** .NET 8, ASP.NET Core Razor Pages, EF Core, SQLite, xUnit

---

### Task 1: Add intake service coverage for seller creation and item batch creation

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateSellerRequest.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateItemBatchRequest.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateItemRowRequest.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/IntakeMode.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`

- [ ] **Step 1: Write the failing service tests**
- [ ] **Step 2: Run the targeted tests to verify the missing service behavior fails**
- [ ] **Step 3: Implement seller creation with next-code allocation**
- [ ] **Step 4: Implement batch item creation with numbering, references, prices, and initial status**
- [ ] **Step 5: Run the targeted service tests until they pass**

### Task 2: Add edit coverage for pre-sale seller and item correction

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/UpdateSellerRequest.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/UpdateItemRequest.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`

- [ ] **Step 1: Write failing tests for seller and item updates**
- [ ] **Step 2: Run the targeted tests to verify they fail**
- [ ] **Step 3: Implement seller update and item update for the allowed pre-sale fields**
- [ ] **Step 4: Run the intake service tests until they pass**

### Task 3: Build seller pages and page-model tests

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Index.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Index.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`

- [ ] **Step 1: Write failing page-model tests for seller list, create, and detail loading**
- [ ] **Step 2: Run the targeted tests to verify they fail**
- [ ] **Step 3: Implement seller list/create/detail/edit page models and Razor pages**
- [ ] **Step 4: Run the targeted page-model tests until they pass**

### Task 4: Add the seller-centric multi-item intake UI

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Modify: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`

- [ ] **Step 1: Write failing tests for posting a multi-row intake batch**
- [ ] **Step 2: Run the targeted tests to verify they fail**
- [ ] **Step 3: Implement a server-rendered multi-row item intake form on the seller detail page**
- [ ] **Step 4: Show existing items with reference, title, seller price, sale price, and status**
- [ ] **Step 5: Run the targeted page-model tests until they pass**

### Task 5: Add item correction UI and full verification

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Items/Edit.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Items/Edit.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Modify: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

- [ ] **Step 1: Write failing tests for item edit page behavior**
- [ ] **Step 2: Run the targeted tests to verify they fail**
- [ ] **Step 3: Implement item edit UI for title and prices**
- [ ] **Step 4: Extend the smoke test to cover the seller pages happy path at a basic level**
- [ ] **Step 5: Run the full test project and confirm the intake slice passes**
- [ ] **Step 6: Commit**
