# Post-Sale Reconciliation Dashboard Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build an admin-gated in-app reconciliation dashboard that prints seller sheets and lets admins mark returned items missing or restore mistakenly missing items to stock.

**Architecture:** Add server-rendered Razor Pages under `Pages/Reconciliation` with French routes. Put reconciliation queries and status changes in `ReconciliationService`, using small service model records for seller sheets and item search results. Protect the whole reconciliation area with a simple session-backed shared-password gate using the existing `AdminOptions`.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, SQLite, xUnit, existing French UI conventions.

---

### Task 1: Add Reconciliation Service Models

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/SellerSheet.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/SellerSheetItem.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/ReconciliationItemSearchResult.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/ReconciliationStatusChangeResult.cs`

**Step 1: Write the model files**

Create `SellerSheet.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerSheet(
    long SellerId,
    string SellerCode,
    string SellerName,
    string? Email,
    string? Phone,
    IReadOnlyList<SellerSheetItem> PayableItems,
    IReadOnlyList<SellerSheetItem> ReturnedItems,
    int TotalPayableCents);
```

Create `SellerSheetItem.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerSheetItem(
    long ItemId,
    string Reference,
    string Title,
    string Status,
    int PriceCents,
    bool IsMissing);
```

Create `ReconciliationItemSearchResult.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record ReconciliationItemSearchResult(
    long ItemId,
    long SellerId,
    string SellerCode,
    string SellerName,
    string Reference,
    string Title,
    string Status,
    int SellerPriceCents);
```

Create `ReconciliationStatusChangeResult.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public enum ReconciliationStatusChangeResult
{
    Changed,
    NotFound,
    NotAllowed
}
```

**Step 2: Build**

Run: `rtk dotnet build AssociationSaleTool.sln -p:UseAppHost=false`

Expected: build succeeds.

**Step 3: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Services/Models
rtk git commit -m "Add reconciliation service models"
```

### Task 2: Implement Seller Sheet Queries

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/ReconciliationService.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/ReconciliationServiceTests.cs`

**Step 1: Write failing tests**

Create `ReconciliationServiceTests.cs` with tests for:

- `ListSellerSheetsAsync` includes only sellers with at least one `sold`, `missing`, or `in_stock` item
- sold item sheet prices use `SaleLineEntity.SellerPriceAtSaleCents`
- missing item sheet prices use `ItemEntity.SellerPriceCents`
- `registered` items are excluded
- returned items contain only `in_stock`

Use an in-memory SQLite database like existing service tests.

Test skeleton:

```csharp
[Fact]
public async Task ListSellerSheetsAsync_UsesSoldSnapshotAndMissingCurrentPrice()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();

    var seller = new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" };
    context.Sellers.Add(seller);
    await context.SaveChangesAsync();

    var sold = new ItemEntity
    {
        SellerId = seller.Id,
        Reference = "AA-001",
        ItemNumber = 1,
        Title = "Train",
        SellerPriceCents = 9999,
        Status = "sold"
    };
    var missing = new ItemEntity
    {
        SellerId = seller.Id,
        Reference = "AA-002",
        ItemNumber = 2,
        Title = "Puzzle",
        SellerPriceCents = 700,
        Status = "missing"
    };

    context.Items.AddRange(sold, missing);
    await context.SaveChangesAsync();

    context.Sales.Add(new SaleEntity
    {
        SoldAt = DateTime.UtcNow,
        SubtotalSellerPriceCents = 500,
        CheckoutFeeCents = 50,
        TotalPaidCents = 550,
        SaleLines = [new SaleLineEntity { ItemId = sold.Id, SellerPriceAtSaleCents = 500 }]
    });
    await context.SaveChangesAsync();

    var service = new ReconciliationService(context);

    var sheet = Assert.Single(await service.ListSellerSheetsAsync());

    Assert.Equal(1200, sheet.TotalPayableCents);
}
```

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter ReconciliationServiceTests`

Expected: tests fail because service methods do not exist.

**Step 3: Implement service methods**

In `ReconciliationService.cs`, inject `AppDbContext` and add:

```csharp
public async Task<List<SellerSheet>> ListSellerSheetsAsync(
    string? sellerCode = null,
    CancellationToken cancellationToken = default)
```

Query sellers with items and sale lines:

```csharp
var activeStatuses = new[] { "sold", "missing", "in_stock" };
var normalizedSellerCode = sellerCode?.Trim().ToUpperInvariant();

var sellers = await dbContext.Sellers
    .AsNoTracking()
    .Include(x => x.Items)
    .ThenInclude(x => x.SaleLine)
    .Where(x => normalizedSellerCode == null || x.SellerCode == normalizedSellerCode)
    .Where(x => x.Items.Any(item => activeStatuses.Contains(item.Status)))
    .OrderBy(x => x.SellerCode)
    .ToListAsync(cancellationToken);
```

Build `SellerSheet` records:

- payable item if status is `sold` or `missing`
- returned item if status is `in_stock`
- sold price from `item.SaleLine!.SellerPriceAtSaleCents`
- missing price from `item.SellerPriceCents`
- returned item price from `item.SellerPriceCents`

