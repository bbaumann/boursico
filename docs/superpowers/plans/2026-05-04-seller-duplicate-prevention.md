# Seller Duplicate Prevention Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent accidental duplicate sellers on create and edit using a reusable duplicate-check service and blocking French UI messages.

**Architecture:** Add a focused `SellerDuplicateService` that owns normalization and matching. Enforce the rule through `IntakeService` result types so Razor Pages and future Excel intake cannot bypass it. Render structured duplicate matches on the seller create/edit pages without changing the database schema.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core SQLite, xUnit, existing `rtk dotnet test` workflow.

---

## File Structure

- Create `src/AssociationSaleTool.Web/Services/SellerDuplicateService.cs`
  - Owns seller duplicate normalization and matching.
- Create `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateCheckRequest.cs`
  - Request object with submitted seller identity and optional excluded seller id.
- Create `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatch.cs`
  - Structured duplicate display data returned to service/page callers.
- Create `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatchReason.cs`
  - Distinguishes name-and-phone matches from name-only submitted-phone-missing matches.
- Create `src/AssociationSaleTool.Web/Services/Models/CreateSellerResult.cs`
  - New create result type with created seller or duplicate matches.
- Create `src/AssociationSaleTool.Web/Services/Models/CreateSellerOutcome.cs`
  - Create result outcome enum.
- Replace `src/AssociationSaleTool.Web/Services/Models/UpdateSellerResult.cs`
  - Convert from the current enum to a structured result record plus `UpdateSellerOutcome`.
- Modify `src/AssociationSaleTool.Web/Services/IntakeService.cs`
  - Inject/use duplicate service, return structured results from create/update, keep phase locks.
- Modify `src/AssociationSaleTool.Web/Program.cs`
  - Register `SellerDuplicateService`.
- Modify `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
  - Handle duplicate create result and expose matches to the view.
- Modify `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
  - Render blocking duplicate match list in French.
- Modify `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml.cs`
  - Handle duplicate update result and expose matches to the view.
- Modify `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml`
  - Render blocking duplicate match list in French.
- Create `tests/AssociationSaleTool.Web.Tests/Services/SellerDuplicateServiceTests.cs`
  - Direct service coverage for normalization and matching.
- Modify `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`
  - Update create API callers and add service-boundary duplicate tests.
- Modify `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`
  - Update test setup for new create result and add duplicate create/edit page tests.
- Modify `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`
  - Add one rendered French duplicate-message smoke test.
- Modify `docs/vnext-backlog.md`
  - Mark duplicate seller prevention as implemented.
- Modify `docs/features.md`
  - Add duplicate prevention to active seller intake capabilities.

---

