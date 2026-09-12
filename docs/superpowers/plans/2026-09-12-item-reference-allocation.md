# Item Reference Allocation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the seller-derived item reference (`BK014`, stamped at item creation) with a letter-bucket reference (`B157`) allocated when a seller's deposit reaches the contract-print step, so adhesive label sheets can be printed before any seller is known.

**Architecture:** A pure `ReferenceAllocator` decides which letter a seller's run goes in, given how far each letter is already used. `IntakeService` performs the transactional allocation during the deposit fee step. The formatted reference string becomes the single source of truth: `item_number` is dropped, and `item.reference` becomes nullable, unique-when-not-null.

**Tech Stack:** .NET 8, ASP.NET Core Razor Pages, EF Core 8 with code-first migrations, SQLite, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-12-item-reference-label-sheets-design.md`

## Global Constraints

- All operator-facing UI labels, page titles, buttons, validation messages, helper text and routes must be in **French**. Internal identifiers stay English. (`AGENTS.md`)
- Prefix every shell command with `rtk`, including inside `&&` chains. (`AGENTS.md`)
- Reference format is `<LETTER><NNN>`, zero-padded to exactly 3 digits, e.g. `B157`. Zero padding is load-bearing: `ORDER BY reference` must match physical label order.
- Letters are the 20-character safe alphabet already in `SellerCodeGenerator`: `B C D E G H J K L M N P R S T U W X Y Z`.
- Soft capacity per letter: **455**, from `Labels:SoftCapacityPerLetter`.
- Hard format limit: **999**. Exceeding it throws; there is no operator-facing recovery.
- The application is **not live**. No data backfill is required by any migration.
- Never edit existing migration files or their `.Designer.cs`. Add new migrations only.
- The build must be green at the end of every task.

---

### Task 1: ReferenceAllocator domain logic

Pure letter-selection logic with no database and no wiring. Nothing else in the codebase changes, so the build stays green.

**Files:**
- Create: `src/AssociationSaleTool.Web/Domain/Items/ReferenceAllocator.cs`
- Create: `src/AssociationSaleTool.Web/Domain/Items/ReferenceSpaceExhaustedException.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Domain/ReferenceAllocatorTests.cs`

**Interfaces:**
- Consumes: `SellerCodeGenerator` (read-only, for the alphabet — do not modify it).
- Produces:
  - `ReferenceAllocator.SafeAlphabet` → `IReadOnlyList<char>`
  - `ReferenceAllocator.ChooseLetter(IReadOnlyDictionary<char,int> usedByLetter, char ownLetter, char? existingLetter, int count, int softCapacity)` → `char`
  - `ReferenceSpaceExhaustedException : InvalidOperationException`

- [ ] **Step 1: Write the failing tests**

Create `tests/AssociationSaleTool.Web.Tests/Domain/ReferenceAllocatorTests.cs`:

```csharp
using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class ReferenceAllocatorTests
{
    private const int Cap = 455;

    private static Dictionary<char, int> Empty() =>
        ReferenceAllocator.SafeAlphabet.ToDictionary(letter => letter, _ => 0);

    [Fact]
    public void ChooseLetter_UsesOwnLetterWhenItFits()
    {
        var used = Empty();
        used['K'] = 100;

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_UsesOwnLetterWhenItExactlyFits()
    {
        var used = Empty();
        used['K'] = 435;

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_SpillsToLeastLoadedLetterWhenOwnLetterIsFull()
    {
        var used = Empty();
        used['K'] = 450;
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 400;
        }

        used['K'] = 450;
        used['S'] = 10;
        used['T'] = 50;

        Assert.Equal('S', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_BreaksSpillTiesByAlphabetOrder()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 400;
        }

        used['K'] = 450;
        used['T'] = 10;
        used['S'] = 10;

        Assert.Equal('S', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_PrefersTheSellerExistingLetterOnTopUp()
    {
        var used = Empty();
        used['K'] = 100;
        used['M'] = 200;

        Assert.Equal('M', ReferenceAllocator.ChooseLetter(used, 'K', 'M', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_FallsBackToOwnLetterWhenExistingLetterIsFull()
    {
        var used = Empty();
        used['K'] = 100;
        used['M'] = 454;

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', 'M', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_RelaxesIntoOwnLetterWhenEveryLetterIsFull()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', null, 20, Cap));
    }

    [Fact]
    public void ChooseLetter_RelaxesIntoOwnLetterEvenWhenTheSellerHasAnExistingLetter()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        Assert.Equal('K', ReferenceAllocator.ChooseLetter(used, 'K', 'M', 5, Cap));
    }

    [Fact]
    public void ChooseLetter_ThrowsWhenAllocationWouldPassTheHardCap()
    {
        var used = Empty();
        foreach (var letter in ReferenceAllocator.SafeAlphabet)
        {
            used[letter] = 455;
        }

        used['K'] = 998;

        Assert.Throws<ReferenceSpaceExhaustedException>(
            () => ReferenceAllocator.ChooseLetter(used, 'K', null, 5, Cap));
    }

    [Fact]
    public void SafeAlphabet_MatchesTheSellerCodeAlphabet()
    {
        Assert.Equal(20, ReferenceAllocator.SafeAlphabet.Count);
        Assert.DoesNotContain('A', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('I', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('O', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('Q', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('V', ReferenceAllocator.SafeAlphabet);
        Assert.DoesNotContain('F', ReferenceAllocator.SafeAlphabet);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests --filter FullyQualifiedName~ReferenceAllocatorTests`
Expected: FAIL — `ReferenceAllocator` does not exist (compile error `CS0246`).

- [ ] **Step 3: Write the exception type**

Create `src/AssociationSaleTool.Web/Domain/Items/ReferenceSpaceExhaustedException.cs`:

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

/// <summary>
/// Thrown when a reference allocation would push an item number past the
/// three-digit format limit. Total capacity is 20 x 999 references against an
/// event of roughly 4,200 items, so this cannot occur in practice. The guard
/// exists only to prevent silent corruption of the reference format and of
/// reference sort order, and deliberately has no operator-facing recovery.
/// </summary>
public sealed class ReferenceSpaceExhaustedException : InvalidOperationException
{
    public ReferenceSpaceExhaustedException(char letter, int requestedLastNumber)
        : base($"Reference space exhausted for letter '{letter}': number {requestedLastNumber} exceeds 999.")
    {
        Letter = letter;
        RequestedLastNumber = requestedLastNumber;
    }

    public char Letter { get; }

    public int RequestedLastNumber { get; }
}
```

- [ ] **Step 4: Write the allocator**

Create `src/AssociationSaleTool.Web/Domain/Items/ReferenceAllocator.cs`:

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

/// <summary>
/// Decides which letter bucket a seller's contiguous reference run goes into.
/// The letter is a physical sort bucket for post-sale recollection, not a
/// seller identifier: twenty letters means twenty piles that twenty volunteers
/// can sort in parallel, so keeping them balanced is the point.
/// </summary>
public static class ReferenceAllocator
{
    public const int HardCap = 999;

    // Same alphabet as SellerCodeGenerator: 'A' is reserved for the
    // association, and F, I, O, Q, V are excluded as visually confusable.
    public static readonly IReadOnlyList<char> SafeAlphabet =
    [
        'B', 'C', 'D', 'E', 'G', 'H', 'J', 'K', 'L',
        'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'
    ];

    public static char ChooseLetter(
        IReadOnlyDictionary<char, int> usedByLetter,
        char ownLetter,
        char? existingLetter,
        int count,
        int softCapacity)
    {
        ArgumentNullException.ThrowIfNull(usedByLetter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var chosen = SelectLetter(usedByLetter, ownLetter, existingLetter, count, softCapacity);

        var lastNumber = Used(usedByLetter, chosen) + count;
        if (lastNumber > HardCap)
        {
            throw new ReferenceSpaceExhaustedException(chosen, lastNumber);
        }

        return chosen;
    }

    private static char SelectLetter(
        IReadOnlyDictionary<char, int> usedByLetter,
        char ownLetter,
        char? existingLetter,
        int count,
        int softCapacity)
    {
        // Top-up: keep the seller's items in one sort pile when they pull more
        // toys out of the bag part-way through the deposit.
        if (existingLetter is { } existing && Fits(usedByLetter, existing, count, softCapacity))
        {
            return existing;
        }

        if (Fits(usedByLetter, ownLetter, count, softCapacity))
        {
            return ownLetter;
        }

        // Spill: the least-loaded letter with room, which keeps the piles even.
        // Alphabet order breaks ties so the choice is deterministic. Ordering
        // by the position in SafeAlphabet rather than by the character itself
        // keeps the tie-break tied to the declared alphabet order.
        var spill = SafeAlphabet
            .Select((letter, index) => (Letter: letter, Index: index))
            .Where(candidate => Fits(usedByLetter, candidate.Letter, count, softCapacity))
            .OrderBy(candidate => Used(usedByLetter, candidate.Letter))
            .ThenBy(candidate => candidate.Index)
            .Select(candidate => (char?)candidate.Letter)
            .FirstOrDefault();

        // Relax: every letter is full, so the soft cap is ignored and the
        // seller goes into their own letter, onto labels printed on the day.
        return spill ?? ownLetter;
    }

    private static bool Fits(
        IReadOnlyDictionary<char, int> usedByLetter,
        char letter,
        int count,
        int softCapacity)
    {
        return Used(usedByLetter, letter) + count <= softCapacity;
    }

    private static int Used(IReadOnlyDictionary<char, int> usedByLetter, char letter)
    {
        return usedByLetter.TryGetValue(letter, out var used) ? used : 0;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests --filter FullyQualifiedName~ReferenceAllocatorTests`
Expected: PASS, 10 tests.

- [ ] **Step 6: Run the full suite to confirm nothing regressed**

Run: `rtk dotnet test`
Expected: PASS — this task is purely additive.

- [ ] **Step 7: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Domain/Items/ReferenceAllocator.cs src/AssociationSaleTool.Web/Domain/Items/ReferenceSpaceExhaustedException.cs tests/AssociationSaleTool.Web.Tests/Domain/ReferenceAllocatorTests.cs
rtk git commit -m "Add ReferenceAllocator letter-selection logic"
```

---

### Task 2: Stop generating references at item creation

Makes `item.reference` nullable and removes reference generation from item creation and CSV import. `item_number` is deliberately **left in place** for now so the ~100 test call sites keep compiling; Task 4 removes it. This keeps every task green.

**Files:**
- Modify: `src/AssociationSaleTool.Web/Data/Entities/ItemEntity.cs:9`
- Modify: `src/AssociationSaleTool.Web/Data/AppDbContext.cs:59`
- Modify: `src/AssociationSaleTool.Web/Domain/Items/ItemReference.cs`
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs:528`
- Modify: `src/AssociationSaleTool.Web/Services/SellerFormImportService.cs:109`
- Create: `src/AssociationSaleTool.Web/Migrations/20260912090000_MakeItemReferenceOptional.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Domain/ItemReferenceTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs:283,289,484,491,769`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/SellerFormImportServiceTests.cs:245`
- Test: `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs:95`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces:
  - `ItemEntity.Reference` → `string?` (null until deposit allocation)
  - `ItemReference.Format(char letter, int number)` → `string`, e.g. `Format('B', 157) == "B157"`

- [ ] **Step 1: Write the failing test for the new reference format**

Replace the body of `tests/AssociationSaleTool.Web.Tests/Domain/ItemReferenceTests.cs`:

```csharp
using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public class ItemReferenceTests
{
    [Fact]
    public void FormatsReferenceWithLetterAndThreeDigitNumber()
    {
        Assert.Equal("B157", ItemReference.Format('B', 157));
    }

    [Fact]
    public void PadsShortNumbersToThreeDigits()
    {
        Assert.Equal("B001", ItemReference.Format('B', 1));
        Assert.Equal("B009", ItemReference.Format('B', 9));
        Assert.Equal("B010", ItemReference.Format('B', 10));
    }

    [Fact]
    public void PaddingKeepsLexicographicOrderEqualToNumericOrder()
    {
        var ordered = new[] { 9, 10, 100, 455 }
            .Select(number => ItemReference.Format('B', number))
            .ToList();

        Assert.Equal(ordered, ordered.OrderBy(reference => reference, StringComparer.Ordinal).ToList());
    }
}
```

- [ ] **Step 2: Write the failing test for creation leaving references null**

Add to `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`:

```csharp
    [Fact]
    public async Task CreateItemBatchAsync_LeavesReferencesUnassignedUntilDeposit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await service.CreateSellerAsync(
            new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var created = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            [new CreateItemRowRequest("Puzzle", 500), new CreateItemRowRequest("Poupée", 300)]));

        Assert.All(created, item => Assert.Null(item.Reference));
        Assert.All(created, item => Assert.Equal("registered", item.Status));
    }
```

- [ ] **Step 3: Run both tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests --filter "FullyQualifiedName~ItemReferenceTests|FullyQualifiedName~CreateItemBatchAsync_LeavesReferencesUnassignedUntilDeposit"`
Expected: FAIL — `ItemReference.Format` has the wrong signature, and creation still assigns `BB001`.

- [ ] **Step 4: Change the reference format**

Replace `src/AssociationSaleTool.Web/Domain/Items/ItemReference.cs`:

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemReference
{
    /// <summary>
    /// Formats a reference as letter + three zero-padded digits, for example
    /// "B157". The padding is load-bearing: ORDER BY reference must produce the
    /// same order as the physical label sheets.
    /// </summary>
    public static string Format(char letter, int number) => $"{letter}{number:000}";
}
```

- [ ] **Step 5: Make the entity property nullable**

In `src/AssociationSaleTool.Web/Data/Entities/ItemEntity.cs`, change line 9 from:

```csharp
    public string Reference { get; set; } = string.Empty;
```

to:

```csharp
    /// <summary>
    /// Null until the seller's deposit reaches the contract-print step, when a
    /// contiguous run is allocated. An item without a reference is necessarily
    /// still "registered" under a "deposit_pending" seller, so it can never
    /// reach checkout.
    /// </summary>
    public string? Reference { get; set; }
```

- [ ] **Step 6: Relax the EF configuration**

In `src/AssociationSaleTool.Web/Data/AppDbContext.cs`, change line 59 from:

```csharp
            entity.Property(x => x.Reference).HasColumnName("reference").IsRequired();
```

to:

```csharp
            entity.Property(x => x.Reference).HasColumnName("reference");
```

Leave the `HasIndex(x => x.Reference).IsUnique()` on line 70 exactly as it is — SQLite treats `NULL`s as distinct, so it becomes unique-when-not-null with no change.

- [ ] **Step 7: Stop assigning references at creation**

In `src/AssociationSaleTool.Web/Services/IntakeService.cs`, inside `AddItemRows`, delete the `Reference` assignment on line 528 so the initializer reads:

```csharp
            var item = new ItemEntity
            {
                Seller = seller,
                SellerId = seller.Id,
                ItemNumber = nextItemNumber,
                Title = row.Title.Trim(),
                SellerPriceCents = row.SellerPriceCents,
                Status = "registered"
            };
```

In `src/AssociationSaleTool.Web/Services/SellerFormImportService.cs`, delete the `Reference` assignment on line 109 so the initializer keeps `ItemNumber = nextItemNumber` and drops the reference line.

- [ ] **Step 8: Write the migration**

Create `src/AssociationSaleTool.Web/Migrations/20260912090000_MakeItemReferenceOptional.cs`, following the table-rebuild pattern of `20260603194500_MakeItemSellerPriceOptional.cs`:

```csharp
using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260912090000_MakeItemReferenceOptional")]
    public partial class MakeItemReferenceOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE item_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents IS NULL OR seller_price_cents >= 0),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference),
                    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
                );

                INSERT INTO item_new (Id, seller_id, reference, item_number, title, seller_price_cents, status)
                SELECT Id, seller_id, reference, item_number, title, seller_price_cents, status
                FROM item;

                DROP TABLE item;

                ALTER TABLE item_new RENAME TO item;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE item_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NOT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents IS NULL OR seller_price_cents >= 0),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference),
                    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
                );

                INSERT INTO item_old (Id, seller_id, reference, item_number, title, seller_price_cents, status)
                SELECT Id, seller_id, COALESCE(reference, 'X' || printf('%03d', Id)), item_number, title, seller_price_cents, status
                FROM item;

                DROP TABLE item;

                ALTER TABLE item_old RENAME TO item;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
```

- [ ] **Step 9: Regenerate the model snapshot**

Run: `rtk dotnet ef migrations add MakeItemReferenceOptional --project src/AssociationSaleTool.Web --no-build` only if the snapshot is out of date after hand-writing the migration above. If EF creates a duplicate migration file, delete the generated `.cs` and keep only its `AppDbContextModelSnapshot.cs` changes plus the hand-written migration and its `.Designer.cs`.

Simpler alternative, preferred: hand-edit `src/AssociationSaleTool.Web/Migrations/AppDbContextModelSnapshot.cs` so the `Reference` property no longer carries `.IsRequired()`, and add a matching `20260912090000_MakeItemReferenceOptional.Designer.cs` copied from the neighbouring migration's designer with the same single change.

- [ ] **Step 10: Update the assertions that expected creation-time references**

- `IntakeServiceTests.cs:283` and `:289` — replace `Assert.Equal("BB001", first.Reference);` / `Assert.Equal("BB002", second.Reference);` with `Assert.Null(first.Reference);` / `Assert.Null(second.Reference);`
- `IntakeServiceTests.cs:484` and `:491` — same replacement. Rename the test at line 457 from `CreateItemBatchAsync_CreatesMultipleRegisteredItemsForSellerWithSequentialReferences` to `CreateItemBatchAsync_CreatesMultipleRegisteredItemsWithoutReferences`.
- `IntakeServiceTests.cs:769` — replace `Assert.Equal("BB001", updatedItem.Reference);` with `Assert.Null(updatedItem.Reference);`
- `SellerFormImportServiceTests.cs:245` — replace `Assert.Contains(updated.Items, item => item.Reference == "AA002" && item.Title == "Jeu PS5");` with `Assert.Contains(updated.Items, item => item.Reference is null && item.Title == "Jeu PS5");`
- `SellersPageModelTests.cs:95` — replace `Assert.Equal("BB001", item.Reference);` with `Assert.Null(item.Reference);`

Leave every test that sets `Reference = "AA-001"` or similar on a pre-seeded entity alone — those seed already-deposited items and stay valid.

- [ ] **Step 11: Run the full suite**

Run: `rtk dotnet test`
Expected: PASS.

- [ ] **Step 12: Commit**

```bash
rtk git add -A
rtk git commit -m "Make item reference optional and stop assigning it at creation"
```

---

### Task 3: Allocate references at the deposit contract step

The feature itself. After this task the workflow works end to end.

**Files:**
- Create: `src/AssociationSaleTool.Web/Configuration/LabelsOptions.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs:44` (after the `PrintingOptions` block)
- Modify: `src/AssociationSaleTool.Web/appsettings.json`
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs:262`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Configuration/OptionsBindingTests.cs`

**Interfaces:**
- Consumes: `ReferenceAllocator.ChooseLetter(...)` and `ItemReference.Format(char, int)` from Tasks 1 and 2.
- Produces:
  - `LabelsOptions.SectionName` → `"Labels"`, `LabelsOptions.SoftCapacityPerLetter` → `int`, default `455`
  - `IntakeService.AllocateDepositReferencesAsync(long sellerId, CancellationToken)` → `Task<SellerDepositStatusChangeResult>`

- [ ] **Step 1: Write the failing tests**

Add to `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`. `CreateValidatableSellerAsync` is a helper you add in this step — it creates a seller with every field `SellerCanBeValidated` requires:

```csharp
    private static async Task<SellerEntity> CreateValidatableSellerAsync(AppDbContext context, IntakeService service)
    {
        var seller = await service.CreateSellerAsync(
            new CreateSellerRequest("Ada", "Lovelace", null, "0123", "1 rue des Tests", null));

        var tracked = await context.Sellers.SingleAsync(x => x.Id == seller.Id);
        tracked.PostalCode = "38120";
        tracked.City = "SAINT-EGREVE";
        tracked.IdentityDocumentType = "CNI";
        tracked.IdentityDocumentNumber = "123456";
        tracked.IdentityDocumentIssuedOn = new DateOnly(2020, 1, 1);
        tracked.IdentityDocumentIssuer = "Mairie";
        await context.SaveChangesAsync();
        return tracked;
    }

    [Fact]
    public async Task AllocateDepositReferencesAsync_AssignsAContiguousRunInTheSellerOwnLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id,
            [new CreateItemRowRequest("Puzzle", 500), new CreateItemRowRequest("Poupée", 300)]));

        var result = await service.AllocateDepositReferencesAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
        var references = await context.Items
            .Where(x => x.SellerId == seller.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Reference)
            .ToListAsync();

        // Seller code is "BB", so the own letter is 'B'.
        Assert.Equal(["B001", "B002"], references);
    }

    [Fact]
    public async Task AllocateDepositReferencesAsync_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, [new CreateItemRowRequest("Puzzle", 500)]));

        await service.AllocateDepositReferencesAsync(seller.Id);
        await service.AllocateDepositReferencesAsync(seller.Id);

        var references = await context.Items
            .Where(x => x.SellerId == seller.Id)
            .Select(x => x.Reference)
            .ToListAsync();

        Assert.Equal(["B001"], references);
    }

    [Fact]
    public async Task AllocateDepositReferencesAsync_TopsUpItemsAddedAfterAFirstPass()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, [new CreateItemRowRequest("Puzzle", 500)]));
        await service.AllocateDepositReferencesAsync(seller.Id);

        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, [new CreateItemRowRequest("Train", 700)]));
        await service.AllocateDepositReferencesAsync(seller.Id);

        var references = await context.Items
            .Where(x => x.SellerId == seller.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Reference)
            .ToListAsync();

        Assert.Equal(["B001", "B002"], references);
    }

    [Fact]
    public async Task AllocateDepositReferencesAsync_RefusesWhenAnItemHasNoPrice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        context.Items.Add(new ItemEntity
        {
            SellerId = seller.Id,
            ItemNumber = 1,
            Title = "Sans prix",
            SellerPriceCents = null,
            Status = "registered"
        });
        await context.SaveChangesAsync();

        var result = await service.AllocateDepositReferencesAsync(seller.Id);

        Assert.Equal(SellerDepositStatusChangeResult.NotAllowed, result);
        Assert.All(await context.Items.ToListAsync(), item => Assert.Null(item.Reference));
    }

    [Fact]
    public async Task CancelSellerDepositValidationAsync_KeepsAllocatedReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, [new CreateItemRowRequest("Puzzle", 500)]));
        await service.AllocateDepositReferencesAsync(seller.Id);
        await service.ValidateSellerDepositAsync(seller.Id);

        await service.CancelSellerDepositValidationAsync(seller.Id);

        // The label is already stuck on the toy, so releasing the number would
        // be actively wrong.
        var item = await context.Items.SingleAsync(x => x.SellerId == seller.Id);
        Assert.Equal("B001", item.Reference);
        Assert.Equal("registered", item.Status);
    }

    [Fact]
    public async Task AllocateDepositReferencesAsync_AfterCancellationOnlyFillsTheNewItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, [new CreateItemRowRequest("Puzzle", 500)]));
        await service.AllocateDepositReferencesAsync(seller.Id);
        await service.ValidateSellerDepositAsync(seller.Id);
        await service.CancelSellerDepositValidationAsync(seller.Id);

        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, [new CreateItemRowRequest("Train", 700)]));
        await service.AllocateDepositReferencesAsync(seller.Id);

        var references = await context.Items
            .Where(x => x.SellerId == seller.Id)
            .OrderBy(x => x.Id)
            .Select(x => x.Reference)
            .ToListAsync();

        Assert.Equal(["B001", "B002"], references);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests --filter FullyQualifiedName~AllocateDepositReferencesAsync`
Expected: FAIL — `AllocateDepositReferencesAsync` does not exist.

- [ ] **Step 3: Add the configuration type**

Create `src/AssociationSaleTool.Web/Configuration/LabelsOptions.cs`:

```csharp
namespace AssociationSaleTool.Web.Configuration;

public sealed class LabelsOptions
{
    public const string SectionName = "Labels";

    /// <summary>
    /// How many references a letter may hold before allocation spills into
    /// another letter. 455 corresponds to the seven sheets of 65 adhesive
    /// labels the association prints per letter, but the application never
    /// reasons about sheets - only about this number.
    /// </summary>
    public int SoftCapacityPerLetter { get; init; } = 455;
}
```

- [ ] **Step 4: Bind the options**

In `src/AssociationSaleTool.Web/Program.cs`, after the `PrintingOptions` registration block ending near line 45, add:

```csharp
builder.Services
    .AddOptions<LabelsOptions>()
    .Bind(builder.Configuration.GetSection(LabelsOptions.SectionName));
```

In `src/AssociationSaleTool.Web/appsettings.json`, add a top-level section after `"Printing"`:

```json
  "Labels": {
    "SoftCapacityPerLetter": 455
  },
```

- [ ] **Step 5: Implement the allocation service method**

In `src/AssociationSaleTool.Web/Services/IntakeService.cs`, add the method next to `ValidateSellerDepositAsync`. The constructor gains an optional `LabelsOptions` so existing `new IntakeService(context)` test call sites keep compiling:

```csharp
    public async Task<SellerDepositStatusChangeResult> AllocateDepositReferencesAsync(
        long sellerId,
        CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return SellerDepositStatusChangeResult.NotFound;
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || seller.DepositStatus != SellerDepositStatuses.Pending
            || seller.Items.Count == 0
            || !SellerCanBeValidated(seller))
        {
            return SellerDepositStatusChangeResult.NotAllowed;
        }

        var pending = seller.Items
            .Where(item => item.Reference is null)
            .OrderBy(item => item.Id)
            .ToList();
        if (pending.Count == 0)
        {
            return SellerDepositStatusChangeResult.Changed;
        }

        // Read every letter's high-water mark inside the same transaction that
        // writes the new references. SQLite serializes writers, so two deposit
        // desks finalizing at once cannot obtain overlapping runs.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // One round trip rather than twenty: at event scale this is a few
        // thousand short strings. Fixed-width zero padding makes the
        // lexicographic maximum equal the numeric one, so no separate numeric
        // column is needed.
        var allocated = await _dbContext.Items
            .Where(x => x.Reference != null)
            .Select(x => x.Reference!)
            .ToListAsync(cancellationToken);

        var usedByLetter = ReferenceAllocator.SafeAlphabet.ToDictionary(
            letter => letter,
            letter => allocated
                .Where(reference => reference[0] == letter)
                .Select(reference => int.Parse(reference[1..]))
                .DefaultIfEmpty(0)
                .Max());

        var ownLetter = seller.SellerCode[0];
        var existingLetter = seller.Items
            .Where(item => item.Reference is not null)
            .OrderByDescending(item => item.Id)
            .Select(item => (char?)item.Reference![0])
            .FirstOrDefault();

        var chosen = ReferenceAllocator.ChooseLetter(
            usedByLetter,
            ownLetter,
            existingLetter,
            pending.Count,
            _labelsOptions.SoftCapacityPerLetter);

        var next = usedByLetter[chosen];
        foreach (var item in pending)
        {
            next++;
            item.Reference = ItemReference.Format(chosen, next);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SellerDepositStatusChangeResult.Changed;
    }
```

Add the field and constructor parameter, defaulting so existing call sites are unaffected:

```csharp
    private readonly LabelsOptions _labelsOptions;
```

and in the constructor parameter list add `LabelsOptions? labelsOptions = null`, with `_labelsOptions = labelsOptions ?? new LabelsOptions();` in the body.

Add `using AssociationSaleTool.Web.Configuration;` and `using AssociationSaleTool.Web.Domain.Items;` to the file's usings if not already present.

- [ ] **Step 6: Run the service tests to verify they pass**

Run: `rtk dotnet test tests/AssociationSaleTool.Web.Tests --filter FullyQualifiedName~IntakeServiceTests`
Expected: PASS, including the six new allocation tests.

- [ ] **Step 7: Wire allocation into the deposit fee handler**

In `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`, inside `OnPostUpdateDepositFeeAsync`, after the `UpdateSellerFeePaidAsync` result checks and **before** the final `GetSellerWithItemsAsync` reload near line 262, insert:

```csharp
        var allocation = await intakeService.AllocateDepositReferencesAsync(id, cancellationToken);
        if (allocation == SellerDepositStatusChangeResult.NotFound)
        {
            return NotFound();
        }

        if (allocation == SellerDepositStatusChangeResult.NotAllowed)
        {
            ModelState.AddModelError(
                string.Empty,
                "Les références des articles n'ont pas pu être attribuées. Vérifiez la fiche vendeur et les articles.");
            return Page();
        }
```

Placing it here means the earlier `DepositValidationMessages` early-return has already rejected incomplete sellers, so references are never burned on a deposit that cannot proceed.

- [ ] **Step 8: Show a placeholder for unreferenced items**

`src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml:269` currently reads:

```razor
                        <td>@item.Reference</td>
```

Replace it with:

```razor
                        <td>@(string.IsNullOrEmpty(item.Reference) ? "—" : item.Reference)</td>
```

`src/AssociationSaleTool.Web/Pages/Data/Index.cshtml:82` currently reads:

```razor
                            <td data-data-list-sort-value="@item.Reference"><a href="/vendeurs/@item.SellerId">@item.Reference</a></td>
```

Replace it with:

```razor
                            <td data-data-list-sort-value="@item.Reference"><a href="/vendeurs/@item.SellerId">@(string.IsNullOrEmpty(item.Reference) ? "—" : item.Reference)</a></td>
```

Leave `src/AssociationSaleTool.Web/Pages/Sellers/DepositContract.cshtml:112` unchanged: by the time the contract is printable, allocation has already run, so `item.Reference` is never null there.

- [ ] **Step 9: Add the options-binding test**

Add to `tests/AssociationSaleTool.Web.Tests/Configuration/OptionsBindingTests.cs`, following the existing tests in that file:

```csharp
    [Fact]
    public void LabelsOptions_DefaultsToSevenSheetsOfSixtyFive()
    {
        Assert.Equal(455, new LabelsOptions().SoftCapacityPerLetter);
    }
```

- [ ] **Step 10: Run the full suite**

Run: `rtk dotnet test`
Expected: PASS.

- [ ] **Step 11: Commit**

```bash
rtk git add -A
rtk git commit -m "Allocate item references at the deposit contract step"
```

---

### Task 4: Drop the item_number column

Mechanical cleanup, isolated so the feature work above is reviewable on its own. `item_number` has no remaining readers once the orderings move.

**Files:**
- Modify: `src/AssociationSaleTool.Web/Data/Entities/ItemEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/AppDbContext.cs:60,72`
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs:50,204-208,516-529`
- Modify: `src/AssociationSaleTool.Web/Services/SellerFormImportService.cs:99-110`
- Modify: `src/AssociationSaleTool.Web/Services/ReconciliationService.cs:108,113`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/DepositContract.cshtml.cs:45`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs:333`
- Create: `src/AssociationSaleTool.Web/Migrations/20260912100000_DropItemNumber.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs:202`
- Test: all files listed by the grep in Step 1 (66 sites in `WebApplicationSmokeTests.cs` alone)

**Interfaces:**
- Consumes: everything from Tasks 1-3.
- Produces: `ItemEntity` without `ItemNumber`; `item` table without `item_number`.

- [ ] **Step 1: Inventory every call site**

Run: `rtk git grep -n "ItemNumber" -- "*.cs" "*.cshtml" ":!src/AssociationSaleTool.Web/Migrations"`
Expected: ~129 hits outside `Migrations/`. Historical migration files and their `.Designer.cs` are **never** edited.

- [ ] **Step 2: Replace the orderings in production code**

Seller-facing lists order by entry order, which `Id` preserves and which stays correct while references are still null:

- `IntakeService.cs:50` — `.Include(x => x.Items.OrderBy(item => item.ItemNumber))` becomes `.Include(x => x.Items.OrderBy(item => item.Id))`
- `DepositContract.cshtml.cs:45` — `seller.Items.OrderBy(x => x.ItemNumber)` becomes `seller.Items.OrderBy(x => x.Id)`
- `Details.cshtml.cs:333` — same replacement

Seller sheets are read in physical label order, and every item on one has a reference by definition:

- `ReconciliationService.cs:108` and `:113` — `.OrderBy(item => item.ItemNumber)` becomes `.OrderBy(item => item.Reference)`

- [ ] **Step 3: Remove the numbering logic**

In `IntakeService.cs`, delete the `nextItemNumber` lookup at lines 204-208 and pass only `seller` and `request.Rows` to `AddItemRows`. Change `AddItemRows` to drop its `currentMaxItemNumber` parameter, its `nextItemNumber` local, and the `ItemNumber` assignment, so the initializer becomes:

```csharp
            var item = new ItemEntity
            {
                Seller = seller,
                SellerId = seller.Id,
                Title = row.Title.Trim(),
                SellerPriceCents = row.SellerPriceCents,
                Status = "registered"
            };
```

In `SellerFormImportService.cs`, delete the `nextItemNumber` computation at lines 99-104 and the `ItemNumber` assignment at line 110.

- [ ] **Step 4: Remove the property and its mapping**

Delete `public int ItemNumber { get; set; }` from `ItemEntity.cs`.

In `AppDbContext.cs`, delete line 60 (`entity.Property(x => x.ItemNumber)...`) and lines 72-73 (`entity.HasIndex(x => new { x.SellerId, x.ItemNumber }).IsUnique();`).

- [ ] **Step 5: Strip ItemNumber from the test suite**

Delete every `ItemNumber = <n>,` line from the object initializers in the test files found in Step 1. They are pure noise once the column is gone — no assertion depends on the value.

In `AppDbContextModelTests.cs:202`, delete the assertion expecting the `(SellerId, ItemNumber)` unique index. Verify the surrounding test still asserts the unique index on `Reference`; if it does not, add:

```csharp
        Assert.Contains(
            itemIndexes,
            index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual([nameof(ItemEntity.Reference)]));
```

In `SellersPageModelTests.cs:245`, change `context.Items.OrderBy(x => x.ItemNumber)` to `context.Items.OrderBy(x => x.Id)`.

- [ ] **Step 6: Write the migration**

Create `src/AssociationSaleTool.Web/Migrations/20260912100000_DropItemNumber.cs`:

```csharp
using AssociationSaleTool.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssociationSaleTool.Web.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260912100000_DropItemNumber")]
    public partial class DropItemNumber : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE item_new (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents IS NULL OR seller_price_cents >= 0),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference)
                );

                INSERT INTO item_new (Id, seller_id, reference, title, seller_price_cents, status)
                SELECT Id, seller_id, reference, title, seller_price_cents, status
                FROM item;

                DROP TABLE item;

                ALTER TABLE item_new RENAME TO item;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = OFF;

                CREATE TABLE item_old (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    seller_id INTEGER NOT NULL,
                    reference TEXT NULL,
                    item_number INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    seller_price_cents INTEGER,
                    status TEXT NOT NULL,
                    CONSTRAINT ck_item_status
                        CHECK (status IN ('registered', 'in_stock', 'sold', 'missing', 'defective')),
                    CONSTRAINT ck_item_seller_price_cents
                        CHECK (seller_price_cents IS NULL OR seller_price_cents >= 0),
                    CONSTRAINT FK_item_seller_seller_id
                        FOREIGN KEY (seller_id)
                        REFERENCES seller(Id)
                        ON DELETE RESTRICT,
                    CONSTRAINT uq_item_reference UNIQUE (reference),
                    CONSTRAINT uq_item_seller_number UNIQUE (seller_id, item_number)
                );

                INSERT INTO item_old (Id, seller_id, reference, item_number, title, seller_price_cents, status)
                SELECT Id,
                       seller_id,
                       reference,
                       ROW_NUMBER() OVER (PARTITION BY seller_id ORDER BY Id),
                       title,
                       seller_price_cents,
                       status
                FROM item;

                DROP TABLE item;

                ALTER TABLE item_old RENAME TO item;

                CREATE UNIQUE INDEX IX_item_reference ON item (reference);
                CREATE INDEX IX_item_seller_id ON item (seller_id);
                CREATE UNIQUE INDEX IX_item_seller_id_item_number ON item (seller_id, item_number);
                CREATE INDEX IX_item_status ON item (status);
                CREATE INDEX IX_item_title ON item (title);

                PRAGMA foreign_keys = ON;
                """,
                suppressTransaction: true);
        }
    }
}
```

`Down` regenerates `item_number` with `ROW_NUMBER()` per seller rather than preserving the original values, because they are gone by then. That satisfies the old unique constraint, which is all a rollback needs.

- [ ] **Step 7: Update the model snapshot**

Hand-edit `AppDbContextModelSnapshot.cs` to remove the `ItemNumber` property and the `SellerId, ItemNumber` index from the `ItemEntity` block, and add the matching `.Designer.cs` for the new migration.

- [ ] **Step 8: Verify the build compiles**

Run: `rtk dotnet build`
Expected: 0 errors. If any `ItemNumber` reference remains, Step 1's grep missed it — re-run it.

- [ ] **Step 9: Run the full suite**

Run: `rtk dotnet test`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
rtk git add -A
rtk git commit -m "Drop the item_number column"
```

---

### Task 5: Update the canonical documentation

`docs/README.md` declares `docs/` the canonical source of truth for implementation, so leaving it describing the old scheme would mislead every future reader and agent session.

**Files:**
- Modify: `docs/domain-model-and-references.md`
- Modify: `docs/database-schema.md`
- Modify: `docs/sql/sqlite.sql`
- Modify: `docs/decisions.md`
- Modify: `docs/features.md`
- Modify: `docs/item-status-lifecycle.md`
- Modify: `docs/guide-utilisateur.md`
- Modify: `docs/use-cases-and-personas.md`
- Modify: `docs/scenarios-recette-utilisateur.md`

- [ ] **Step 1: Rewrite the reference scheme**

In `docs/domain-model-and-references.md`, replace the seller-centered reference section with the letter-bucket scheme: format `<LETTER><NNN>`, the 20-letter alphabet, 455 soft capacity, allocation at the deposit contract step, and the sort-bucket rationale. State plainly that the reference carries no seller information.

- [ ] **Step 2: Update the schema documents**

In `docs/database-schema.md`: `reference` is nullable and unique-when-not-null; `item_number` is removed from the Columns, Nullability, Keys And Constraints, Uniqueness Constraints and Index Plan sections; rewrite the Reference Generation section to describe letter allocation.

In `docs/sql/sqlite.sql`: change `reference TEXT NOT NULL` to `reference TEXT NULL`, delete the `item_number` column, delete `uq_item_seller_number`, and delete the `IX_item_seller_id_item_number` index.

- [ ] **Step 3: Supersede the affected decisions**

In `docs/decisions.md`, add a superseding note to the top of **D-014 Use Seller-Centered Item References** and **D-017 Item Numbers Are Fixed-Width Per-Seller Sequences**, in the style the file already uses, pointing at the new record. Then append:

```markdown
## D-050 Allocate Letter-Bucket Item References At Deposit

Item references are `<LETTER><NNN>` allocated when a seller's deposit reaches
the contract-print step, not derived from the seller code at item creation.

This lets the association print all adhesive label sheets before any seller is
known, which is the only way the labels can exist on the day.

The letter is a physical sort bucket for post-sale recollection, not a seller
identifier. Volunteers sort unsold items by letter then number into twenty
piles, so the letters must stay balanced; balance falls out of the existing
spread order in `SellerCodeGenerator`, whose first letter cycles every twenty
sellers.

Allocating at deposit rather than at item creation removes the need for a
numbering buffer: by the time references are assigned, the seller's item list
is final, so each seller's run is contiguous by construction.

Supersedes [D-014](#d-014-use-seller-centered-item-references) and
[D-017](#d-017-item-numbers-are-fixed-width-per-seller-sequences).
```

- [ ] **Step 4: Update the feature and lifecycle documents**

In `docs/features.md`, change "Assign one unique reference per item" to state that references are assigned when the deposit reaches the contract step, and add the reference step to the Seller Deposit Validation walkthrough.

In `docs/item-status-lifecycle.md`, remove `item_number` from the frozen-fields list and note that `reference` is null until deposit allocation.

- [ ] **Step 5: Update the French operator guide**

In `docs/guide-utilisateur.md`, in the `Valider le dépôt vendeur` section, add that references are assigned at the `Continuer` step and appear on the printed contract, and that items show `—` instead of a reference before then. Keep the register of the surrounding French prose.

- [ ] **Step 6: Update the scenarios**

In `docs/use-cases-and-personas.md` and `docs/scenarios-recette-utilisateur.md`, update `Prevente-04` and `Prevente-05` so the expected results match: no reference after item entry, references present after `Continuer`.

- [ ] **Step 7: Check nothing stale remains**

Run: `rtk git grep -n "item_number\|BK014\|BK-0" -- docs/`
Expected: no hits outside the superseded-decision notes in `decisions.md`.

- [ ] **Step 8: Commit**

```bash
rtk git add docs/
rtk git commit -m "Update canonical docs for letter-bucket item references"
```

---

### Task 6: End-to-end verification

**Files:** none modified.

- [ ] **Step 1: Build clean**

Run: `rtk dotnet build -c Release`
Expected: 0 warnings, 0 errors.

- [ ] **Step 2: Run the full suite**

Run: `rtk dotnet test`
Expected: all green. Record the test count.

- [ ] **Step 3: Confirm the sellability invariant was never weakened**

Run: `rtk git diff main --stat -- tests/AssociationSaleTool.Web.Tests/Services/CheckoutServiceTests.cs`
Expected: only `ItemNumber = <n>,` initializer lines removed (Task 4), no assertion changes.

This file is the evidence that an item without a reference still cannot be sold. `CheckoutService` gates on `status == "in_stock"` and `DepositStatus == Validated`, both of which an unreferenced item fails independently. If any assertion in this file had to change to make the suite pass, the invariant moved and the change needs re-examining before going further.

- [ ] **Step 4: Verify the migration applies to an empty database**

```bash
rm -rf /tmp/boursico-verify && mkdir -p /tmp/boursico-verify
rtk dotnet run --project src/AssociationSaleTool.Web --no-build
```

Expected: migrations apply, `Now listening on: http://0.0.0.0:5050`, nothing at `warn:` or above.

- [ ] **Step 5: Walk the deposit flow manually**

1. Open `/vendeurs`, create a seller, note the assigned code.
2. Add three items. Confirm the reference column shows `—` for all three.
3. Complete phone, address, postal code, city and all four identity-document fields.
4. Click `Finaliser le dépôt`, then `Continuer`.
5. Confirm the three items now show references sharing the seller code's first letter, numbered consecutively from `001`.
6. Click `Imprimer le contrat` and confirm the references appear on it.
7. Click `Valider le dépôt` and confirm `Le contrat est signé`.
8. Open `/caisse` and confirm the three items are searchable by reference.

- [ ] **Step 6: Verify the top-up path**

1. In `/administration`, unlock and stay in `Prévente`.
2. On the seller page, `Annuler la validation du dépôt` with the admin password.
3. Confirm the three existing references are unchanged.
4. Add a fourth item; confirm it shows `—`.
5. Re-run `Finaliser le dépôt` → `Continuer`.
6. Confirm the fourth item took the next number in the same letter and the first three are untouched.

- [ ] **Step 7: Commit any fixes**

```bash
rtk git add -A
rtk git commit -m "Fix issues found during end-to-end verification"
```