Sort items by `ItemNumber`.

**Step 4: Run tests to verify they pass**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter ReconciliationServiceTests`

Expected: all reconciliation service sheet tests pass.

**Step 5: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Services/ReconciliationService.cs tests/AssociationSaleTool.Web.Tests/Services/ReconciliationServiceTests.cs
rtk git commit -m "Add reconciliation seller sheet queries"
```

### Task 3: Implement Item Search And Status Corrections

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/ReconciliationService.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/ReconciliationServiceTests.cs`

**Step 1: Write failing tests**

Add tests for:

- search finds items by partial reference
- search finds items by partial title
- search returns seller context
- `MarkItemMissingAsync` changes `in_stock` to `missing`
- `MarkItemInStockAsync` changes `missing` to `in_stock`
- registered, not-provided, and sold items are rejected

Example assertion:

```csharp
var result = await service.MarkItemMissingAsync(item.Id);

Assert.Equal(ReconciliationStatusChangeResult.Changed, result);
Assert.Equal("missing", (await context.Items.SingleAsync()).Status);
```

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter ReconciliationServiceTests`

Expected: tests fail because methods do not exist.

**Step 3: Implement search**

Add:

```csharp
public async Task<List<ReconciliationItemSearchResult>> SearchItemsAsync(
    string query,
    CancellationToken cancellationToken = default)
```

Rules:

- trim the query
- return empty list for blank query
- match `Reference` or `Title`
- include seller fields
- order by reference
- cap results at 25

**Step 4: Implement status changes**

Add:

```csharp
public async Task<ReconciliationStatusChangeResult> MarkItemMissingAsync(long itemId, CancellationToken cancellationToken = default)
public async Task<ReconciliationStatusChangeResult> MarkItemInStockAsync(long itemId, CancellationToken cancellationToken = default)
```

Rules:

- reload the item with tracking
- `MarkItemMissingAsync` only allows current status `in_stock`
- `MarkItemInStockAsync` only allows current status `missing`
- save changes only for allowed transitions

**Step 5: Run tests to verify they pass**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter ReconciliationServiceTests`

Expected: all reconciliation service tests pass.

**Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Services/ReconciliationService.cs tests/AssociationSaleTool.Web.Tests/Services/ReconciliationServiceTests.cs
rtk git commit -m "Add reconciliation item corrections"
```

### Task 4: Add Session-Backed Admin Gate

**Files:**
- Create: `src/AssociationSaleTool.Web/Infrastructure/ReconciliationAdminSession.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing smoke tests**

Add tests that:

- `GET /rapprochement` without session shows password form
- `GET /rapprochement/feuilles` without session redirects or shows the password gate

Expected French labels:

- `Mot de passe administrateur`
- `Déverrouiller`

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter WebApplicationSmokeTests`

Expected: route not found or labels absent.

**Step 3: Add session configuration**

In `Program.cs`:

```csharp
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
```

Before mapping endpoints:

```csharp
app.UseSession();
```

Place it after `app.UseRouting();` and before `app.UseAuthorization();`.

**Step 4: Add admin session helper**

Create:

```csharp
namespace AssociationSaleTool.Web.Infrastructure;

public static class ReconciliationAdminSession
{
    private const string SessionKey = "ReconciliationAdminUnlocked";

    public static bool IsUnlocked(HttpContext httpContext)
    {
        return httpContext.Session.GetString(SessionKey) == "true";
    }

    public static void Unlock(HttpContext httpContext)
    {
        httpContext.Session.SetString(SessionKey, "true");
    }
}
```

**Step 5: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter WebApplicationSmokeTests`

Expected: still fails until pages exist, but session wiring builds.

**Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Infrastructure/ReconciliationAdminSession.cs src/AssociationSaleTool.Web/Program.cs tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs
rtk git commit -m "Add reconciliation admin session gate"
```

### Task 5: Build The Reconciliation Dashboard Page

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Reconciliation/Index.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Reconciliation/Index.cshtml.cs`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing smoke tests**

Add tests for:

- `/rapprochement` renders the admin gate when locked
- posting the configured admin password unlocks the dashboard
- unlocked dashboard contains `Feuilles vendeurs`
- unlocked dashboard contains `Correction des articles`
- dashboard links to `/rapprochement/feuilles`

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter Reconciliation`

Expected: route/page tests fail.

**Step 3: Implement PageModel**

Inject:

- `ReconciliationService`
- `IOptions<AdminOptions>`

Page model properties:

- `bool IsUnlocked`
- `PasswordInput`
- `SheetInput`
- `SearchInput`
- `IReadOnlyList<ReconciliationItemSearchResult> SearchResults`
- `string? SuccessMessage`

Handlers:

- `OnGetAsync`
- `OnPostUnlockAsync`
- `OnPostSearchAsync`
- `OnPostMarkMissingAsync`
- `OnPostMarkInStockAsync`

Each non-unlock handler first checks `ReconciliationAdminSession.IsUnlocked(HttpContext)`. If locked, show the password gate.

