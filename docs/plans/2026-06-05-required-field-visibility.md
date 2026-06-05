# Required Field Visibility Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make required seller and item edit fields visibly distinct without changing submit-button disabled behavior.

**Architecture:** Add small reusable label markup directly in the existing Razor pages and centralize the visual treatment in `site.css`. Keep the server-side validation model unchanged and test the rendered pages with the existing smoke-test infrastructure.

**Tech Stack:** ASP.NET Core Razor Pages, Bootstrap-adjacent custom CSS, xUnit smoke tests, `rtk dotnet test`.

---

### Task 1: Seller Form Required Markers

**Files:**
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`

**Step 1: Write the failing test**

Add a smoke test that requests `/vendeurs/creer` and asserts that `Prénom` and `Nom` labels include `Obligatoire`.

**Step 2: Run test to verify it fails**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter RequiredField`

Expected: fail because the required marker is not rendered yet.

**Step 3: Write minimal implementation**

Update seller create/edit labels for `Prénom` and `Nom` to include a shared-looking inline badge:

```html
<span class="field-label-text">Prénom <span class="required-field-badge">Obligatoire</span></span>
```

Add a short French note near the form validation summary:

```html
<p class="form-required-note">Les champs marqués <span class="required-field-badge">Obligatoire</span> doivent être complétés.</p>
```

Add CSS for `.field-label-text`, `.required-field-badge`, `.form-required-note`, and invalid required inputs.

**Step 4: Run test to verify it passes**

Run the same filtered test. Expected: pass.

### Task 2: Item Edit Required Markers

**Files:**
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Items/Edit.cshtml`

**Step 1: Write the failing test**

Add a smoke test that requests an editable item page and asserts that `Titre` and `Prix vendeur` labels include `Obligatoire`.

**Step 2: Run test to verify it fails**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter RequiredField`

Expected: fail because item edit does not render the required marker yet.

**Step 3: Write minimal implementation**

Update item edit labels for `Titre` and `Prix vendeur` with the same badge and note.

**Step 4: Run test to verify it passes**

Run the filtered test. Expected: pass.

### Task 3: Final Verification

**Files:**
- Verify all touched files.

**Step 1: Run focused tests**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter RequiredField`

Expected: pass.

**Step 2: Run full test project**

Run: `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`

Expected: pass.

**Step 3: Review diff**

Run: `rtk git diff --check`

Expected: no whitespace errors.