### Task 1: Add Seller Duplicate Service

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateCheckRequest.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatch.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatchReason.cs`
- Create: `src/AssociationSaleTool.Web/Services/SellerDuplicateService.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/SellerDuplicateServiceTests.cs`

- [ ] **Step 1: Write failing service tests**

Create `tests/AssociationSaleTool.Web.Tests/Services/SellerDuplicateServiceTests.cs`:

```csharp
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class SellerDuplicateServiceTests
{
    [Fact]
    public async Task FindDuplicateSellersAsync_MatchesNamesCaseSpacesAndAccents()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Émilie",
            LastName = "Du  Pont",
            Phone = "0123"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest(" emilie ", "du pont", "0123"));

        var match = Assert.Single(matches);
        Assert.Equal("AA", match.SellerCode);
        Assert.Equal(SellerDuplicateMatchReason.NameAndPhone, match.Reason);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_DoesNotCollapseHyphensOrSpaces()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Jean-Luc",
            LastName = "Martin",
            Phone = "0123"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Jean Luc", "Martin", "0123"));

        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_TrimsPhoneButKeepsFormattingMeaningful()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "01 02 03"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var trimmedPhoneMatches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", " 01 02 03 "));
        var compactPhoneMatches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", "010203"));

        Assert.Single(trimmedPhoneMatches);
        Assert.Empty(compactPhoneMatches);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_BlankSubmittedPhoneBlocksSameNameEvenWhenExistingPhoneIsPresent()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.Add(new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "0123"
        });
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", ""));

        var match = Assert.Single(matches);
        Assert.Equal(SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing, match.Reason);
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_ExcludesCurrentSellerAndOrdersMatchesBySellerCode()
    {
        await using var context = await CreateContextAsync();
        context.Sellers.AddRange(
            new SellerEntity { SellerCode = "CA", FirstName = "Ada", LastName = "Lovelace" },
            new SellerEntity { SellerCode = "AA", FirstName = "Ada", LastName = "Lovelace" },
            new SellerEntity { SellerCode = "BA", FirstName = "Ada", LastName = "Lovelace" });
        await context.SaveChangesAsync();
        var excludedSeller = await context.Sellers.SingleAsync(x => x.SellerCode == "BA");

        var service = new SellerDuplicateService(context);

        var matches = await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", null, excludedSeller.Id));

        Assert.Collection(
            matches,
            first => Assert.Equal("AA", first.SellerCode),
            second => Assert.Equal("CA", second.SellerCode));
    }

    [Fact]
    public async Task FindDuplicateSellersAsync_ReturnsItemCountsAndDisplayContext()
    {
        await using var context = await CreateContextAsync();
        var seller = new SellerEntity
        {
            SellerCode = "AA",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Phone = "0123",
            Items =
            [
                new ItemEntity { Reference = "AA-001", ItemNumber = 1, Title = "Toy", SellerPriceCents = 100, Status = "registered" },
                new ItemEntity { Reference = "AA-002", ItemNumber = 2, Title = "Puzzle", SellerPriceCents = 200, Status = "registered" }
            ]
        };
        context.Sellers.Add(seller);
        await context.SaveChangesAsync();

        var service = new SellerDuplicateService(context);

        var match = Assert.Single(await service.FindDuplicateSellersAsync(
            new SellerDuplicateCheckRequest("Ada", "Lovelace", "0123")));

        Assert.Equal(seller.Id, match.SellerId);
        Assert.Equal("Ada", match.FirstName);
        Assert.Equal("Lovelace", match.LastName);
        Assert.Equal("ada@example.com", match.Email);
        Assert.Equal("0123", match.Phone);
        Assert.Equal(2, match.ItemCount);
    }

    private static async Task<AppDbContext> CreateContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
```

- [ ] **Step 2: Run service tests to verify RED**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellerDuplicateServiceTests
```

Expected: fail because `SellerDuplicateService`, `SellerDuplicateCheckRequest`, `SellerDuplicateMatch`, and `SellerDuplicateMatchReason` do not exist.

- [ ] **Step 3: Add duplicate models**

Create `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateCheckRequest.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerDuplicateCheckRequest(
    string FirstName,
    string LastName,
    string? Phone,
    long? ExcludedSellerId = null);
```

Create `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatchReason.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public enum SellerDuplicateMatchReason
{
    NameAndPhone,
    NameOnlySubmittedPhoneMissing
}
```

Create `src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatch.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerDuplicateMatch(
    long SellerId,
    string SellerCode,
    string FirstName,
    string LastName,
    string? Phone,
    string? Email,
    int ItemCount,
    SellerDuplicateMatchReason Reason);
```

- [ ] **Step 4: Add duplicate service**

Create `src/AssociationSaleTool.Web/Services/SellerDuplicateService.cs`:

```csharp
using System.Globalization;
using System.Text;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SellerDuplicateService(AppDbContext dbContext)
{
    public async Task<IReadOnlyList<SellerDuplicateMatch>> FindDuplicateSellersAsync(
        SellerDuplicateCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var submittedFirstName = NormalizeName(request.FirstName);
        var submittedLastName = NormalizeName(request.LastName);
        var submittedPhone = NormalizePhone(request.Phone);
        var hasSubmittedPhone = !string.IsNullOrWhiteSpace(submittedPhone);

        var sellers = await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => request.ExcludedSellerId == null || x.Id != request.ExcludedSellerId.Value)
            .ToListAsync(cancellationToken);

        return sellers
            .Where(seller =>
                NormalizeName(seller.FirstName) == submittedFirstName
                && NormalizeName(seller.LastName) == submittedLastName
                && (!hasSubmittedPhone || NormalizePhone(seller.Phone) == submittedPhone))
            .OrderBy(seller => seller.SellerCode)
            .Select(seller => new SellerDuplicateMatch(
                seller.Id,
                seller.SellerCode,
                seller.FirstName,
                seller.LastName,
                seller.Phone,
                seller.Email,
                seller.Items.Count,
                hasSubmittedPhone
                    ? SellerDuplicateMatchReason.NameAndPhone
                    : SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing))
            .ToList();
    }

    private static string NormalizePhone(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string NormalizeName(string value)
    {
        var collapsedWhitespace = string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var lower = collapsedWhitespace.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(lower.Length);

        foreach (var character in lower)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
```

