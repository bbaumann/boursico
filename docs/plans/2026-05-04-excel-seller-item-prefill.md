# Excel Seller And Item Prefill Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a one-seller Excel prefill helper that fills a combined seller and item intake form, while normal save persists seller and item data in one transaction.

**Architecture:** Keep Excel parsing separate from persistence. Add a parser service that converts one `.xlsx` worksheet into form input, a combined intake save method in `IntakeService`, and short-lived session handoff for existing-seller redirects. Razor Pages remain server-rendered with French routes and labels.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, SQLite, session state, ClosedXML for `.xlsx` parsing, xUnit.

---

### Task 1: Add Combined Intake Request And Result Models

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateSellerWithItemsRequest.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateSellerWithItemsResult.cs`

**Step 1: Write the model files**

Create `CreateSellerWithItemsRequest.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerWithItemsRequest(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? Notes,
    IntakeMode IntakeMode,
    IReadOnlyList<CreateItemRowRequest> Rows);
```

Create `CreateSellerWithItemsResult.cs`:

```csharp
using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerWithItemsResult(
    CreateSellerWithItemsOutcome Outcome,
    SellerEntity? Seller,
    long? ExistingSellerId);

public enum CreateSellerWithItemsOutcome
{
    Created,
    ExistingSellerFound
}
```

**Step 2: Build**

Run: `rtk dotnet build AssociationSaleTool.sln -p:UseAppHost=false`

Expected: build succeeds.

**Step 3: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Services/Models/CreateSellerWithItemsRequest.cs src/AssociationSaleTool.Web/Services/Models/CreateSellerWithItemsResult.cs
rtk git commit -m "Add combined intake service models"
```

### Task 2: Add Intake Service Tests For Combined Save

