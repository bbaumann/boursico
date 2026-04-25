# Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first runnable ASP.NET Core foundation for the association sale tool with configuration binding, domain rules, EF Core SQLite persistence, initial migration, and automated tests.

**Architecture:** Create one .NET 8 Razor Pages web application plus one test project. Keep business rules in focused domain types and services, keep EF Core entities and mapping in the web project, and wire startup so SQLite is the only runtime database with one event per file.

**Tech Stack:** .NET 8, ASP.NET Core Razor Pages, EF Core, SQLite, xUnit

---

### Task 1: Scaffold the solution and baseline projects

**Files:**
- Create: `AssociationSaleTool.sln`
- Create: `src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj`
- Create: `src/AssociationSaleTool.Web/Program.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Index.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Index.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/_ViewImports.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/_ViewStart.cshtml`
- Create: `src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Create: `tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj`
- Create: `tests/AssociationSaleTool.Web.Tests/Smoke/PlaceholderSmokeTests.cs`

- [ ] **Step 1: Create the solution and projects**

```powershell
rtk dotnet new sln -n AssociationSaleTool
rtk dotnet new webapp -n AssociationSaleTool.Web -o src/AssociationSaleTool.Web --framework net8.0
rtk dotnet new xunit -n AssociationSaleTool.Web.Tests -o tests/AssociationSaleTool.Web.Tests --framework net8.0
rtk dotnet sln AssociationSaleTool.sln add src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj
rtk dotnet sln AssociationSaleTool.sln add tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj
rtk dotnet add tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj reference src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj
```

- [ ] **Step 2: Run the template projects once**

Run: `rtk dotnet test AssociationSaleTool.sln`
Expected: PASS with the default xUnit test project

- [ ] **Step 3: Replace the default home page with the project shell**

```cshtml
@page
@model IndexModel
@{
    ViewData["Title"] = "Association Sale Tool";
}

<section class="hero">
    <h1>Association Sale Tool</h1>
    <p>Foundation scaffold for intake, checkout, and reconciliation.</p>
</section>
```

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages;

public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
```

- [ ] **Step 4: Add a minimal layout and stylesheet**

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - Association Sale Tool</title>
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
</head>
<body>
    <main class="page-shell">
        @RenderBody()
    </main>
</body>
</html>
```

```css
:root {
    --bg: #f3f1ea;
    --panel: #fffdf8;
    --text: #1e1f1c;
    --accent: #0d5c63;
    --border: #d7d2c8;
}

html, body {
    margin: 0;
    padding: 0;
    background: linear-gradient(180deg, #f6f3eb 0%, #ece6d9 100%);
    color: var(--text);
    font-family: Georgia, "Times New Roman", serif;
}

.page-shell {
    max-width: 960px;
    margin: 0 auto;
    padding: 3rem 1.5rem;
}

.hero {
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 16px;
    padding: 2rem;
}
```

- [ ] **Step 5: Verify the scaffold still builds**

Run: `rtk dotnet test AssociationSaleTool.sln`
Expected: PASS

- [ ] **Step 6: Commit**

```powershell
rtk git add AssociationSaleTool.sln src tests
rtk git commit -m "Scaffold ASP.NET Core foundation"
```

### Task 2: Add configuration models and startup validation

**Files:**
- Create: `src/AssociationSaleTool.Web/Configuration/DatabaseOptions.cs`
- Create: `src/AssociationSaleTool.Web/Configuration/EventOptions.cs`
- Create: `src/AssociationSaleTool.Web/Configuration/AdminOptions.cs`
- Create: `src/AssociationSaleTool.Web/Configuration/PrintingOptions.cs`
- Modify: `src/AssociationSaleTool.Web/appsettings.json`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Configuration/OptionsBindingTests.cs`

- [ ] **Step 1: Write the failing configuration test**

```csharp
using AssociationSaleTool.Web.Configuration;
using Microsoft.Extensions.Configuration;

namespace AssociationSaleTool.Web.Tests.Configuration;

public class OptionsBindingTests
{
    [Fact]
    public void BindsCanonicalSections()
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = "data/event.db",
            ["Event:Name"] = "2026 Toy Sale",
            ["Admin:SharedPassword"] = "secret",
            ["Printing:ReceiptHeaderText"] = "Association Toy Sale"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        Assert.Equal("data/event.db", configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!.SqlitePath);
        Assert.Equal("2026 Toy Sale", configuration.GetSection(EventOptions.SectionName).Get<EventOptions>()!.Name);
        Assert.Equal("secret", configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>()!.SharedPassword);
        Assert.Equal("Association Toy Sale", configuration.GetSection(PrintingOptions.SectionName).Get<PrintingOptions>()!.ReceiptHeaderText);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter BindsCanonicalSections`
Expected: FAIL because the options types do not exist

- [ ] **Step 3: Add the options types and startup binding**

```csharp
namespace AssociationSaleTool.Web.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string SqlitePath { get; init; } = string.Empty;
}
```

```csharp
namespace AssociationSaleTool.Web.Configuration;

public sealed class EventOptions
{
    public const string SectionName = "Event";

    public string Name { get; init; } = string.Empty;
}
```

```csharp
namespace AssociationSaleTool.Web.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string SharedPassword { get; init; } = string.Empty;
}
```

```csharp
namespace AssociationSaleTool.Web.Configuration;

public sealed class PrintingOptions
{
    public const string SectionName = "Printing";

    public string ReceiptHeaderText { get; init; } = string.Empty;
}
```

```json
{
  "Database": {
    "SqlitePath": "data/association-sale-tool.db"
  },
  "Event": {
    "Name": "Association Toy Sale"
  },
  "Admin": {
    "SharedPassword": "change-me"
  },
  "Printing": {
    "ReceiptHeaderText": "Association Toy Sale"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 4: Validate required configuration at startup**

```csharp
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.SqlitePath), "Database:SqlitePath is required")
    .ValidateOnStart();