- [ ] **Step 5: Run service tests to verify GREEN**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellerDuplicateServiceTests
```

Expected: `SellerDuplicateServiceTests` pass.

- [ ] **Step 6: Commit service slice**

Run:

```bash
rtk git add src/AssociationSaleTool.Web/Services/SellerDuplicateService.cs src/AssociationSaleTool.Web/Services/Models/SellerDuplicateCheckRequest.cs src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatch.cs src/AssociationSaleTool.Web/Services/Models/SellerDuplicateMatchReason.cs tests/AssociationSaleTool.Web.Tests/Services/SellerDuplicateServiceTests.cs
rtk git commit -m "Add seller duplicate matching service"
```

Expected: commit succeeds.

---

### Task 2: Enforce Duplicate Checks In IntakeService

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateSellerOutcome.cs`
- Create: `src/AssociationSaleTool.Web/Services/Models/CreateSellerResult.cs`
- Modify: `src/AssociationSaleTool.Web/Services/Models/UpdateSellerResult.cs`
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`

- [ ] **Step 1: Write failing IntakeService tests**

Add these tests to `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`:

```csharp
[Fact]
public async Task CreateSellerAsync_ReturnsDuplicateResultAndDoesNotCreateSellerForSameNameAndPhone()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    var existing = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", null))).Seller!;

    var result = await service.CreateSellerAsync(new CreateSellerRequest(" ada ", "lovelace", null, "0123", null));

    Assert.Equal(CreateSellerOutcome.DuplicateFound, result.Outcome);
    Assert.Null(result.Seller);
    var match = Assert.Single(result.DuplicateMatches);
    Assert.Equal(existing.Id, match.SellerId);
    Assert.Equal(SellerDuplicateMatchReason.NameAndPhone, match.Reason);
    Assert.Equal(1, await context.Sellers.CountAsync());
}

[Fact]
public async Task CreateSellerAsync_ReturnsNameOnlyDuplicateWhenSubmittedPhoneIsBlank()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", null));

    var result = await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, null, null));

    Assert.Equal(CreateSellerOutcome.DuplicateFound, result.Outcome);
    Assert.Equal(SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing, Assert.Single(result.DuplicateMatches).Reason);
    Assert.Equal(1, await context.Sellers.CountAsync());
}

[Fact]
public async Task UpdateSellerAsync_ReturnsDuplicateResultAndDoesNotUpdateSeller()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    var first = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", null))).Seller!;
    var second = (await service.CreateSellerAsync(new CreateSellerRequest("Grace", "Hopper", null, "9999", null))).Seller!;

    var result = await service.UpdateSellerAsync(second.Id, new UpdateSellerRequest("Ada", "Lovelace", null, "0123", "Updated"));

    Assert.Equal(UpdateSellerOutcome.DuplicateFound, result.Outcome);
    Assert.Equal(first.Id, Assert.Single(result.DuplicateMatches).SellerId);
    var unchangedSecond = await context.Sellers.SingleAsync(x => x.Id == second.Id);
    Assert.Equal("Grace", unchangedSecond.FirstName);
    Assert.Equal("Hopper", unchangedSecond.LastName);
    Assert.Equal("9999", unchangedSecond.Phone);
}

