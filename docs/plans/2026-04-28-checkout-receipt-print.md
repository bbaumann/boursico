# Checkout Receipt Print Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add checkout slice 3: optional buyer address, sale persistence of that address, and browser receipt printing after a successful sale save.

**Architecture:** Keep `/caisse` as the checkout workflow surface. Extend `POST /api/ventes` with optional buyer address and keep business rules in `CheckoutService`. Use vanilla JavaScript to enable the print action, save first, render a print-only receipt, call `window.print()`, and reload after printing.

**Tech Stack:** ASP.NET Core Razor Pages, ASP.NET Core controllers, EF Core SQLite, vanilla JavaScript, CSS print media, xUnit.

---

### Task 1: Persist Receipt Details

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/CheckoutService.cs`
- Modify: `src/AssociationSaleTool.Web/Controllers/Models/CheckoutCartRequest.cs`
- Modify: `src/AssociationSaleTool.Web/Controllers/SalesController.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/CheckoutServiceTests.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Write failing service tests proving `SaveSaleAsync(itemIds, buyerAddress)` stores the trimmed buyer address and stores `null` for whitespace.
2. Write a failing HTTP test proving `POST /api/ventes` with `buyerAddress` persists the value on `sale.buyer_address`.
3. Run `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "CheckoutServiceTests|WebApplicationSmokeTests" -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-print-persistence`.
4. Add `BuyerAddress` to `CheckoutCartRequest`, pass it through `SalesController`, and update `CheckoutService.SaveSaleAsync` to normalize and persist it.
5. Rerun the filtered tests and commit `Add checkout receipt persistence`.

### Task 2: Checkout Receipt UI

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/wwwroot/js/site.js`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Steps:**
1. Write failing smoke/static tests for the French address label, `Enregistrer et imprimer`, receipt header data, print receipt hooks, `window.print()`, and print CSS.
2. Run the smoke tests with isolated output.
3. Inject `PrintingOptions` into the checkout page model and expose the receipt header text.
4. Add the optional buyer address text area, the disabled print-save button, and the print-only receipt container.
5. Extend JavaScript to enable the print-save button when buyer address is filled, send `buyerAddress` to `/api/ventes`, render the receipt, call `window.print()`, and reload after print.
6. Add print media CSS so the normal checkout UI is hidden and only the receipt is printed.
7. Rerun smoke tests and commit `Add checkout receipt print UI`.

### Task 3: Full Verification

**Files:**
- No code changes expected.

**Steps:**
1. Run `rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-print-full`.
2. Run `rtk git diff --check`.
3. Run `rtk git status --short`.
4. Confirm only known WAL and `.artifacts/` noise remains.