```

- [ ] **Step 5: Run the tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter BindsCanonicalSections`
Expected: PASS

- [ ] **Step 6: Commit**

```powershell
rtk git add src/AssociationSaleTool.Web/appsettings.json src/AssociationSaleTool.Web/Configuration tests/AssociationSaleTool.Web.Tests/Configuration
rtk git commit -m "Add configuration contract"
```

### Task 3: Implement domain rules and unit tests

**Files:**
- Create: `src/AssociationSaleTool.Web/Domain/Items/ItemStatus.cs`
- Create: `src/AssociationSaleTool.Web/Domain/Items/ItemStatusTransitions.cs`
- Create: `src/AssociationSaleTool.Web/Domain/Sellers/SellerCodeGenerator.cs`
- Create: `src/AssociationSaleTool.Web/Domain/Items/ItemReference.cs`
- Create: `src/AssociationSaleTool.Web/Domain/Pricing/PriceCalculator.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Domain/SellerCodeGeneratorTests.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Domain/ItemReferenceTests.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Domain/ItemStatusTransitionsTests.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Domain/PriceCalculatorTests.cs`

- [ ] **Step 1: Write the failing domain tests**

```csharp
[Fact]
public void GeneratesSellerCodesInSpreadOrder()
{
    Assert.Equal("AA", SellerCodeGenerator.GetCodeAt(0));
    Assert.Equal("BA", SellerCodeGenerator.GetCodeAt(1));
    Assert.Equal("ZA", SellerCodeGenerator.GetCodeAt(21));
    Assert.Equal("AB", SellerCodeGenerator.GetCodeAt(22));
}
```

```csharp
[Fact]
public void FormatsReferenceWithThreeDigitItemNumber()
{
    Assert.Equal("BK-014", ItemReference.Format("BK", 14));
}
```

```csharp
[Theory]
[InlineData(ItemStatus.Registered, ItemStatus.InStock, true)]
[InlineData(ItemStatus.Registered, ItemStatus.NotProvided, true)]
[InlineData(ItemStatus.InStock, ItemStatus.Sold, true)]
[InlineData(ItemStatus.InStock, ItemStatus.Missing, true)]
[InlineData(ItemStatus.Sold, ItemStatus.InStock, false)]
public void EnforcesDocumentedTransitions(ItemStatus from, ItemStatus to, bool expected)
{
    Assert.Equal(expected, ItemStatusTransitions.CanTransition(from, to));
}
```

```csharp
[Fact]
public void AppliesTwentyPercentMarginToSellerPrice()
{
    Assert.Equal(600, PriceCalculator.ToSalePriceCents(500));
}
```

- [ ] **Step 2: Run the targeted tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter Domain`
Expected: FAIL because the domain types do not exist

- [ ] **Step 3: Implement the domain types**

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

public enum ItemStatus
{
    Registered,
    InStock,
    NotProvided,
    Sold,
    Missing
}
```

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemStatusTransitions
{
    public static bool CanTransition(ItemStatus from, ItemStatus to)
    {
        return (from, to) switch
        {
            (ItemStatus.Registered, ItemStatus.InStock) => true,
            (ItemStatus.Registered, ItemStatus.NotProvided) => true,
            (ItemStatus.InStock, ItemStatus.Sold) => true,
            (ItemStatus.InStock, ItemStatus.Missing) => true,
            _ => false
        };
    }
}
```

```csharp
namespace AssociationSaleTool.Web.Domain.Sellers;