[Fact]
public async Task UpdateSellerAsync_ExcludesCurrentSellerFromDuplicateCheck()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    var seller = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", null))).Seller!;

    var result = await service.UpdateSellerAsync(seller.Id, new UpdateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", "Updated"));

    Assert.Equal(UpdateSellerOutcome.Updated, result.Outcome);
    Assert.Empty(result.DuplicateMatches);
    Assert.Equal("ada@example.com", (await context.Sellers.SingleAsync()).Email);
}
```

Also update existing `CreateSellerAsync` test setup statements to use `.Seller!`, and update existing `UpdateSellerAsync` assertions to check `.Outcome`.

- [ ] **Step 2: Run IntakeService tests to verify RED**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter IntakeServiceTests
```

Expected: fail because `CreateSellerResult`, `CreateSellerOutcome`, structured `UpdateSellerResult`, and duplicate enforcement do not exist.

- [ ] **Step 3: Add create result models**

Create `src/AssociationSaleTool.Web/Services/Models/CreateSellerOutcome.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public enum CreateSellerOutcome
{
    Created,
    DuplicateFound
}
```

Create `src/AssociationSaleTool.Web/Services/Models/CreateSellerResult.cs`:

```csharp
using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerResult(
    CreateSellerOutcome Outcome,
    SellerEntity? Seller,
    IReadOnlyList<SellerDuplicateMatch> DuplicateMatches)
{
    public static CreateSellerResult Created(SellerEntity seller)
    {
        return new CreateSellerResult(CreateSellerOutcome.Created, seller, []);
    }

    public static CreateSellerResult DuplicateFound(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        return new CreateSellerResult(CreateSellerOutcome.DuplicateFound, null, matches);
    }
}
```

- [ ] **Step 4: Replace update result model**

Replace `src/AssociationSaleTool.Web/Services/Models/UpdateSellerResult.cs` with:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record UpdateSellerResult(
    UpdateSellerOutcome Outcome,
    IReadOnlyList<SellerDuplicateMatch> DuplicateMatches)
{
    public static UpdateSellerResult Updated { get; } = new(UpdateSellerOutcome.Updated, []);

    public static UpdateSellerResult NotFound { get; } = new(UpdateSellerOutcome.NotFound, []);

    public static UpdateSellerResult NotAllowed { get; } = new(UpdateSellerOutcome.NotAllowed, []);

    public static UpdateSellerResult DuplicateFound(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        return new UpdateSellerResult(UpdateSellerOutcome.DuplicateFound, matches);
    }
}

public enum UpdateSellerOutcome
{
    Updated,
    NotFound,
    NotAllowed,
    DuplicateFound
}
```

- [ ] **Step 5: Modify IntakeService**

Change constructor and create/update methods in `src/AssociationSaleTool.Web/Services/IntakeService.cs`:

```csharp
private readonly SellerDuplicateService _sellerDuplicateService;

public IntakeService(
    AppDbContext dbContext,
    SalePhaseService? salePhaseService = null,
    SellerDuplicateService? sellerDuplicateService = null)
{
    _dbContext = dbContext;
    _salePhaseService = salePhaseService ?? new SalePhaseService(dbContext);
    _sellerDuplicateService = sellerDuplicateService ?? new SellerDuplicateService(dbContext);
}
```

Change `CreateSellerAsync` signature and body:

```csharp
public async Task<CreateSellerResult> CreateSellerAsync(CreateSellerRequest request, CancellationToken cancellationToken = default)
{
    if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
    {
        throw new InvalidOperationException("Seller master data is locked outside presale.");
    }

    var duplicateMatches = await _sellerDuplicateService.FindDuplicateSellersAsync(
        new SellerDuplicateCheckRequest(request.FirstName, request.LastName, request.Phone),
        cancellationToken);
    if (duplicateMatches.Count > 0)
    {
        return CreateSellerResult.DuplicateFound(duplicateMatches);
    }

    var seller = new SellerEntity
    {
        SellerCode = SellerCodeGenerator.GetCodeAt(await _dbContext.Sellers.CountAsync(cancellationToken)),
        FirstName = request.FirstName.Trim(),
        LastName = request.LastName.Trim(),
        Email = NormalizeOptional(request.Email),
        Phone = NormalizeOptional(request.Phone),
        Notes = NormalizeOptional(request.Notes)
    };

    _dbContext.Sellers.Add(seller);
    await _dbContext.SaveChangesAsync(cancellationToken);
    return CreateSellerResult.Created(seller);
}
```

Change duplicate handling inside `UpdateSellerAsync` before assigning fields:

```csharp
var duplicateMatches = await _sellerDuplicateService.FindDuplicateSellersAsync(
    new SellerDuplicateCheckRequest(request.FirstName, request.LastName, request.Phone, sellerId),
    cancellationToken);
