# Checkout Catalog Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the first checkout slice: a French `/caisse` page with an in-memory item catalog, live-filtered table, and local cart behavior only.

**Architecture:** Add a read-only catalog query to `CheckoutService`, render the catalog once in a Razor Page, and use vanilla JavaScript in `site.js` for local filtering and cart state. The page deliberately omits validation, sale persistence, confirmation, and print actions.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite, vanilla JavaScript, xUnit.

---

### Task 1: Checkout Catalog Service

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/CheckoutCatalogItem.cs`
- Modify: `src/AssociationSaleTool.Web/Services/CheckoutService.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/CheckoutServiceTests.cs`

**Step 1: Write the failing test**

Create a service test that seeds one `in_stock` item and one `registered` item. Assert `ListCatalogItemsAsync()` returns only the `in_stock` item, with id, reference, title, and seller price cents.

**Step 2: Run test to verify it fails**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter CheckoutServiceTests -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-catalog-task1
```

Expected: FAIL because `CheckoutService.ListCatalogItemsAsync` and `CheckoutCatalogItem` do not exist.

**Step 3: Write minimal implementation**

Add `CheckoutCatalogItem` as a sealed record with:

- `long Id`
- `string Reference`
- `string Title`
- `int SellerPriceCents`

Implement `CheckoutService(AppDbContext dbContext)` with `ListCatalogItemsAsync(CancellationToken)` querying `Items.AsNoTracking()`, filtering `Status == "in_stock"`, ordering by `Reference`, and projecting to the record.

**Step 4: Run test to verify it passes**

Run the same filtered test command. Expected: PASS.

**Step 5: Commit**

Commit service and test changes with message `Add checkout catalog service`.

### Task 2: French Checkout Page

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Checkout/Index.cshtml`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write the failing tests**

Add smoke tests that:

- GET `/caisse` succeeds
- decoded content contains French labels such as `Caisse`, `Rechercher un article`, `Panier`, `Sous-total`, `Frais de caisse`, and `Total`
- content contains `checkout-catalog-data` and `data-checkout-root`
- content does not contain seeded seller first name, last name, email, or phone
- content does not contain save, confirmation, or print action labels

**Step 2: Run tests to verify they fail**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "FullyQualifiedName~WebApplicationSmokeTests" -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-catalog-task2
```

Expected: FAIL because `/caisse` does not exist.

**Step 3: Write minimal implementation**

Add a `Checkout.IndexModel` page model that loads catalog items through `CheckoutService` on `OnGetAsync`. Serialize catalog items with camelCase JSON into a `CatalogJson` property.

Add `Index.cshtml` with `@page "/caisse"`, French labels, a `<script type="application/json" id="checkout-catalog-data">` containing the catalog JSON, and checkout hooks:

- `data-checkout-root`
- `data-checkout-search`
- `data-checkout-results`
- `data-checkout-cart`
- `data-checkout-empty-cart`
- `data-checkout-message`
- `data-checkout-subtotal`
- `data-checkout-fee`
- `data-checkout-total`

Do not render seller details, save, confirmation, or print controls.

**Step 4: Run tests to verify they pass**

Run the same smoke test command. Expected: PASS.

**Step 5: Commit**

Commit page and smoke test changes with message `Add checkout catalog page`.

### Task 3: Local Checkout Interaction

**Files:**
- Modify: `src/AssociationSaleTool.Web/wwwroot/js/site.js`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write the failing tests**

Add smoke/static-asset tests that assert:

- `/js/site.js` contains checkout initialization and duplicate-add message text in French
- `/css/site.css` contains checkout layout classes
- `/caisse` includes result rows rendered by JavaScript hooks, not seller data

**Step 2: Run tests to verify they fail**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj --filter "FullyQualifiedName~WebApplicationSmokeTests" -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-catalog-task3
```

Expected: FAIL because checkout JavaScript and CSS are not implemented.

**Step 3: Write minimal implementation**

Extend `site.js` so when `[data-checkout-root]` exists it:

- parses `checkout-catalog-data`
- renders a live-filtered results table from the in-memory catalog
- matches lowercase `reference + title`
- adds rows to an in-memory cart when a result button is clicked
- rejects duplicate item ids with `Cet article est deja dans le panier.`
- removes cart rows locally
- recalculates subtotal, 10 percent checkout fee rounded to nearest cent, and final total
- displays euro amounts with two decimal places and `EUR`

Extend `site.css` with checkout-specific layout, table, cart, total, and conflict-message styles using the existing visual language.

**Step 4: Run tests to verify they pass**

Run the same smoke/static-asset test command. Expected: PASS.

**Step 5: Commit**

Commit JavaScript, CSS, and test changes with message `Add local checkout cart interaction`.

### Task 4: Full Verification

**Files:**
- No code changes expected.

**Step 1: Run full test suite**

Run:

```powershell
rtk dotnet test tests\AssociationSaleTool.Web.Tests\AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false -p:OutputPath=C:\dev\association-sale-tool\.artifacts\checkout-catalog-full
```

Expected: all tests pass with 0 warnings.

**Step 2: Review git status**

Run:

```powershell
rtk git status --short
```

Expected: only intended source/test/docs changes are committed; known dirty database WAL, `.artifacts/`, and pre-existing untracked noise may remain.