**Step 4: Implement Razor page**

Use:

```razor
@page "/rapprochement"
```

Locked state:

- title `Rapprochement`
- password label `Mot de passe administrateur`
- submit button `Déverrouiller`

Unlocked state:

- section `Feuilles vendeurs`
- link/button to `/rapprochement/feuilles` labeled `Imprimer toutes les feuilles`
- seller code input `Code vendeur`
- button `Imprimer une feuille`
- section `Correction des articles`
- search input `Référence ou titre`
- button `Rechercher`
- result rows with French status labels
- action buttons `Marquer manquant` and `Remettre en stock`

**Step 5: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter Reconciliation`

Expected: dashboard tests pass.

**Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Reconciliation tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs
rtk git commit -m "Add reconciliation dashboard page"
```

### Task 6: Build Printable Seller Sheets

**Files:**
- Create: `src/AssociationSaleTool.Web/Pages/Reconciliation/Sheets.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Reconciliation/Sheets.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing tests**

Add smoke tests that:

- unlocked `GET /rapprochement/feuilles` renders active seller sheets
- sellers with only `registered` items are absent
- `GET /rapprochement/feuilles?codeVendeur=AA` renders only seller `AA`
- unknown seller code shows a French message

Expected French labels:

- `Feuille vendeur`
- `Articles à payer`
- `Articles à rendre`
- `Total à payer`
- `MANQUANT`

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter Reconciliation`

Expected: print page tests fail.

**Step 3: Implement PageModel**

Use route:

```razor
@page "/rapprochement/feuilles"
```

`OnGetAsync(string? codeVendeur, CancellationToken cancellationToken)`:

- require admin session
- call `ListSellerSheetsAsync(codeVendeur)`
- expose `Sheets`
- expose an error message if `codeVendeur` is provided and no sheet is returned

**Step 4: Implement print markup**

Render one `.seller-sheet-print-page` per sheet:

- seller code and name
- payable table
- returned table
- total amount formatted in EUR

Use existing price formatting style:

```csharp
@((item.PriceCents / 100m).ToString("0.00")) EUR
```

**Step 5: Add print CSS**

In `site.css`, add focused print rules:

```css
@media print {
  .no-print {
    display: none !important;
  }

  .seller-sheet-print-page {
    break-after: page;
    page-break-after: always;
  }

  .seller-sheet-print-page:last-child {
    break-after: auto;
    page-break-after: auto;
  }
}
```

**Step 6: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter Reconciliation`

Expected: print page tests pass.

**Step 7: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Reconciliation/Sheets.cshtml src/AssociationSaleTool.Web/Pages/Reconciliation/Sheets.cshtml.cs src/AssociationSaleTool.Web/wwwroot/css/site.css tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs
rtk git commit -m "Add printable reconciliation seller sheets"
```

### Task 7: Add Navigation And Route Coverage

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml`
- Modify: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing tests**

Add tests that the main navigation contains:

- `href="/rapprochement"`
- label `Rapprochement`

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter Navigation`

Expected: navigation test fails.

**Step 3: Add navigation link**

Follow the existing `_Layout.cshtml` navigation pattern and add a link to `/rapprochement`.

**Step 4: Run smoke tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter WebApplicationSmokeTests`

Expected: smoke tests pass.

**Step 5: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs
rtk git commit -m "Add reconciliation navigation"
```

### Task 8: Update Canonical Documentation

**Files:**
- Modify: `docs/README.md`
- Modify: `docs/architecture.md`
- Modify: `docs/features.md`
- Modify: `docs/item-status-lifecycle.md`
- Modify: `docs/process-and-failover.md`

**Step 1: Update docs**

Change wording from separate reconciliation app/script to in-app reconciliation feature.

Document:

- `/rapprochement` is admin-gated
- all seller sheets are printed from the reconciliation dashboard
- only sellers with `sold`, `missing`, or `in_stock` items get sheets
- missing status can be restored to `in_stock` by an admin only as a reconciliation correction

**Step 2: Search for stale wording**

Run: `rtk rg -n "separate reconciliation|script|terminal|missing -> in_stock|Reconciliation Tool" docs`

Expected: no stale contradiction remains, except historical decision wording if intentionally updated or superseded.

**Step 3: Commit**

```bash
rtk git add docs/README.md docs/architecture.md docs/features.md docs/item-status-lifecycle.md docs/process-and-failover.md
rtk git commit -m "Update docs for in-app reconciliation"
```

### Task 9: Final Verification

**Files:**
- No new files.

**Step 1: Run full test suite**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false`

Expected: all tests pass.

**Step 2: Run build**

Run: `rtk dotnet build AssociationSaleTool.sln -p:UseAppHost=false`

Expected: build succeeds.

**Step 3: Review git status**

Run: `rtk git status --short`

Expected: only known unrelated local database files may remain dirty.

**Step 4: Commit any final test/doc adjustments**

If final verification required small fixes:

```bash
rtk git add <changed-files>
rtk git commit -m "Finalize reconciliation dashboard"
```