if (duplicateMatches.Count > 0)
{
    return UpdateSellerResult.DuplicateFound(duplicateMatches);
}
```

Keep existing `NotFound`, `NotAllowed`, and `Updated` returns using the new static result values.

- [ ] **Step 6: Register the duplicate service**

Add to `src/AssociationSaleTool.Web/Program.cs` near the other scoped services:

```csharp
builder.Services.AddScoped<SellerDuplicateService>();
```

- [ ] **Step 7: Update existing tests and run GREEN**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter IntakeServiceTests
```

Expected: `IntakeServiceTests` pass.

- [ ] **Step 8: Commit IntakeService enforcement**

Run:

```bash
rtk git add src/AssociationSaleTool.Web/Program.cs src/AssociationSaleTool.Web/Services/IntakeService.cs src/AssociationSaleTool.Web/Services/Models/CreateSellerOutcome.cs src/AssociationSaleTool.Web/Services/Models/CreateSellerResult.cs src/AssociationSaleTool.Web/Services/Models/UpdateSellerResult.cs tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs
rtk git commit -m "Enforce seller duplicate checks in intake service"
```

Expected: commit succeeds.

---

### Task 3: Render Duplicate Matches On Seller Create And Edit Pages

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`

- [ ] **Step 1: Write failing page-model tests**

Add tests to `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`:

```csharp
[Fact]
public async Task CreateSellerPage_Post_ReturnsPageWithDuplicateMatches()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    var existing = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", null))).Seller!;

    var pageModel = new CreateModel(service)
    {
        Input = new CreateModel.InputModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "0123"
        }
    };

    var result = await pageModel.OnPostAsync(CancellationToken.None);

    Assert.IsType<PageResult>(result);
    Assert.False(pageModel.ModelState.IsValid);
    var match = Assert.Single(pageModel.DuplicateMatches);
    Assert.Equal(existing.Id, match.SellerId);
    Assert.Equal("ada@example.com", match.Email);
    Assert.Equal(1, await context.Sellers.CountAsync());
}

[Fact]
public async Task CreateSellerPage_Post_UsesNameOnlyDuplicateMessageWhenSubmittedPhoneIsBlank()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", null, "0123", null));

    var pageModel = new CreateModel(service)
    {
        Input = new CreateModel.InputModel { FirstName = "Ada", LastName = "Lovelace" }
    };

    var result = await pageModel.OnPostAsync(CancellationToken.None);

    Assert.IsType<PageResult>(result);
    Assert.Contains(
        "Aucun téléphone n'a été saisi",
        pageModel.ModelState[string.Empty]!.Errors.Single().ErrorMessage);
}

[Fact]
public async Task SellerEditPage_Post_ReturnsPageWithDuplicateMatches()
{
    await using var connection = new SqliteConnection("Data Source=:memory:");
    await connection.OpenAsync();
    var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
    await using var context = new AppDbContext(options);
    await context.Database.EnsureCreatedAsync();
    var service = new IntakeService(context);
    var existing = (await service.CreateSellerAsync(new CreateSellerRequest("Ada", "Lovelace", "ada@example.com", "0123", null))).Seller!;
    var edited = (await service.CreateSellerAsync(new CreateSellerRequest("Grace", "Hopper", null, "9999", null))).Seller!;

    var pageModel = new SellerEditModel(service)
    {
        Input = new SellerEditModel.InputModel
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Phone = "0123"
        }
    };

    var result = await pageModel.OnPostAsync(edited.Id, CancellationToken.None);

    Assert.IsType<PageResult>(result);
    Assert.False(pageModel.ModelState.IsValid);
    var match = Assert.Single(pageModel.DuplicateMatches);
    Assert.Equal(existing.Id, match.SellerId);
}
```

- [ ] **Step 2: Run page tests to verify RED**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellersPageModelTests
```