public static class SellerCodeGenerator
{
    private static readonly char[] SafeAlphabet = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'J', 'K', 'L', 'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'];

    public static string GetCodeAt(int index)
    {
        var first = SafeAlphabet[index % SafeAlphabet.Length];
        var second = SafeAlphabet[index / SafeAlphabet.Length];
        return string.Create(2, (first, second), static (span, value) =>
        {
            span[0] = value.first;
            span[1] = value.second;
        });
    }
}
```

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemReference
{
    public static string Format(string sellerCode, int itemNumber) => $"{sellerCode}-{itemNumber:000}";
}
```

```csharp
namespace AssociationSaleTool.Web.Domain.Pricing;

public static class PriceCalculator
{
    public static int ToSalePriceCents(int sellerPriceCents) => sellerPriceCents + (sellerPriceCents / 5);
}
```

- [ ] **Step 4: Run the domain tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter Domain`
Expected: PASS

- [ ] **Step 5: Commit**

```powershell
rtk git add src/AssociationSaleTool.Web/Domain tests/AssociationSaleTool.Web.Tests/Domain
rtk git commit -m "Add core domain rules"
```

### Task 4: Add EF Core SQLite persistence and the initial migration

**Files:**
- Modify: `src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj`
- Create: `src/AssociationSaleTool.Web/Data/AppDbContext.cs`
- Create: `src/AssociationSaleTool.Web/Data/Entities/SellerEntity.cs`
- Create: `src/AssociationSaleTool.Web/Data/Entities/ItemEntity.cs`
- Create: `src/AssociationSaleTool.Web/Data/Entities/SaleEntity.cs`
- Create: `src/AssociationSaleTool.Web/Data/Entities/SaleLineEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Create: `src/AssociationSaleTool.Web/Infrastructure/SqliteConnectionConfigurator.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs`
- Create: `src/AssociationSaleTool.Web/Migrations/*`

- [ ] **Step 1: Write the failing model test**

```csharp
using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Tests.Data;

public class AppDbContextModelTests
{
    [Fact]
    public void ItemReferenceHasUniqueIndex()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ItemEntity));
        Assert.Contains(entityType!.GetIndexes(), index => index.IsUnique && index.Properties.Any(property => property.Name == nameof(ItemEntity.Reference)));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter ItemReferenceHasUniqueIndex`
Expected: FAIL because the DbContext and entities do not exist

- [ ] **Step 3: Add EF Core packages and entities**

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.14">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.14" />
</ItemGroup>
```

```csharp
public sealed class ItemEntity
{
    public long Id { get; set; }
    public long SellerId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public int ItemNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SellerPriceCents { get; set; }
    public int SalePriceCents { get; set; }
    public string Status { get; set; } = string.Empty;
    public SellerEntity Seller { get; set; } = null!;
    public SaleLineEntity? SaleLine { get; set; }
}
```

- [ ] **Step 4: Configure the DbContext and register it**

```csharp
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var connectionString = $"Data Source={databaseOptions.SqlitePath}";

    options.UseSqlite(connectionString);
});
```

- [ ] **Step 5: Create the initial migration**

Run: `rtk dotnet ef migrations add InitialCreate --project src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj --startup-project src/AssociationSaleTool.Web/AssociationSaleTool.Web.csproj --output-dir Migrations`
Expected: PASS and new files under `src/AssociationSaleTool.Web/Migrations`

- [ ] **Step 6: Run the persistence tests**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter Data`
Expected: PASS

- [ ] **Step 7: Commit**

```powershell
rtk git add src/AssociationSaleTool.Web tests/AssociationSaleTool.Web.Tests/Data
rtk git commit -m "Add EF Core SQLite persistence"
```

### Task 5: Add startup initialization, placeholder services, and end-to-end verification

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Create: `src/AssociationSaleTool.Web/Services/CheckoutService.cs`
- Create: `src/AssociationSaleTool.Web/Services/ReconciliationService.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Create: `tests/AssociationSaleTool.Web.Tests/Smoke/WebApplicationSmokeTests.cs`

- [ ] **Step 1: Write the failing smoke test**

```csharp
using Microsoft.AspNetCore.Mvc.Testing;

namespace AssociationSaleTool.Web.Tests.Smoke;

public class WebApplicationSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebApplicationSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HomePageLoadsSuccessfully()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 2: Run the smoke test to verify it fails if startup is incomplete**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests/AssociationSaleTool.Web.Tests.csproj --filter HomePageLoadsSuccessfully`
Expected: FAIL until the app exposes a public `Program` type and complete startup wiring

- [ ] **Step 3: Register services and database initialization**

```csharp
builder.Services.AddScoped<IntakeService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<ReconciliationService>();
```

```csharp
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await dbContext.Database.MigrateAsync();
```

- [ ] **Step 4: Run the full solution verification**

Run: `rtk dotnet test AssociationSaleTool.sln`
Expected: PASS

- [ ] **Step 5: Commit**

```powershell
rtk git add src tests
rtk git commit -m "Complete foundation startup"
```