**Files:**
- Modify: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`

**Step 1: Write failing tests**

Add tests for:

- `CreateSellerWithItemsAsync` creates seller and items in one save
- `EmailPreregistration` creates `registered` items
- `InPerson` creates `in_stock` items
- item references use the allocated seller code
- duplicate seller returns `ExistingSellerFound`
- invalid duplicate save does not create another seller

Example test shape:

```csharp
[Fact]
public async Task CreateSellerWithItemsAsync_CreatesSellerAndRegisteredItems()
{
    await using var context = CreateContext();
    var service = new IntakeService(context);

    var result = await service.CreateSellerWithItemsAsync(
        new CreateSellerWithItemsRequest(
            "Marie",
            "Durand",
            null,
            "0600000000",
            null,
            IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Puzzle", 500)]));

    Assert.Equal(CreateSellerWithItemsOutcome.Created, result.Outcome);
    var seller = Assert.Single(context.Sellers.Include(x => x.Items));
    Assert.Equal("AA", seller.SellerCode);
    var item = Assert.Single(seller.Items);
    Assert.Equal("AA-001", item.Reference);
    Assert.Equal("registered", item.Status);
}
```

**Step 2: Run tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter IntakeServiceTests`

Expected: tests fail because `CreateSellerWithItemsAsync` does not exist.

### Task 3: Implement Combined Save In IntakeService

**Files:**
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`

**Step 1: Add duplicate lookup helper**

Add a helper that normalizes first name, last name, and phone:

```csharp
private static string NormalizeForLookup(string? value)
{
    return string.Join(
        " ",
        (value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
```

**Step 2: Add existing seller lookup**

Add:

```csharp
public async Task<SellerEntity?> FindExistingSellerAsync(
    string firstName,
    string lastName,
    string? phone,
    CancellationToken cancellationToken = default)
```

Use exact normalized first and last name matching. If phone is present, require normalized phone to match too.

**Step 3: Add combined create method**

Add:

```csharp
public async Task<CreateSellerWithItemsResult> CreateSellerWithItemsAsync(
    CreateSellerWithItemsRequest request,
    CancellationToken cancellationToken = default)
```

Inside one transaction:

- re-check existing seller
- allocate seller code
- create seller
- create items with references
- save

Use existing `CreateItemBatchAsync` behavior as the reference for status and item-number assignment.

**Step 4: Add concurrency handling around seller code**

Keep first implementation simple but explicit:

- allocate from the current seller count
- save inside a transaction
- catch unique constraint failures on `seller_code`
- retry with the next code a small number of times or return a clear failure result if retries are exhausted

**Step 5: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter IntakeServiceTests`

Expected: intake service tests pass.

**Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Services/IntakeService.cs tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs
rtk git commit -m "Add combined seller item intake save"
```

### Task 4: Add Excel Parser Service

**Files:**
- Modify: `src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj`
- Create: `src/AssociationSaleTool.Web/Services/ExcelSellerItemParser.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/ExcelSellerItemParseResult.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/ExcelSellerItemParserTests.cs`

**Step 1: Add package**

Run:

```bash
rtk dotnet add src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj package ClosedXML
```

Expected: package reference is added.

**Step 2: Write parser result models**

Create result records for:

- success flag
- seller fields
- intake mode
- item rows
- French error messages

**Step 3: Write failing parser tests**

Build small `.xlsx` files in test memory with ClosedXML. Cover:

- valid workbook maps seller and rows
- blank item rows ignored
- missing seller name rejected
- missing `libellé` / `prix` headers rejected
- title without price rejected
- price without title rejected

**Step 4: Implement parser**

Implementation rules:

- accept `.xlsx` only
- require exactly one worksheet
- find labels by normalized text
- find item header row containing `libellé` and `prix`
- parse price values as whole or decimal euro amounts using existing euro parsing rules where possible
- return rows without saving anything

**Step 5: Run parser tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter ExcelSellerItemParserTests`

Expected: parser tests pass.

**Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj src/AssociationSaleTool.Web/Services/ExcelSellerItemParser.cs src/AssociationSaleTool.Web/Services/Models/ExcelSellerItemParseResult.cs tests/AssociationSaleTool.Web.Tests/Services/ExcelSellerItemParserTests.cs
rtk git commit -m "Add Excel seller item parser"
```

### Task 5: Add Pending Item Rows Session Handoff

**Files:**
- Create: `src/AssociationSaleTool.Web/Infrastructure/PendingSellerItemRowsSession.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Infrastructure/PendingSellerItemRowsSessionTests.cs`

**Step 1: Write failing tests**

Cover:

- pending rows can be stored for a seller id
- pending rows are consumed once
- consuming clears the session value

**Step 2: Implement helper**

Use JSON in session with a dedicated key. Store:

- seller id
- intake mode
- item rows
- alert message key

**Step 3: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter PendingSellerItemRowsSessionTests`

Expected: tests pass.

**Step 4: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Infrastructure/PendingSellerItemRowsSession.cs tests/AssociationSaleTool.Web.Tests/Infrastructure/PendingSellerItemRowsSessionTests.cs
rtk git commit -m "Add pending seller item rows session handoff"
```

### Task 6: Update Seller Create Page To Combined Form

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

**Step 1: Write failing page model tests**

Cover:

- successful manual save creates seller and item rows
- duplicate seller redirects to existing seller details with pending rows
- validation failure preserves seller and item fields

**Step 2: Update page model**

Add an input model containing:

- seller fields
- intake mode
- item rows

Reuse the existing row validation behavior from `DetailsModel`.

On normal post:

- validate rows
- call `CreateSellerWithItemsAsync`
- redirect to new seller details on success
- store pending rows and redirect to existing seller details on duplicate

**Step 3: Update Razor page**

Add the item table below seller fields and label the save button:

`Enregistrer le vendeur et les articles`

Keep all visible text French.

**Step 4: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellersPageModelTests`

Expected: seller page model tests pass.

**Step 5: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs
rtk git commit -m "Combine seller and item intake form"
```

### Task 7: Add Excel Upload To Seller Create Page

**Files:**
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`

**Step 1: Register parser service**

Add:

```csharp
builder.Services.AddScoped<ExcelSellerItemParser>();
```

**Step 2: Add upload handler**

Add a page handler such as:

```csharp
public async Task<IActionResult> OnPostImportExcelAsync(CancellationToken cancellationToken)
```

The handler:

- validates file presence and extension
- calls parser
- checks for existing seller
- redirects with pending rows if seller exists
- otherwise fills the create form input and returns `Page()`

**Step 3: Update Razor form**

Add a secondary multipart form or button area labeled:

- `Importer depuis Excel`
- `Fichier Excel`
- `Importer`

Do not combine upload and final save in one button.

**Step 4: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellersPageModelTests`

Expected: tests pass.

**Step 5: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Program.cs src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs
rtk git commit -m "Add Excel prefill to seller intake"
```

### Task 8: Populate Pending Rows On Existing Seller Details

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`

**Step 1: Write failing tests**

Cover:

- pending rows for matching seller id populate the item form
- alert message is shown
- pending rows are cleared after first display

**Step 2: Update page model**

On `OnGetAsync`, consume pending rows from session. If present for the current seller id:

- set `Input.IntakeMode`
- set `Input.Rows`
- set an alert property

**Step 3: Update Razor page**

Render the alert above `Ajouter des articles`:

`Ce vendeur existe déjà. Les articles du fichier Excel ont été ajoutés au formulaire ci-dessous. Vérifiez puis enregistrez le lot.`

**Step 4: Run tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellersPageModelTests`

Expected: tests pass.

**Step 5: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs
rtk git commit -m "Prefill existing seller item rows from Excel"
```

### Task 9: Update Documentation And Run Full Verification

**Files:**
- Modify: `docs/vnext-backlog.md`
- Modify: `docs/features.md`
- Modify: `docs/decisions.md`

**Step 1: Update docs**

Document:

- Excel import is a prefill helper, not a direct import workflow
- one workbook tab equals one seller intake
- seller and item save is one transaction
- existing sellers receive appended pending rows for operator review

Add a lightweight ADR for the decision.

**Step 2: Run full tests**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false
```

Expected: all tests pass.

**Step 3: Run build**

Run:

```bash
rtk dotnet build AssociationSaleTool.sln -p:UseAppHost=false
```

Expected: build succeeds with 0 errors.

**Step 4: Commit**

```bash
rtk git add docs/vnext-backlog.md docs/features.md docs/decisions.md
rtk git commit -m "Document Excel seller item prefill"
```

## Execution Notes

- Preserve existing user-facing French routes and labels.
- Do not persist anything during Excel upload.
- Do not silently update existing seller details from Excel.
- Keep seller code and item reference generation entirely server-side.
- Re-run duplicate seller detection on final save.
- If the current worktree is dirty, isolate implementation in a clean worktree before executing this plan.