Expected: fail because page models do not expose duplicate matches or handle duplicate result outcomes.

- [ ] **Step 3: Update Create page model**

In `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs`, add:

```csharp
public IReadOnlyList<SellerDuplicateMatch> DuplicateMatches { get; private set; } = [];
```

Change the save branch:

```csharp
var result = await intakeService.CreateSellerAsync(
    new CreateSellerRequest(Input.FirstName, Input.LastName, Input.Email, Input.Phone, Input.Notes),
    cancellationToken);

if (result.Outcome == CreateSellerOutcome.DuplicateFound)
{
    DuplicateMatches = result.DuplicateMatches;
    ModelState.AddModelError(string.Empty, BuildDuplicateMessage(DuplicateMatches));
    return Page();
}

return RedirectToPage("/Sellers/Details", new { id = result.Seller!.Id });
```

Add helper:

```csharp
private static string BuildDuplicateMessage(IReadOnlyList<SellerDuplicateMatch> matches)
{
    return matches.Any(x => x.Reason == SellerDuplicateMatchReason.NameOnlySubmittedPhoneMissing)
        ? "Un vendeur existe déjà avec les mêmes nom et prénom. Aucun téléphone n'a été saisi, vérifiez manuellement le vendeur existant ou ajoutez un téléphone distinct."
        : "Un vendeur existe déjà avec les mêmes nom, prénom et téléphone. Ouvrez le vendeur existant avant de continuer.";
}
```

- [ ] **Step 4: Update Edit page model**

In `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml.cs`, add:

```csharp
public IReadOnlyList<SellerDuplicateMatch> DuplicateMatches { get; private set; } = [];
```

Change result handling:

```csharp
if (result.Outcome == UpdateSellerOutcome.NotFound)
{
    return NotFound();
}

if (result.Outcome == UpdateSellerOutcome.NotAllowed)
{
    ModelState.AddModelError(string.Empty, "Les informations du vendeur ne peuvent pas être modifiées dans le statut actuel de la vente.");
    return Page();
}

if (result.Outcome == UpdateSellerOutcome.DuplicateFound)
{
    DuplicateMatches = result.DuplicateMatches;
    ModelState.AddModelError(string.Empty, BuildDuplicateMessage(DuplicateMatches));
    return Page();
}
```

Add the same `BuildDuplicateMessage` helper as the create page model.

- [ ] **Step 5: Render duplicate matches in Create view**

In `src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml`, after validation summary, add:

```cshtml
@if (Model.DuplicateMatches.Count > 0)
{
    <section class="duplicate-match-list" aria-label="Vendeurs existants">
        <h2>Vendeurs existants</h2>
        <ul>
        @foreach (var match in Model.DuplicateMatches)
        {
            <li>
                <strong>@match.SellerCode - @match.FirstName @match.LastName</strong>
                <span>@(match.Phone ?? "Sans téléphone")</span>
                <span>@(match.Email ?? "Sans e-mail")</span>
                <span>@match.ItemCount article@(match.ItemCount > 1 ? "s" : "")</span>
                <a class="button-link" asp-page="/Sellers/Details" asp-route-id="@match.SellerId">Ouvrir ce vendeur</a>
            </li>
        }
        </ul>
    </section>
}
```

- [ ] **Step 6: Render duplicate matches in Edit view**

Add the same block to `src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml` after validation summary.

- [ ] **Step 7: Run page tests to verify GREEN**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter SellersPageModelTests
```

Expected: `SellersPageModelTests` pass.

- [ ] **Step 8: Commit Razor page duplicate handling**

Run:

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml.cs src/AssociationSaleTool.Web/Pages/Sellers/Create.cshtml src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml.cs src/AssociationSaleTool.Web/Pages/Sellers/Edit.cshtml tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs
rtk git commit -m "Show duplicate seller matches on intake forms"
```

Expected: commit succeeds.

---

### Task 4: Add Rendered Smoke Coverage

**Files:**
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

- [ ] **Step 1: Write failing smoke test**

Add one rendered test to `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`:

```csharp
[Fact]
public async Task CreateSellerPostShowsDuplicateMatchesInFrench()
{
    var client = _factory.CreateClient();
    using (var scope = _factory.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Sellers.Add(new SellerEntity
        {
            SellerCode = "DU",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            Phone = "0123"
        });
        await dbContext.SaveChangesAsync();
    }

    var form = await GetPageWithVerificationTokenAsync(client, "/vendeurs/creer");
    var response = await client.PostAsync(
        "/vendeurs/creer",
        BuildFormContent(
            form,
            ("Input.FirstName", "Ada"),
            ("Input.LastName", "Lovelace"),
            ("Input.Phone", "0123")));

    response.EnsureSuccessStatusCode();
    var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    Assert.Contains("Un vendeur existe déjà avec les mêmes nom, prénom et téléphone", content);
    Assert.Contains("DU - Ada Lovelace", content);
    Assert.Contains("ada@example.com", content);
    Assert.Contains("Ouvrir ce vendeur", content);
}
```

Use existing helper methods in the smoke test file for antiforgery form posting. If the existing helper has a different name, use that exact existing helper rather than adding a second token parser.

- [ ] **Step 2: Run smoke test to verify RED**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter CreateSellerPostShowsDuplicateMatchesInFrench
```

Expected: fail until the rendered view has the duplicate match block wired correctly.

- [ ] **Step 3: Make the rendered test pass**

Adjust only the Razor markup or the helper usage needed for the test. Do not change matching behavior in this task.

- [ ] **Step 4: Run smoke test to verify GREEN**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter CreateSellerPostShowsDuplicateMatchesInFrench
```

Expected: the new smoke test passes.

- [ ] **Step 5: Commit smoke coverage**

Run:

```bash
rtk git add tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs
rtk git commit -m "Cover duplicate seller message rendering"
```

Expected: commit succeeds.

---

### Task 5: Update Documentation And Verify Full Suite

**Files:**
- Modify: `docs/vnext-backlog.md`
- Modify: `docs/features.md`

- [ ] **Step 1: Update backlog**

In `docs/vnext-backlog.md`, change the `Prevent Duplicate Sellers` section so it says:

```markdown
Status: implemented by the seller duplicate prevention slice.

Relevant design:

- [Seller Duplicate Prevention Design](./superpowers/specs/2026-05-04-seller-duplicate-prevention-design.md)

Implemented behavior:

- create and edit both block exact normalized duplicate sellers
- matching uses normalized first name and last name, with accent-insensitive comparison
- when a submitted phone is present, the trimmed phone must also match
- when a submitted phone is blank, same-name matches block and ask the operator to verify manually
- all duplicate matches are shown with seller code, contact context, item count, and a link to the existing seller
- existing historical duplicates are not cleaned up by this feature
- Excel prefill reuses the same duplicate-check service and redirects to the existing seller with pending item rows
```

- [ ] **Step 2: Update features**

In `docs/features.md`, add this bullet under Event Operations V1 capabilities near seller creation:

```markdown
- Prevent accidental duplicate sellers during seller creation and seller editing
```

- [ ] **Step 3: Run focused tests**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false --filter "SellerDuplicateServiceTests|IntakeServiceTests|SellersPageModelTests|CreateSellerPostShowsDuplicateMatchesInFrench"
```

Expected: focused tests pass.

- [ ] **Step 4: Run full test suite**

Run:

```bash
rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj -p:UseAppHost=false
```

Expected: all tests pass.

- [ ] **Step 5: Commit docs and final verification state**

Run:

```bash
rtk git add docs/vnext-backlog.md docs/features.md
rtk git commit -m "Document seller duplicate prevention"
```

Expected: commit succeeds.

- [ ] **Step 6: Final status check**

Run:

```bash
rtk git status --short
rtk git log -5 --oneline
```

Expected: only pre-existing local database/workbook files remain dirty or untracked; recent commits show the seller duplicate prevention implementation commits.
