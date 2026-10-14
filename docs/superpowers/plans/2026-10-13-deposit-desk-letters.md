# Deposit Desk Letters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allocate item references at the deposit desk — one run per seller at `Commencer le dépôt`, then one number per row saved — from letters each open desk owns exclusively, instead of at the contract step from the seller-code letter.

**Architecture:** A pure `DeskLetterPicker` decides which letters a run of N numbers uses and which free letters a desk takes. `DepositDeskService` opens and closes desks and owns the `labels_per_letter` setting. `ReferenceAllocationService` applies a plan to tracked entities (per-letter counters, desk holdings) without saving; `IntakeService` calls it from `StartDepositAsync` and `CreateItemBatchAsync` and saves everything in one `SaveChanges`, with optimistic concurrency tokens on `reference_letter` guarding races. The desk is identified by a token cookie read on the server.

**Tech Stack:** .NET 8, ASP.NET Core Razor Pages, EF Core 8 (SQLite), xUnit, Node test runner for client checks.

**Spec:** `docs/superpowers/specs/2026-10-13-deposit-desk-letters-design.md` (decision D-051 in `docs/decisions.md`)

## Global Constraints

- All operator-facing UI labels, page titles, buttons, validation messages, helper text and routes must be in **French**. Internal identifiers stay English. (`AGENTS.md`)
- Prefix every shell command with `rtk`, including inside `&&` chains. For warning counts and git history checks, call `dotnet`/`git` directly (rtk reformats them).
- Reference format unchanged: `<LETTER><NNN>`, zero-padded to 3 digits, via the existing `ItemReference.Format(char letter, int number)`.
- Letters: `B C D E G H J K L M N P R S T U W X Y Z` (20).
- Letters per desk: **2**, from `Labels:LettersPerDesk` in `appsettings.json`.
- Labels per letter: default **455**, stored in `event_state.labels_per_letter`, allowed range **1..999**, never below a number already used.
- Low-labels warning threshold: **20** labels left.
- Desk cookie: name **`poste-depot`**, `HttpOnly`, `SameSite=Strict`, 4 days.
- A per-letter counter (`reference_letter.last_number`) only increases. Deleting an item never frees its number.
- The application is not live: this change **regenerates the single `InitialCreate` migration** (project convention since 2026-09-29). Existing local databases must be deleted.
- Run both test suites before claiming a task is done: `rtk dotnet test` and every `tests/*.test.mjs` with `node --test`.
- The build must be green at the end of every task.

## Review Focus

1. **Deleting the highest-numbered labelled item** must not let the next save reuse that number. Pinned in Task 4 (`AllocateAsync_NeverReusesTheNumberOfADeletedItem`).
2. **Two desks taking the same free letter at once** must not both hold it. Pinned in Task 4 (`AllocateAsync_ConcurrentTakeOfTheSameLetterFailsForTheSecondDesk`) through the concurrency token.
3. **A computer whose desk was force-closed** must not create the item at all, not just skip its reference. Pinned in Task 5 (`CreateItemBatchAsync_OnAClosedDeskCreatesNothing`).
4. **Lowering labels per letter below what is used** must be refused, and raising it must make an exhausted letter takeable again. Pinned in Task 3.
5. **A pre-registered run bigger than any held letter's room** must split rather than fail, and fail cleanly only when no room exists at all. Pinned in Task 1 and Task 4.

---

### Task 1: DeskLetterPicker domain logic

Pure letter selection, no database. Nothing else changes, so the build stays green.

**Files:**
- Create: `src/AssociationSaleTool.Web/Domain/Items/DeskLetterPicker.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Domain/DeskLetterPickerTests.cs`

**Interfaces:**
- Produces:
  - `record LetterState(char Letter, int LastNumber, long? HeldByDeskId)`
  - `record LetterSegment(char Letter, int Count)`
  - `record RunPlan(IReadOnlyList<LetterSegment> Segments, IReadOnlyList<char> LettersToTake)`
  - `DeskLetterPicker.SafeAlphabet` → `IReadOnlyList<char>`
  - `DeskLetterPicker.Room(LetterState letter, int labelsPerLetter)` → `int`
  - `DeskLetterPicker.PickFreeLetters(IReadOnlyList<LetterState> letters, int count, int labelsPerLetter)` → `IReadOnlyList<char>`
  - `DeskLetterPicker.PlanRun(IReadOnlyList<LetterState> letters, long deskId, char? sellerLetter, int count, int labelsPerLetter)` → `RunPlan?` (null = no room)

- [ ] **Step 1: Write the failing tests**

Create `tests/AssociationSaleTool.Web.Tests/Domain/DeskLetterPickerTests.cs`:

```csharp
using AssociationSaleTool.Web.Domain.Items;

namespace AssociationSaleTool.Web.Tests.Domain;

public sealed class DeskLetterPickerTests
{
    private const long Desk = 1;
    private const long OtherDesk = 2;

    private static List<LetterState> Letters(params (char Letter, int Used, long? Holder)[] overrides)
    {
        var letters = DeskLetterPicker.SafeAlphabet
            .Select(letter => new LetterState(letter, 0, null))
            .ToList();
        foreach (var (letter, used, holder) in overrides)
        {
            var index = letters.FindIndex(x => x.Letter == letter);
            letters[index] = new LetterState(letter, used, holder);
        }

        return letters;
    }

    [Fact]
    public void PickFreeLetters_OnAnEmptyEventTakesTheFirstLettersOfTheAlphabet()
    {
        Assert.Equal(['B', 'C'], DeskLetterPicker.PickFreeLetters(Letters(), 2, 455));
    }

    [Fact]
    public void PickFreeLetters_SkipsHeldAndExhaustedLettersAndPrefersTheLeastUsed()
    {
        var letters = Letters(('B', 0, OtherDesk), ('C', 455, null), ('D', 40, null), ('E', 10, null));

        // G..Z are unused, so they come before D (40) and E (10).
        Assert.Equal(['G', 'H'], DeskLetterPicker.PickFreeLetters(letters, 2, 455));
    }

    [Fact]
    public void PickFreeLetters_ReturnsNothingForAZeroCount()
    {
        Assert.Empty(DeskLetterPicker.PickFreeLetters(Letters(), 0, 455));
    }

    [Fact]
    public void PlanRun_KeepsTheSellerLetterWhenItFits()
    {
        var letters = Letters(('B', 12, Desk), ('C', 0, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, 'B', 1, 455);

        Assert.NotNull(plan);
        Assert.Equal([new LetterSegment('B', 1)], plan.Segments);
        Assert.Empty(plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_WithoutSellerLetterUsesTheHeldLetterWithMostRoom()
    {
        var letters = Letters(('B', 30, Desk), ('C', 10, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, null, 12, 455);

        Assert.Equal([new LetterSegment('C', 12)], plan!.Segments);
    }

    [Fact]
    public void PlanRun_IgnoresTheSellerLetterWhenAnotherDeskHoldsIt()
    {
        var letters = Letters(('B', 12, OtherDesk), ('K', 5, Desk), ('R', 50, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, 'B', 1, 455);

        Assert.Equal([new LetterSegment('K', 1)], plan!.Segments);
    }

    [Fact]
    public void PlanRun_MovesAWholeRunToTheOtherLetterRatherThanSplitting()
    {
        // Spec example 4: B has 2 left, C has room for the 5 rows.
        var letters = Letters(('B', 18, Desk), ('C', 3, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, null, 5, 20);

        Assert.Equal([new LetterSegment('C', 5)], plan!.Segments);
    }

    [Fact]
    public void PlanRun_SplitsAcrossHeldLettersThenTakesAFreeLetter()
    {
        var letters = Letters(('B', 18, Desk), ('C', 17, Desk));

        var plan = DeskLetterPicker.PlanRun(letters, Desk, 'B', 8, 20);

        Assert.Equal(
            [new LetterSegment('B', 2), new LetterSegment('C', 3), new LetterSegment('D', 3)],
            plan!.Segments);
        Assert.Equal(['D'], plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_TakesAFreeLetterWhenTheDeskHoldsNone()
    {
        var plan = DeskLetterPicker.PlanRun(Letters(), Desk, null, 3, 455);

        Assert.Equal([new LetterSegment('B', 3)], plan!.Segments);
        Assert.Equal(['B'], plan.LettersToTake);
    }

    [Fact]
    public void PlanRun_ReturnsNullWhenThereIsNoRoomAnywhere()
    {
        var letters = DeskLetterPicker.SafeAlphabet
            .Select(letter => new LetterState(letter, 20, letter == 'B' ? Desk : null))
            .ToList();

        Assert.Null(DeskLetterPicker.PlanRun(letters, Desk, null, 1, 20));
    }

    [Fact]
    public void Room_IsNeverNegativeAfterTheSettingWasLowered()
    {
        Assert.Equal(0, DeskLetterPicker.Room(new LetterState('B', 30, null), 20));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~DeskLetterPickerTests"`
Expected: build error, `DeskLetterPicker` does not exist.

- [ ] **Step 3: Implement**

Create `src/AssociationSaleTool.Web/Domain/Items/DeskLetterPicker.cs`:

```csharp
namespace AssociationSaleTool.Web.Domain.Items;

public sealed record LetterState(char Letter, int LastNumber, long? HeldByDeskId);

public sealed record LetterSegment(char Letter, int Count);

public sealed record RunPlan(IReadOnlyList<LetterSegment> Segments, IReadOnlyList<char> LettersToTake);

/// <summary>
/// Decides which reference letters a deposit desk draws numbers from. A
/// letter is held by at most one open desk, so the desk's next number is
/// always the next label on the sheet in front of the operator.
/// </summary>
public static class DeskLetterPicker
{
    // Same alphabet as SellerCodeGenerator: 'A' is reserved for the
    // association, and F, I, O, Q, V are excluded as visually confusable.
    public static readonly IReadOnlyList<char> SafeAlphabet =
    [
        'B', 'C', 'D', 'E', 'G', 'H', 'J', 'K', 'L',
        'M', 'N', 'P', 'R', 'S', 'T', 'U', 'W', 'X', 'Y', 'Z'
    ];

    public static int Room(LetterState letter, int labelsPerLetter)
    {
        return Math.Max(0, labelsPerLetter - letter.LastNumber);
    }

    public static IReadOnlyList<char> PickFreeLetters(
        IReadOnlyList<LetterState> letters,
        int count,
        int labelsPerLetter)
    {
        if (count <= 0)
        {
            return [];
        }

        return FreeLettersLeastUsedFirst(letters, labelsPerLetter)
            .Take(count)
            .Select(letter => letter.Letter)
            .ToList();
    }

    public static RunPlan? PlanRun(
        IReadOnlyList<LetterState> letters,
        long deskId,
        char? sellerLetter,
        int count,
        int labelsPerLetter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var usable = letters
            .Where(letter => letter.HeldByDeskId == deskId && Room(letter, labelsPerLetter) > 0)
            .OrderByDescending(letter => Room(letter, labelsPerLetter))
            .ThenBy(letter => AlphabetIndex(letter.Letter))
            .ToList();
        var sellerUsable = sellerLetter is { } wanted
            ? usable.FirstOrDefault(letter => letter.Letter == wanted)
            : null;

        // Tier 1: keep a seller's extras next to their run.
        if (sellerUsable is not null && Room(sellerUsable, labelsPerLetter) >= count)
        {
            return new RunPlan([new LetterSegment(sellerUsable.Letter, count)], []);
        }

        // Tier 2: never split a run while one held letter can take all of it.
        if (usable.FirstOrDefault() is { } widest && Room(widest, labelsPerLetter) >= count)
        {
            return new RunPlan([new LetterSegment(widest.Letter, count)], []);
        }

        // Tier 3: split - seller letter, other held letters, then free letters taken on the spot.
        var candidates = new List<(LetterState Letter, bool Take)>();
        if (sellerUsable is not null)
        {
            candidates.Add((sellerUsable, false));
        }

        candidates.AddRange(usable
            .Where(letter => letter.Letter != sellerUsable?.Letter)
            .Select(letter => (letter, false)));
        candidates.AddRange(FreeLettersLeastUsedFirst(letters, labelsPerLetter)
            .Select(letter => (letter, true)));

        var segments = new List<LetterSegment>();
        var lettersToTake = new List<char>();
        var remaining = count;
        foreach (var (letter, take) in candidates)
        {
            if (remaining == 0)
            {
                break;
            }

            var used = Math.Min(remaining, Room(letter, labelsPerLetter));
            segments.Add(new LetterSegment(letter.Letter, used));
            if (take)
            {
                lettersToTake.Add(letter.Letter);
            }

            remaining -= used;
        }

        // Tier 4: no room at all.
        return remaining == 0 ? new RunPlan(segments, lettersToTake) : null;
    }

    private static IEnumerable<LetterState> FreeLettersLeastUsedFirst(
        IReadOnlyList<LetterState> letters,
        int labelsPerLetter)
    {
        return letters
            .Where(letter => letter.HeldByDeskId is null && Room(letter, labelsPerLetter) > 0)
            .OrderBy(letter => letter.LastNumber)
            .ThenBy(letter => AlphabetIndex(letter.Letter));
    }

    private static int AlphabetIndex(char letter)
    {
        var index = SafeAlphabet.ToList().IndexOf(letter);
        return index < 0 ? int.MaxValue : index;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `rtk dotnet test --filter "FullyQualifiedName~DeskLetterPickerTests"`
Expected: 11 passed.

- [ ] **Step 5: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Domain/Items/DeskLetterPicker.cs tests/AssociationSaleTool.Web.Tests/Domain/DeskLetterPickerTests.cs
rtk git commit -m "Add the letter picker for deposit desks"
```

---

### Task 2: Data model for desks, letters and labels per letter

**Files:**
- Create: `src/AssociationSaleTool.Web/Data/Entities/DepositDeskEntity.cs`
- Create: `src/AssociationSaleTool.Web/Data/Entities/ReferenceLetterEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/Entities/SellerEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/Entities/EventStateEntity.cs`
- Modify: `src/AssociationSaleTool.Web/Data/AppDbContext.cs`
- Replace: `src/AssociationSaleTool.Web/Migrations/*` (regenerated `InitialCreate`)
- Test: `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs`

**Interfaces:**
- Consumes: `DeskLetterPicker.SafeAlphabet` (Task 1) for the seed.
- Produces:
  - `AppDbContext.DepositDesks` (`DbSet<DepositDeskEntity>`), `AppDbContext.ReferenceLetters` (`DbSet<ReferenceLetterEntity>`)
  - `DepositDeskEntity { long Id; string Name; string Token; DateTime OpenedAt; DateTime? ClosedAt; DateTime LastActivityAt }`
  - `ReferenceLetterEntity { string Letter; int LastNumber; long? HeldByDeskId; DepositDeskEntity? HeldByDesk }` — `LastNumber` and `HeldByDeskId` are concurrency tokens
  - `SellerEntity.DepositStartedAt` (`DateTime?`)
  - `EventStateEntity.LabelsPerLetter` (`int`, default 455)

- [ ] **Step 1: Write the failing tests**

Add to `tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs` (reuse the file's existing in-memory SQLite setup; the snippet shows it inline):

```csharp
    [Fact]
    public async Task NewDatabase_SeedsTheTwentyReferenceLettersFreeAndUnused()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();

        var letters = await context.ReferenceLetters.OrderBy(x => x.Letter).ToListAsync();

        Assert.Equal("BCDEGHJKLMNPRSTUWXYZ", string.Concat(letters.Select(x => x.Letter)));
        Assert.All(letters, letter =>
        {
            Assert.Equal(0, letter.LastNumber);
            Assert.Null(letter.HeldByDeskId);
        });
    }

    [Fact]
    public async Task NewDatabase_DefaultsLabelsPerLetterTo455()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();

        Assert.Equal(455, (await context.EventState.SingleAsync()).LabelsPerLetter);
    }

    [Fact]
    public async Task ReferenceLetter_RejectsANumberAbove999()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();

        var letter = await context.ReferenceLetters.SingleAsync(x => x.Letter == "B");
        letter.LastNumber = 1000;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
```

Add `using Microsoft.Data.Sqlite;` and `using Microsoft.EntityFrameworkCore;` if the file lacks them.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~AppDbContextModelTests"`
Expected: build error, `ReferenceLetters` and `LabelsPerLetter` do not exist.

- [ ] **Step 3: Add the entities**

Create `src/AssociationSaleTool.Web/Data/Entities/DepositDeskEntity.cs`:

```csharp
namespace AssociationSaleTool.Web.Data.Entities;

/// <summary>
/// One deposit desk: one computer at one physical queue. Created when a
/// computer opens it, identified by the random token stored in its cookie.
/// </summary>
public sealed class DepositDeskEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public List<ReferenceLetterEntity> Letters { get; set; } = [];
}
```

Create `src/AssociationSaleTool.Web/Data/Entities/ReferenceLetterEntity.cs`:

```csharp
namespace AssociationSaleTool.Web.Data.Entities;

/// <summary>
/// A reference letter's counter and current holder. LastNumber only ever
/// increases, so a number whose label was already peeled is never handed out
/// twice, even after its item is deleted.
/// </summary>
public sealed class ReferenceLetterEntity
{
    public string Letter { get; set; } = string.Empty;

    public int LastNumber { get; set; }

    public long? HeldByDeskId { get; set; }

    public DepositDeskEntity? HeldByDesk { get; set; }
}
```

In `SellerEntity.cs`, after `DepositStatus`:

```csharp
    /// <summary>
    /// Set by "Commencer le dépôt" at a deposit desk. From then on, every row
    /// saved for this seller receives its reference in the same save.
    /// </summary>
    public DateTime? DepositStartedAt { get; set; }
```

In `EventStateEntity.cs`, after `SalePhase`:

```csharp
    public int LabelsPerLetter { get; set; } = 455;
```

- [ ] **Step 4: Map them in `AppDbContext`**

Add the sets after `EventState`:

```csharp
    public DbSet<DepositDeskEntity> DepositDesks => Set<DepositDeskEntity>();

    public DbSet<ReferenceLetterEntity> ReferenceLetters => Set<ReferenceLetterEntity>();
```

In the `SellerEntity` mapping, next to the other columns:

```csharp
            entity.Property(x => x.DepositStartedAt).HasColumnName("deposit_started_at");
```

In the `EventStateEntity` mapping: add the column, add `LabelsPerLetter = 455` to the `HasData` object, and add a check constraint inside the existing `ToTable(table => ...)`:

```csharp
            entity.Property(x => x.LabelsPerLetter).HasColumnName("labels_per_letter").HasDefaultValue(455).IsRequired();
```

```csharp
                table.HasCheckConstraint("ck_event_state_labels_per_letter", "labels_per_letter BETWEEN 1 AND 999");
```

Add two new mappings at the end of `OnModelCreating`:

```csharp
        modelBuilder.Entity<DepositDeskEntity>(entity =>
        {
            entity.ToTable("deposit_desk");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Token).HasColumnName("token").IsRequired();
            entity.Property(x => x.OpenedAt).HasColumnName("opened_at").IsRequired();
            entity.Property(x => x.ClosedAt).HasColumnName("closed_at");
            entity.Property(x => x.LastActivityAt).HasColumnName("last_activity_at").IsRequired();

            entity.HasIndex(x => x.Token).IsUnique();

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_deposit_desk_name_not_blank", "length(trim(name)) > 0");
            });
        });

        modelBuilder.Entity<ReferenceLetterEntity>(entity =>
        {
            entity.ToTable("reference_letter");
            entity.HasKey(x => x.Letter);
            entity.Property(x => x.Letter).HasColumnName("letter").HasMaxLength(1);
            // Both are concurrency tokens: two desks writing the same letter
            // from one stale read fail with DbUpdateConcurrencyException
            // instead of both believing they hold it or both using a number.
            entity.Property(x => x.LastNumber).HasColumnName("last_number").HasDefaultValue(0).IsRequired().IsConcurrencyToken();
            entity.Property(x => x.HeldByDeskId).HasColumnName("held_by_desk_id").IsConcurrencyToken();

            entity.HasOne(x => x.HeldByDesk)
                .WithMany(x => x.Letters)
                .HasForeignKey(x => x.HeldByDeskId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasData(DeskLetterPicker.SafeAlphabet.Select(letter => new ReferenceLetterEntity
            {
                Letter = letter.ToString(),
                LastNumber = 0
            }));

            entity.ToTable(table =>
            {
                table.HasCheckConstraint("ck_reference_letter_last_number", "last_number BETWEEN 0 AND 999");
            });
        });
```

Add `using AssociationSaleTool.Web.Domain.Items;` at the top of `AppDbContext.cs`.

- [ ] **Step 5: Regenerate the InitialCreate migration**

The app is not live; the convention is to regenerate the single migration rather than add one.

```bash
rtk git rm -q src/AssociationSaleTool.Web/Migrations/20260929074921_InitialCreate.cs src/AssociationSaleTool.Web/Migrations/20260929074921_InitialCreate.Designer.cs src/AssociationSaleTool.Web/Migrations/AppDbContextModelSnapshot.cs
rtk dotnet tool restore
rtk dotnet ef migrations add InitialCreate --project src/AssociationSaleTool.Web --output-dir Migrations
```

Expected: three new files in `Migrations/`. Open the new `*_InitialCreate.cs` and check it creates `deposit_desk` and `reference_letter`, inserts 20 `reference_letter` rows, and adds `deposit_started_at` and `labels_per_letter`.

Never delete `src/AssociationSaleTool.Web/data/` to reset your local database: on Windows it is the same folder as the `Data/` source folder. Delete only the `*.db`, `*.db-shm` and `*.db-wal` files in it.

- [ ] **Step 6: Run the tests**

Run: `rtk dotnet test`
Expected: all pass, including the 3 new model tests.

- [ ] **Step 7: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Data src/AssociationSaleTool.Web/Migrations tests/AssociationSaleTool.Web.Tests/Data/AppDbContextModelTests.cs
rtk git commit -m "Add deposit desks, reference letter counters and labels per letter to the model"
```

---

### Task 3: DepositDeskService — desks and the labels-per-letter setting

**Files:**
- Modify: `src/AssociationSaleTool.Web/Configuration/LabelsOptions.cs`
- Modify: `src/AssociationSaleTool.Web/appsettings.json`
- Create: `src/AssociationSaleTool.Web/Services/Models/DepositDeskModels.cs`
- Create: `src/AssociationSaleTool.Web/Services/DepositDeskService.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/DepositDeskServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 picker, Task 2 entities.
- Produces:
  - `LabelsOptions.LettersPerDesk` (`int`, default 2). `SoftCapacityPerLetter` stays until Task 5 removes it.
  - `DepositDeskService(AppDbContext dbContext, IOptions<LabelsOptions>? labelsOptions = null)`
  - `const int DepositDeskService.LowLabelsThreshold = 20`
  - `Task<OpenDeskResult> OpenDeskAsync(string? name, CancellationToken ct = default)`
  - `Task<CurrentDesk> GetCurrentDeskAsync(string? token, CancellationToken ct = default)`
  - `Task<bool> CloseDeskAsync(long deskId, CancellationToken ct = default)`
  - `Task<int> GetLabelsPerLetterAsync(CancellationToken ct = default)`
  - `Task<LabelsPerLetterUpdateResult> SetLabelsPerLetterAsync(int labelsPerLetter, CancellationToken ct = default)`
  - `Task<IReadOnlyList<ReferenceLetterRow>> ListLettersAsync(CancellationToken ct = default)`
  - `Task<IReadOnlyList<OpenDeskRow>> ListOpenDesksAsync(CancellationToken ct = default)`
  - model records below

- [ ] **Step 1: Write the failing tests**

Create `tests/AssociationSaleTool.Web.Tests/Services/DepositDeskServiceTests.cs`:

```csharp
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class DepositDeskServiceTests
{
    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task OpenDeskAsync_GivesEachDeskTwoDistinctLeastUsedLetters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);

        var first = await service.OpenDeskAsync(null);
        var second = await service.OpenDeskAsync("Entrée");

        Assert.Equal("Poste 1", first.Name);
        Assert.Equal(['B', 'C'], first.Letters);
        Assert.Equal("Entrée", second.Name);
        Assert.Equal(['D', 'E'], second.Letters);
        Assert.NotEqual(first.Token, second.Token);
    }

    [Fact]
    public async Task OpenDeskAsync_UsesLettersPerDeskFromOptions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context, Options.Create(new LabelsOptions { LettersPerDesk = 3 }));

        var desk = await service.OpenDeskAsync(null);

        Assert.Equal(['B', 'C', 'D'], desk.Letters);
    }

    [Fact]
    public async Task OpenDeskAsync_OpensWithoutLettersWhenNoneIsFree()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        for (var i = 0; i < 10; i++)
        {
            await service.OpenDeskAsync(null);
        }

        var eleventh = await service.OpenDeskAsync(null);

        Assert.Empty(eleventh.Letters);
        Assert.Equal(DeskState.Open, (await service.GetCurrentDeskAsync(eleventh.Token)).State);
    }

    [Fact]
    public async Task CloseDeskAsync_FreesTheLettersAndMarksTheDeskClosed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        var first = await service.OpenDeskAsync(null);

        Assert.True(await service.CloseDeskAsync(first.DeskId));
        var second = await service.OpenDeskAsync(null);

        Assert.Equal(DeskState.Closed, (await service.GetCurrentDeskAsync(first.Token)).State);
        Assert.Equal(['B', 'C'], second.Letters);
        Assert.False(await service.CloseDeskAsync(first.DeskId));
    }

    [Fact]
    public async Task GetCurrentDeskAsync_ReportsNoneForAMissingOrUnknownToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);

        Assert.Equal(DeskState.None, (await service.GetCurrentDeskAsync(null)).State);
        Assert.Equal(DeskState.None, (await service.GetCurrentDeskAsync("inconnu")).State);
    }

    [Fact]
    public async Task GetCurrentDeskAsync_ReportsLabelsLeftPerHeldLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        var desk = await service.OpenDeskAsync(null);
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber = 440;
        await context.SaveChangesAsync();

        var current = await service.GetCurrentDeskAsync(desk.Token);

        Assert.Equal([new DeskLetterInfo('B', 15), new DeskLetterInfo('C', 455)], current.Letters);
    }

    [Fact]
    public async Task SetLabelsPerLetterAsync_RefusesOutOfRangeValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);

        Assert.Equal(LabelsPerLetterUpdateStatus.OutOfRange, (await service.SetLabelsPerLetterAsync(0)).Status);
        Assert.Equal(LabelsPerLetterUpdateStatus.OutOfRange, (await service.SetLabelsPerLetterAsync(1000)).Status);
        Assert.Equal(455, await service.GetLabelsPerLetterAsync());
    }

    [Fact]
    public async Task SetLabelsPerLetterAsync_RefusesAValueBelowANumberAlreadyUsed()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "K")).LastNumber = 15;
        await context.SaveChangesAsync();

        var result = await service.SetLabelsPerLetterAsync(10);

        Assert.Equal(new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.BelowUsed, 'K', 15), result);
        Assert.Equal(455, await service.GetLabelsPerLetterAsync());
    }

    [Fact]
    public async Task SetLabelsPerLetterAsync_RaisingTheValueMakesAnExhaustedLetterTakeableAgain()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        await service.SetLabelsPerLetterAsync(20);
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber = 20;
        await context.SaveChangesAsync();
        Assert.Equal(['C', 'D'], (await service.OpenDeskAsync(null)).Letters);

        Assert.Equal(LabelsPerLetterUpdateStatus.Saved, (await service.SetLabelsPerLetterAsync(30)).Status);
        // A desk asking for every free letter shows B is takeable again (a
        // default 2-letter desk would pick unused letters first).
        var next = await new DepositDeskService(context, Options.Create(new LabelsOptions { LettersPerDesk = 20 }))
            .OpenDeskAsync(null);

        Assert.Contains('B', next.Letters);
    }

    [Fact]
    public async Task ListOpenDesksAsync_ListsOnlyOpenDesksWithTheirLetters()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        var closed = await service.OpenDeskAsync("Fermé");
        await service.OpenDeskAsync("Ouvert");
        await service.CloseDeskAsync(closed.DeskId);

        var desk = Assert.Single(await service.ListOpenDesksAsync());

        Assert.Equal("Ouvert", desk.Name);
        Assert.Equal(['D', 'E'], desk.Letters);
    }

    [Fact]
    public async Task ListLettersAsync_ShowsUsageAndHolder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var service = new DepositDeskService(context);
        await service.OpenDeskAsync("Poste A");
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "D")).LastNumber = 455;
        await context.SaveChangesAsync();

        var letters = await service.ListLettersAsync();

        Assert.Equal(20, letters.Count);
        Assert.Equal(new ReferenceLetterRow('B', 0, 455, "Poste A"), letters[0]);
        Assert.Equal(new ReferenceLetterRow('D', 455, 0, null), letters[2]);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~DepositDeskServiceTests"`
Expected: build error, `DepositDeskService` does not exist.

- [ ] **Step 3: Add the option**

In `LabelsOptions.cs`, add below `SoftCapacityPerLetter`:

```csharp
    /// <summary>
    /// How many reference letters an open deposit desk holds. Each letter's
    /// pre-printed sheets sit on that desk while it is open.
    /// </summary>
    public int LettersPerDesk { get; init; } = 2;
```

In `appsettings.json`, set the `Labels` section to:

```json
  "Labels": {
    "SoftCapacityPerLetter": 455,
    "LettersPerDesk": 2
  },
```

- [ ] **Step 4: Add the models**

Create `src/AssociationSaleTool.Web/Services/Models/DepositDeskModels.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public enum DeskState
{
    None,
    Open,
    Closed
}

public sealed record DeskLetterInfo(char Letter, int LabelsLeft);

public sealed record CurrentDesk(DeskState State, long? DeskId, string? Name, IReadOnlyList<DeskLetterInfo> Letters)
{
    public static CurrentDesk None { get; } = new(DeskState.None, null, null, []);
}

public sealed record OpenDeskResult(long DeskId, string Name, string Token, IReadOnlyList<char> Letters);

public enum LabelsPerLetterUpdateStatus
{
    Saved,
    OutOfRange,
    BelowUsed
}

public sealed record LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus Status, char? Letter = null, int HighestUsed = 0);

public sealed record ReferenceLetterRow(char Letter, int Used, int LabelsLeft, string? HeldByDeskName);

public sealed record OpenDeskRow(long Id, string Name, IReadOnlyList<char> Letters, DateTime OpenedAt, DateTime LastActivityAt);
```

- [ ] **Step 5: Implement the service**

Create `src/AssociationSaleTool.Web/Services/DepositDeskService.cs`:

```csharp
using System.Security.Cryptography;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Services;

public sealed class DepositDeskService(AppDbContext dbContext, IOptions<LabelsOptions>? labelsOptions = null)
{
    public const int LowLabelsThreshold = 20;
    public const int MaxLabelsPerLetter = 999;

    private readonly int _lettersPerDesk = (labelsOptions?.Value ?? new LabelsOptions()).LettersPerDesk;

    public async Task<OpenDeskResult> OpenDeskAsync(string? name, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var deskCount = await dbContext.DepositDesks.CountAsync(cancellationToken);
        var desk = new DepositDeskEntity
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Poste {deskCount + 1}" : name.Trim(),
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            OpenedAt = now,
            LastActivityAt = now
        };
        dbContext.DepositDesks.Add(desk);
        await dbContext.SaveChangesAsync(cancellationToken);

        var labelsPerLetter = await GetLabelsPerLetterAsync(cancellationToken);
        var letters = await dbContext.ReferenceLetters.ToListAsync(cancellationToken);
        var picked = DeskLetterPicker.PickFreeLetters(ToStates(letters), _lettersPerDesk, labelsPerLetter);
        foreach (var letter in letters.Where(x => picked.Contains(x.Letter[0])))
        {
            letter.HeldByDeskId = desk.Id;
        }

        // The concurrency token on held_by_desk_id makes a desk opened at the
        // same instant fail here rather than share a letter; the operator
        // re-submits and gets the next free letters.
        await dbContext.SaveChangesAsync(cancellationToken);
        return new OpenDeskResult(desk.Id, desk.Name, desk.Token, picked);
    }

    public async Task<CurrentDesk> GetCurrentDeskAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return CurrentDesk.None;
        }

        var desk = await dbContext.DepositDesks
            .AsNoTracking()
            .Include(x => x.Letters)
            .SingleOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (desk is null)
        {
            return CurrentDesk.None;
        }

        if (desk.ClosedAt is not null)
        {
            return new CurrentDesk(DeskState.Closed, desk.Id, desk.Name, []);
        }

        var labelsPerLetter = await GetLabelsPerLetterAsync(cancellationToken);
        var letters = desk.Letters
            .OrderBy(x => x.Letter)
            .Select(x => new DeskLetterInfo(x.Letter[0], Math.Max(0, labelsPerLetter - x.LastNumber)))
            .ToList();
        return new CurrentDesk(DeskState.Open, desk.Id, desk.Name, letters);
    }

    public async Task<bool> CloseDeskAsync(long deskId, CancellationToken cancellationToken = default)
    {
        var desk = await dbContext.DepositDesks
            .Include(x => x.Letters)
            .SingleOrDefaultAsync(x => x.Id == deskId, cancellationToken);
        if (desk is null || desk.ClosedAt is not null)
        {
            return false;
        }

        desk.ClosedAt = DateTime.UtcNow;
        foreach (var letter in desk.Letters)
        {
            letter.HeldByDeskId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GetLabelsPerLetterAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.EventState.AsNoTracking().Select(x => x.LabelsPerLetter).SingleAsync(cancellationToken);
    }

    public async Task<LabelsPerLetterUpdateResult> SetLabelsPerLetterAsync(
        int labelsPerLetter,
        CancellationToken cancellationToken = default)
    {
        if (labelsPerLetter < 1 || labelsPerLetter > MaxLabelsPerLetter)
        {
            return new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.OutOfRange);
        }

        var mostUsed = await dbContext.ReferenceLetters
            .AsNoTracking()
            .OrderByDescending(x => x.LastNumber)
            .ThenBy(x => x.Letter)
            .FirstAsync(cancellationToken);
        if (mostUsed.LastNumber > labelsPerLetter)
        {
            return new LabelsPerLetterUpdateResult(
                LabelsPerLetterUpdateStatus.BelowUsed,
                mostUsed.Letter[0],
                mostUsed.LastNumber);
        }

        var state = await dbContext.EventState.SingleAsync(cancellationToken);
        state.LabelsPerLetter = labelsPerLetter;
        state.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.Saved);
    }

    public async Task<IReadOnlyList<ReferenceLetterRow>> ListLettersAsync(CancellationToken cancellationToken = default)
    {
        var labelsPerLetter = await GetLabelsPerLetterAsync(cancellationToken);
        var letters = await dbContext.ReferenceLetters
            .AsNoTracking()
            .Include(x => x.HeldByDesk)
            .ToListAsync(cancellationToken);

        return letters
            .OrderBy(x => DeskLetterPicker.SafeAlphabet.ToList().IndexOf(x.Letter[0]))
            .Select(x => new ReferenceLetterRow(
                x.Letter[0],
                x.LastNumber,
                Math.Max(0, labelsPerLetter - x.LastNumber),
                x.HeldByDesk?.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<OpenDeskRow>> ListOpenDesksAsync(CancellationToken cancellationToken = default)
    {
        var desks = await dbContext.DepositDesks
            .AsNoTracking()
            .Include(x => x.Letters)
            .Where(x => x.ClosedAt == null)
            .OrderBy(x => x.OpenedAt)
            .ToListAsync(cancellationToken);

        return desks
            .Select(x => new OpenDeskRow(
                x.Id,
                x.Name,
                x.Letters.Select(letter => letter.Letter[0]).OrderBy(letter => letter).ToList(),
                x.OpenedAt,
                x.LastActivityAt))
            .ToList();
    }

    internal static IReadOnlyList<LetterState> ToStates(IEnumerable<ReferenceLetterEntity> letters)
    {
        return letters
            .Select(x => new LetterState(x.Letter[0], x.LastNumber, x.HeldByDeskId))
            .ToList();
    }
}
```

Register it in `Program.cs` next to the other services:

```csharp
builder.Services.AddScoped<DepositDeskService>();
```

- [ ] **Step 6: Run the tests**

Run: `rtk dotnet test --filter "FullyQualifiedName~DepositDeskServiceTests"`
Expected: 11 passed. Then `rtk dotnet test`: all pass.

- [ ] **Step 7: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Configuration/LabelsOptions.cs src/AssociationSaleTool.Web/appsettings.json src/AssociationSaleTool.Web/Services/Models/DepositDeskModels.cs src/AssociationSaleTool.Web/Services/DepositDeskService.cs src/AssociationSaleTool.Web/Program.cs tests/AssociationSaleTool.Web.Tests/Services/DepositDeskServiceTests.cs
rtk git commit -m "Open and close deposit desks and store labels per letter"
```

---

### Task 4: ReferenceAllocationService — apply a run at a desk

The service mutates tracked entities and never saves; its caller saves rows, counters and holdings in one `SaveChanges`.

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/ReferenceAllocationModels.cs`
- Create: `src/AssociationSaleTool.Web/Services/ReferenceAllocationService.cs`
- Modify: `src/AssociationSaleTool.Web/Program.cs`
- Test: `tests/AssociationSaleTool.Web.Tests/Services/ReferenceAllocationServiceTests.cs`

**Interfaces:**
- Consumes: `DeskLetterPicker.PlanRun`, `DeskLetterPicker.PickFreeLetters` (Task 1); `DepositDeskService.ToStates` (Task 3); `ItemReference.Format(char, int)` (existing).
- Produces:
  - `enum ReferenceAllocationStatus { Allocated, NoDesk, DeskClosed, NoLabelsLeft }`
  - `record ReferenceAllocationResult(ReferenceAllocationStatus Status, IReadOnlyList<string> AssignedReferences, IReadOnlyList<char> TakenLetters)`
  - `ReferenceAllocationService(AppDbContext dbContext, IOptions<LabelsOptions>? labelsOptions = null)`
  - `Task<ReferenceAllocationResult> AllocateAsync(long? deskId, char? sellerLetter, IReadOnlyList<ItemEntity> items, CancellationToken ct = default)`

- [ ] **Step 1: Write the failing tests**

Create `tests/AssociationSaleTool.Web.Tests/Services/ReferenceAllocationServiceTests.cs`:

```csharp
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Tests.Services;

public sealed class ReferenceAllocationServiceTests
{
    private static async Task<AppDbContext> CreateContextAsync(SqliteConnection connection)
    {
        await connection.OpenAsync();
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<SellerEntity> SeedSellerAsync(AppDbContext context, int itemCount)
    {
        var seller = new SellerEntity { SellerCode = "BB", FirstName = "Ada", LastName = "Lovelace" };
        for (var i = 0; i < itemCount; i++)
        {
            seller.Items.Add(new ItemEntity { Title = $"Jouet {i + 1}", SellerPriceCents = 100, Status = "registered" });
        }

        context.Sellers.Add(seller);
        await context.SaveChangesAsync();
        return seller;
    }

    [Fact]
    public async Task AllocateAsync_GivesARunOfConsecutiveNumbersFromTheDeskLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desk = await new DepositDeskService(context).OpenDeskAsync(null);
        var seller = await SeedSellerAsync(context, 3);

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, null, seller.Items);
        await context.SaveChangesAsync();

        Assert.Equal(ReferenceAllocationStatus.Allocated, result.Status);
        Assert.Equal(["B001", "B002", "B003"], result.AssignedReferences);
        Assert.Equal(["B001", "B002", "B003"], seller.Items.Select(x => x.Reference));
        Assert.Equal(3, (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber);
    }

    [Fact]
    public async Task AllocateAsync_NeverReusesTheNumberOfADeletedItem()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desk = await new DepositDeskService(context).OpenDeskAsync(null);
        var seller = await SeedSellerAsync(context, 2);
        var service = new ReferenceAllocationService(context);
        await service.AllocateAsync(desk.DeskId, null, seller.Items);
        await context.SaveChangesAsync();

        context.Items.Remove(seller.Items.Single(x => x.Reference == "B002"));
        await context.SaveChangesAsync();
        var extra = new ItemEntity { SellerId = seller.Id, Title = "Extra", SellerPriceCents = 100, Status = "registered" };
        context.Items.Add(extra);
        var result = await service.AllocateAsync(desk.DeskId, 'B', [extra]);
        await context.SaveChangesAsync();

        Assert.Equal(["B003"], result.AssignedReferences);
    }

    [Fact]
    public async Task AllocateAsync_RefusesWithoutADeskOrOnAClosedDeskAndChangesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desks = new DepositDeskService(context);
        var desk = await desks.OpenDeskAsync(null);
        await desks.CloseDeskAsync(desk.DeskId);
        var seller = await SeedSellerAsync(context, 1);
        var service = new ReferenceAllocationService(context);

        Assert.Equal(ReferenceAllocationStatus.NoDesk, (await service.AllocateAsync(null, null, seller.Items)).Status);
        Assert.Equal(ReferenceAllocationStatus.DeskClosed, (await service.AllocateAsync(desk.DeskId, null, seller.Items)).Status);
        Assert.Null(seller.Items[0].Reference);
        Assert.All(await context.ReferenceLetters.ToListAsync(), x => Assert.Equal(0, x.LastNumber));
    }

    [Fact]
    public async Task AllocateAsync_ReleasesAnExhaustedLetterAndTakesAReplacement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desks = new DepositDeskService(context);
        await desks.SetLabelsPerLetterAsync(20);
        var desk = await desks.OpenDeskAsync(null);
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber = 18;
        (await context.ReferenceLetters.SingleAsync(x => x.Letter == "C")).LastNumber = 17;
        await context.SaveChangesAsync();
        var seller = await SeedSellerAsync(context, 8);

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, 'B', seller.Items);
        await context.SaveChangesAsync();

        Assert.Equal(
            ["B019", "B020", "C018", "C019", "C020", "D001", "D002", "D003"],
            result.AssignedReferences);
        // D was taken mid-run; B and C are exhausted and released; E tops the desk back up to two letters.
        Assert.Equal(['D', 'E'], result.TakenLetters);
        var held = await context.ReferenceLetters.Where(x => x.HeldByDeskId == desk.DeskId).Select(x => x.Letter).ToListAsync();
        Assert.Equal(["D", "E"], held.Order());
    }

    [Fact]
    public async Task AllocateAsync_RefusesWhenNoLetterHasRoomAndChangesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desks = new DepositDeskService(context);
        await desks.SetLabelsPerLetterAsync(1);
        var desk = await desks.OpenDeskAsync(null);
        foreach (var letter in await context.ReferenceLetters.ToListAsync())
        {
            letter.LastNumber = 1;
        }

        await context.SaveChangesAsync();
        var seller = await SeedSellerAsync(context, 1);

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, null, seller.Items);

        Assert.Equal(ReferenceAllocationStatus.NoLabelsLeft, result.Status);
        Assert.Null(seller.Items[0].Reference);
    }

    [Fact]
    public async Task AllocateAsync_WithNoItemsAllocatesNothingButRecordsActivity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var context = await CreateContextAsync(connection);
        var desk = await new DepositDeskService(context).OpenDeskAsync(null);
        var before = (await context.DepositDesks.SingleAsync()).LastActivityAt;

        var result = await new ReferenceAllocationService(context).AllocateAsync(desk.DeskId, null, []);
        await context.SaveChangesAsync();

        Assert.Equal(ReferenceAllocationStatus.Allocated, result.Status);
        Assert.Empty(result.AssignedReferences);
        Assert.True((await context.DepositDesks.SingleAsync()).LastActivityAt >= before);
    }

    [Fact]
    public async Task AllocateAsync_ConcurrentTakeOfTheSameLetterFailsForTheSecondDesk()
    {
        // Two contexts on one database model two desks reading the same snapshot.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await using var first = await CreateContextAsync(connection);
        await using var second = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        // Desks without letters, so both runs must take the first free letter, B.
        var options = Options.Create(new LabelsOptions { LettersPerDesk = 0 });
        var deskA = await new DepositDeskService(first, options).OpenDeskAsync("A");
        var deskB = await new DepositDeskService(first, options).OpenDeskAsync("B");
        var sellerA = await SeedSellerAsync(first, 1);
        var sellerB = await SeedSellerAsync(first, 1);
        var itemB = await second.Items.SingleAsync(x => x.Id == sellerB.Items[0].Id);

        await new ReferenceAllocationService(first, options).AllocateAsync(deskA.DeskId, null, sellerA.Items);
        await new ReferenceAllocationService(second, options).AllocateAsync(deskB.DeskId, null, [itemB]);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~ReferenceAllocationServiceTests"`
Expected: build error, `ReferenceAllocationService` does not exist.

- [ ] **Step 3: Add the models**

Create `src/AssociationSaleTool.Web/Services/Models/ReferenceAllocationModels.cs`:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public enum ReferenceAllocationStatus
{
    Allocated,
    NoDesk,
    DeskClosed,
    NoLabelsLeft
}

public sealed record ReferenceAllocationResult(
    ReferenceAllocationStatus Status,
    IReadOnlyList<string> AssignedReferences,
    IReadOnlyList<char> TakenLetters)
{
    public static ReferenceAllocationResult Failed(ReferenceAllocationStatus status) => new(status, [], []);
}
```

- [ ] **Step 4: Implement the service**

Create `src/AssociationSaleTool.Web/Services/ReferenceAllocationService.cs`:

```csharp
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Services;

/// <summary>
/// Gives items their references from the letters a deposit desk holds. It only
/// changes tracked entities: the caller saves the items, the letter counters
/// and the desk's holdings in one SaveChanges, so a refused or failed save
/// leaves no number used. The concurrency tokens on reference_letter turn a
/// race with another desk into DbUpdateConcurrencyException at that save.
/// </summary>
public sealed class ReferenceAllocationService(AppDbContext dbContext, IOptions<LabelsOptions>? labelsOptions = null)
{
    private readonly int _lettersPerDesk = (labelsOptions?.Value ?? new LabelsOptions()).LettersPerDesk;

    public async Task<ReferenceAllocationResult> AllocateAsync(
        long? deskId,
        char? sellerLetter,
        IReadOnlyList<ItemEntity> items,
        CancellationToken cancellationToken = default)
    {
        if (deskId is null)
        {
            return ReferenceAllocationResult.Failed(ReferenceAllocationStatus.NoDesk);
        }

        var desk = await dbContext.DepositDesks.SingleOrDefaultAsync(x => x.Id == deskId, cancellationToken);
        if (desk is null || desk.ClosedAt is not null)
        {
            return ReferenceAllocationResult.Failed(ReferenceAllocationStatus.DeskClosed);
        }

        var labelsPerLetter = await dbContext.EventState.Select(x => x.LabelsPerLetter).SingleAsync(cancellationToken);
        var letters = await dbContext.ReferenceLetters.ToListAsync(cancellationToken);
        var assigned = new List<string>();
        var taken = new List<char>();

        if (items.Count > 0)
        {
            var plan = DeskLetterPicker.PlanRun(
                DepositDeskService.ToStates(letters),
                desk.Id,
                sellerLetter,
                items.Count,
                labelsPerLetter);
            if (plan is null)
            {
                return ReferenceAllocationResult.Failed(ReferenceAllocationStatus.NoLabelsLeft);
            }

            foreach (var letter in plan.LettersToTake)
            {
                Find(letters, letter).HeldByDeskId = desk.Id;
                taken.Add(letter);
            }

            var index = 0;
            foreach (var segment in plan.Segments)
            {
                var letter = Find(letters, segment.Letter);
                for (var i = 0; i < segment.Count; i++)
                {
                    letter.LastNumber++;
                    var reference = ItemReference.Format(segment.Letter, letter.LastNumber);
                    items[index++].Reference = reference;
                    assigned.Add(reference);
                }
            }
        }

        // An exhausted letter goes back to the pool, still exhausted, so no
        // other desk takes it; the desk then tops up to its usual count.
        foreach (var letter in letters.Where(x => x.HeldByDeskId == desk.Id && x.LastNumber >= labelsPerLetter))
        {
            letter.HeldByDeskId = null;
        }

        var usable = letters.Count(x => x.HeldByDeskId == desk.Id && x.LastNumber < labelsPerLetter);
        foreach (var letter in DeskLetterPicker.PickFreeLetters(
                     DepositDeskService.ToStates(letters),
                     _lettersPerDesk - usable,
                     labelsPerLetter))
        {
            Find(letters, letter).HeldByDeskId = desk.Id;
            taken.Add(letter);
        }

        desk.LastActivityAt = DateTime.UtcNow;
        return new ReferenceAllocationResult(ReferenceAllocationStatus.Allocated, assigned, taken);
    }

    private static ReferenceLetterEntity Find(IEnumerable<ReferenceLetterEntity> letters, char letter)
    {
        return letters.Single(x => x.Letter[0] == letter);
    }
}
```

Register it in `Program.cs`:

```csharp
builder.Services.AddScoped<ReferenceAllocationService>();
```

- [ ] **Step 5: Run the tests**

Run: `rtk dotnet test --filter "FullyQualifiedName~ReferenceAllocationServiceTests"`
Expected: 7 passed. Then `rtk dotnet test`: all pass.

- [ ] **Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Services/Models/ReferenceAllocationModels.cs src/AssociationSaleTool.Web/Services/ReferenceAllocationService.cs src/AssociationSaleTool.Web/Program.cs tests/AssociationSaleTool.Web.Tests/Services/ReferenceAllocationServiceTests.cs
rtk git commit -m "Allocate references from the letters a deposit desk holds"
```

---

### Task 5: IntakeService — start the deposit, allocate on save, drop contract-step allocation

**Files:**
- Create: `src/AssociationSaleTool.Web/Services/Models/DepositAllocationModels.cs`
- Modify: `src/AssociationSaleTool.Web/Services/Models/CreateItemBatchRequest.cs`
- Modify: `src/AssociationSaleTool.Web/Services/IntakeService.cs`
- Delete: `src/AssociationSaleTool.Web/Domain/Items/ReferenceAllocator.cs`, `src/AssociationSaleTool.Web/Domain/Items/ReferenceSpaceExhaustedException.cs`, `tests/AssociationSaleTool.Web.Tests/Domain/ReferenceAllocatorTests.cs`
- Modify: `src/AssociationSaleTool.Web/Configuration/LabelsOptions.cs`, `src/AssociationSaleTool.Web/appsettings.json`
- Modify tests: `tests/AssociationSaleTool.Web.Tests/Services/IntakeServiceTests.cs`, `tests/AssociationSaleTool.Web.Tests/Pages/SellersPageModelTests.cs`, `tests/AssociationSaleTool.Web.Tests/Configuration/OptionsBindingTests.cs`

**Interfaces:**
- Consumes: `ReferenceAllocationService.AllocateAsync` (Task 4), `DepositDeskService.OpenDeskAsync` (Task 3, in tests).
- Produces:
  - `enum DepositAllocationStatus { Done, NotFound, NotAllowed, NoDesk, DeskClosed, NoLabelsLeft, Conflict }`
  - `record DepositAllocationResult(DepositAllocationStatus Status, IReadOnlyList<ItemEntity> Items, IReadOnlyList<string> AssignedReferences, IReadOnlyList<char> TakenLetters)`
  - `CreateItemBatchRequest(long SellerId, IntakeMode IntakeMode, IReadOnlyList<CreateItemRowRequest> Rows, long? DepositDeskId = null)`
  - `IntakeService.StartDepositAsync(long sellerId, long? depositDeskId, CancellationToken ct = default)` → `Task<DepositAllocationResult>`
  - `IntakeService.CreateItemBatchAsync(CreateItemBatchRequest request, CancellationToken ct = default)` → `Task<DepositAllocationResult>` (was `List<ItemEntity>`)
  - `AllocateDepositReferencesAsync` no longer exists.

- [ ] **Step 1: Write the failing tests**

In `IntakeServiceTests.cs`, add a helper next to `CreateValidatableSellerAsync`:

```csharp
    private static async Task<long> OpenDeskAsync(AppDbContext context)
    {
        return (await new DepositDeskService(context).OpenDeskAsync(null)).DeskId;
    }
```

Replace every allocation test from `AllocateDepositReferencesAsync_AssignsAContiguousRunInTheSellerOwnLetter` through `AllocateDepositReferencesAsync_AfterCancellationOnlyFillsTheNewItems` (lines ~1506–1800) with:

```csharp
    [Fact]
    public async Task StartDepositAsync_GivesExistingRowsAConsecutiveRunAndMarksTheDepositStarted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration,
            [new CreateItemRowRequest("Puzzle", 500), new CreateItemRowRequest("Poupée", 300)]));
        var deskId = await OpenDeskAsync(context);

        var result = await service.StartDepositAsync(seller.Id, deskId);

        Assert.Equal(DepositAllocationStatus.Done, result.Status);
        Assert.Equal(["B001", "B002"], result.AssignedReferences);
        Assert.NotNull((await context.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
    }

    [Fact]
    public async Task StartDepositAsync_ForALiveSellerWithNoRowsOnlyMarksTheDepositStarted()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);

        var result = await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));

        Assert.Equal(DepositAllocationStatus.Done, result.Status);
        Assert.Empty(result.AssignedReferences);
        Assert.NotNull((await context.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
    }

    [Fact]
    public async Task StartDepositAsync_RefusesASecondStartWithoutAllocating()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        var deskId = await OpenDeskAsync(context);
        await service.StartDepositAsync(seller.Id, deskId);

        var second = await service.StartDepositAsync(seller.Id, deskId);

        Assert.Equal(DepositAllocationStatus.NotAllowed, second.Status);
        Assert.Equal(1, (await context.ReferenceLetters.SingleAsync(x => x.Letter == "B")).LastNumber);
    }

    [Fact]
    public async Task StartDepositAsync_WithoutADeskWritesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));

        var result = await service.StartDepositAsync(seller.Id, null);

        Assert.Equal(DepositAllocationStatus.NoDesk, result.Status);
        context.ChangeTracker.Clear();
        Assert.Null((await context.Sellers.SingleAsync(x => x.Id == seller.Id)).DepositStartedAt);
        Assert.Null((await context.Items.SingleAsync()).Reference);
    }

    [Fact]
    public async Task CreateItemBatchAsync_BeforeTheStartLeavesReferencesEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);

        var result = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)], await OpenDeskAsync(context)));

        Assert.Equal(DepositAllocationStatus.Done, result.Status);
        Assert.Null(Assert.Single(result.Items).Reference);
    }

    [Fact]
    public async Task CreateItemBatchAsync_AfterTheStartGivesEachRowTheNextNumberInTheSellerLetter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        var deskId = await OpenDeskAsync(context);
        await service.StartDepositAsync(seller.Id, deskId);

        var first = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Extra 1", 200)], deskId));
        var second = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Extra 2", 200)], deskId));

        Assert.Equal(["B002"], first.AssignedReferences);
        Assert.Equal(["B003"], second.AssignedReferences);
    }

    [Fact]
    public async Task CreateItemBatchAsync_OnAClosedDeskCreatesNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        var deskId = await OpenDeskAsync(context);
        await service.StartDepositAsync(seller.Id, deskId);
        await new DepositDeskService(context).CloseDeskAsync(deskId);

        var result = await service.CreateItemBatchAsync(new CreateItemBatchRequest(
            seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Extra", 200)], deskId));

        Assert.Equal(DepositAllocationStatus.DeskClosed, result.Status);
        context.ChangeTracker.Clear();
        Assert.Equal(0, await context.Items.CountAsync());
    }

    [Fact]
    public async Task CancelSellerDepositValidationAsync_KeepsReferencesAndTheStartedMarker()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var service = new IntakeService(context);
        var seller = await CreateValidatableSellerAsync(context, service);
        await service.CreateItemBatchAsync(new CreateItemBatchRequest(seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));
        await service.ValidateSellerDepositAsync(seller.Id);

        await service.CancelSellerDepositValidationAsync(seller.Id);

        context.ChangeTracker.Clear();
        Assert.Equal("B001", (await context.Items.SingleAsync()).Reference);
        Assert.NotNull((await context.Sellers.SingleAsync()).DepositStartedAt);
    }
```

Elsewhere in `IntakeServiceTests.cs` (lines ~1072, 1329, 1371, 1413), replace each `await service.AllocateDepositReferencesAsync(seller.Id);` with:

```csharp
        await service.StartDepositAsync(seller.Id, await OpenDeskAsync(context));
```

The three tests that keep the return value of `CreateItemBatchAsync` (lines ~472, 512, 561) now get a `DepositAllocationResult`: change their `createdItems`/`created` usages to `createdItems.Items` / `created.Items`.

If `CancelSellerDepositValidationAsync` takes more parameters than the seller id, keep the call shape used by the existing `CancelSellerDepositValidationAsync_KeepsAllocatedReferences` test you are replacing.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~IntakeServiceTests"`
Expected: build error, `StartDepositAsync` and `DepositAllocationStatus` do not exist.

- [ ] **Step 3: Add the models and the request field**

Create `src/AssociationSaleTool.Web/Services/Models/DepositAllocationModels.cs`:

```csharp
using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public enum DepositAllocationStatus
{
    Done,
    NotFound,
    NotAllowed,
    NoDesk,
    DeskClosed,
    NoLabelsLeft,
    Conflict
}

public sealed record DepositAllocationResult(
    DepositAllocationStatus Status,
    IReadOnlyList<ItemEntity> Items,
    IReadOnlyList<string> AssignedReferences,
    IReadOnlyList<char> TakenLetters)
{
    public static DepositAllocationResult Failed(DepositAllocationStatus status) => new(status, [], [], []);
}
```

Change `CreateItemBatchRequest.cs` to:

```csharp
namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateItemBatchRequest(
    long SellerId,
    IntakeMode IntakeMode,
    IReadOnlyList<CreateItemRowRequest> Rows,
    long? DepositDeskId = null);
```

- [ ] **Step 4: Change IntakeService**

Add a constructor parameter and field (keep the optional-parameter pattern):

```csharp
    private readonly ReferenceAllocationService _referenceAllocationService;
```

```csharp
    public IntakeService(
        AppDbContext dbContext,
        SalePhaseService? salePhaseService = null,
        SellerDuplicateService? sellerDuplicateService = null,
        IserePostalCityLookup? postalCityLookup = null,
        IOptions<LabelsOptions>? labelsOptions = null,
        ReferenceAllocationService? referenceAllocationService = null)
    {
        _dbContext = dbContext;
        _salePhaseService = salePhaseService ?? new SalePhaseService(dbContext);
        _sellerDuplicateService = sellerDuplicateService ?? new SellerDuplicateService(dbContext);
        _postalCityLookup = postalCityLookup ?? IserePostalCityLookup.Empty;
        _referenceAllocationService = referenceAllocationService ?? new ReferenceAllocationService(dbContext, labelsOptions);
    }
```

Remove the `_labelsOptions` field. Replace `CreateItemBatchAsync` with:

```csharp
    public async Task<DepositAllocationResult> CreateItemBatchAsync(CreateItemBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken))
        {
            throw new InvalidOperationException("Item master data is locked outside presale.");
        }

        EnsureRowsAreValid(request.Rows);

        var seller = await _dbContext.Sellers
            .Include(x => x.Items)
            .SingleAsync(x => x.Id == request.SellerId, cancellationToken);
        if (seller.DepositStatus != SellerDepositStatuses.Pending)
        {
            throw new InvalidOperationException("Seller deposit is already validated.");
        }

        var sellerLetter = LatestReferenceLetter(seller);
        var createdItems = AddItemRows(seller, request.Rows);
        var allocation = new ReferenceAllocationResult(ReferenceAllocationStatus.Allocated, [], []);
        if (seller.DepositStartedAt is not null)
        {
            allocation = await _referenceAllocationService.AllocateAsync(
                request.DepositDeskId, sellerLetter, createdItems, cancellationToken);
            if (allocation.Status != ReferenceAllocationStatus.Allocated)
            {
                _dbContext.ChangeTracker.Clear();
                return DepositAllocationResult.Failed(ToDepositStatus(allocation.Status));
            }
        }

        return await SaveAllocationAsync(createdItems, allocation, cancellationToken);
    }
```

Replace `AllocateDepositReferencesAsync` (the whole method) with:

```csharp
    public async Task<DepositAllocationResult> StartDepositAsync(
        long sellerId,
        long? depositDeskId,
        CancellationToken cancellationToken = default)
    {
        var seller = await _dbContext.Sellers
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null)
        {
            return DepositAllocationResult.Failed(DepositAllocationStatus.NotFound);
        }

        if (!await _salePhaseService.CanEditItemMasterDataAsync(cancellationToken)
            || seller.DepositStatus != SellerDepositStatuses.Pending
            || seller.DepositStartedAt is not null)
        {
            return DepositAllocationResult.Failed(DepositAllocationStatus.NotAllowed);
        }

        var pending = seller.Items
            .Where(item => item.Reference is null)
            .OrderBy(item => item.Id)
            .ToList();
        var allocation = await _referenceAllocationService.AllocateAsync(
            depositDeskId, LatestReferenceLetter(seller), pending, cancellationToken);
        if (allocation.Status != ReferenceAllocationStatus.Allocated)
        {
            _dbContext.ChangeTracker.Clear();
            return DepositAllocationResult.Failed(ToDepositStatus(allocation.Status));
        }

        seller.DepositStartedAt = DateTime.UtcNow;
        return await SaveAllocationAsync(pending, allocation, cancellationToken);
    }

    private async Task<DepositAllocationResult> SaveAllocationAsync(
        IReadOnlyList<ItemEntity> items,
        ReferenceAllocationResult allocation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another desk wrote one of these letters since we read it; nothing
            // was saved, and the operator re-submits.
            _dbContext.ChangeTracker.Clear();
            return DepositAllocationResult.Failed(DepositAllocationStatus.Conflict);
        }

        return new DepositAllocationResult(
            DepositAllocationStatus.Done,
            items,
            allocation.AssignedReferences,
            allocation.TakenLetters);
    }

    private static char? LatestReferenceLetter(SellerEntity seller)
    {
        return seller.Items
            .Where(item => item.Reference is not null)
            .OrderByDescending(item => item.Id)
            .Select(item => (char?)item.Reference![0])
            .FirstOrDefault();
    }

    private static DepositAllocationStatus ToDepositStatus(ReferenceAllocationStatus status)
    {
        return status switch
        {
            ReferenceAllocationStatus.NoDesk => DepositAllocationStatus.NoDesk,
            ReferenceAllocationStatus.DeskClosed => DepositAllocationStatus.DeskClosed,
            ReferenceAllocationStatus.NoLabelsLeft => DepositAllocationStatus.NoLabelsLeft,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
```

Delete `HighestNumberForLetter` and any other helper only `AllocateDepositReferencesAsync` used (the compiler lists them as unused or missing). Delete `ReferenceAllocator.cs`, `ReferenceSpaceExhaustedException.cs` and `ReferenceAllocatorTests.cs`.

In `LabelsOptions.cs`, delete `SoftCapacityPerLetter` and its comment. In `appsettings.json`, the `Labels` section becomes:

```json
  "Labels": {
    "LettersPerDesk": 2
  },
```

Register nothing new: `IntakeService` receives `ReferenceAllocationService` from DI through the new constructor parameter.

- [ ] **Step 5: Keep the fee step compiling until Task 7 rewires it**

In `Pages/Sellers/Details.cshtml.cs`, `OnPostUpdateDepositFeeAsync` calls the removed method. Delete the block from `var allocation = await intakeService.AllocateDepositReferencesAsync(id, cancellationToken);` through its `NotAllowed` error return, so the handler goes straight from saving the fee to reloading the seller. In `OnPostAsync`, the `CreateItemBatchAsync` call still compiles (its result is ignored); Task 7 adds the desk id.

- [ ] **Step 6: Update the other tests**

`SellersPageModelTests.cs` line ~865: replace `await service.AllocateDepositReferencesAsync(seller.Id);` with

```csharp
        await service.StartDepositAsync(seller.Id, (await new DepositDeskService(context).OpenDeskAsync(null)).DeskId);
```

(use the context variable name that test already uses). Update the comment at line ~906 to say "Deliberately skip StartDepositAsync".

`OptionsBindingTests.cs`: change the `Labels:SoftCapacityPerLetter = "310"` binding assertion to bind `Labels:LettersPerDesk = "3"` and assert `LettersPerDesk == 3`. Replace `LabelsOptions_SoftCapacityPerLetterReachesIntakeServiceThroughDependencyInjection` with a test of the same shape that sets `["Labels:LettersPerDesk"] = "1"`, resolves `DepositDeskService` from the factory's scope, opens a desk, and asserts `Assert.Single(desk.Letters)`. Rename it `LabelsOptions_LettersPerDeskReachesDepositDeskServiceThroughDependencyInjection`.

- [ ] **Step 7: Run all tests**

Run: `rtk dotnet test`
Expected: all pass. Fix any remaining compile errors the removal surfaces by following the same replacements.

- [ ] **Step 8: Commit**

```bash
rtk git add -A src/AssociationSaleTool.Web tests/AssociationSaleTool.Web.Tests
rtk git commit -m "Start deposits at a desk and allocate references when rows are saved"
```

Check `rtk git status` before committing: only files listed in this task should be staged (the `resources/` and `PublishProfiles/` files must stay untracked).

---

### Task 6: Desk cookie, desk page and banner

**Files:**
- Create: `src/AssociationSaleTool.Web/Infrastructure/DepositDeskCookie.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Sellers/Desk.cshtml`, `Desk.cshtml.cs`
- Create: `src/AssociationSaleTool.Web/Pages/Shared/_DepositDeskBanner.cshtml`
- Modify: `src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml`
- Modify: `src/AssociationSaleTool.Web/wwwroot/css/site.css`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/DepositDeskSmokeTests.cs`

**Interfaces:**
- Consumes: `DepositDeskService` (Task 3).
- Produces:
  - `DepositDeskCookie.Name` = `"poste-depot"`, `Read(HttpRequest)` → `string?`, `Write(HttpResponse, string token)`, `Delete(HttpResponse)`
  - route `/vendeurs/poste` with handlers `Open` and `Close`

- [ ] **Step 1: Write the failing smoke tests**

Create `tests/AssociationSaleTool.Web.Tests/Smoke/DepositDeskSmokeTests.cs`. Use the existing `IntakeWebApplicationFactory`; the antiforgery helper below mirrors what `WebApplicationSmokeTests` does for form posts (reuse its helper if one exists):

```csharp
using System.Net;
using System.Text.RegularExpressions;
using AssociationSaleTool.Web.Tests.Infrastructure;

namespace AssociationSaleTool.Web.Tests.Smoke;

public sealed class DepositDeskSmokeTests(IntakeWebApplicationFactory factory) : IClassFixture<IntakeWebApplicationFactory>
{
    private static async Task<string> AntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        return Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
    }

    [Fact]
    public async Task SellerPagesShowThatNoDeskIsOpen()
    {
        var client = factory.CreateClient();

        var content = WebUtility.HtmlDecode(await client.GetStringAsync("/vendeurs"));

        Assert.Contains("Aucun poste de dépôt ouvert sur cet ordinateur.", content);
        Assert.Contains("href=\"/vendeurs/poste\"", content);
    }

    [Fact]
    public async Task OpeningADeskShowsItsLettersInTheBannerAndClosingItRemovesTheCookie()
    {
        var client = factory.CreateClient();
        var token = await AntiforgeryTokenAsync(client, "/vendeurs/poste");

        var opened = await client.PostAsync("/vendeurs/poste?handler=Open", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DeskName"] = "Poste test"
        }));
        var page = WebUtility.HtmlDecode(await opened.Content.ReadAsStringAsync());

        Assert.Contains("Poste test · lettres", page);
        Assert.Contains("Allez chercher les planches", page);

        token = await AntiforgeryTokenAsync(client, "/vendeurs/poste");
        var closed = await client.PostAsync("/vendeurs/poste?handler=Close", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Contains("Aucun poste de dépôt ouvert sur cet ordinateur.", WebUtility.HtmlDecode(await closed.Content.ReadAsStringAsync()));
    }
}
```

If the factory client does not keep cookies between requests, create it with `factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true })` (add `using Microsoft.AspNetCore.Mvc.Testing;`).

- [ ] **Step 2: Run them to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~DepositDeskSmokeTests"`
Expected: FAIL, the banner text and `/vendeurs/poste` do not exist (404).

- [ ] **Step 3: Add the cookie helper**

Create `src/AssociationSaleTool.Web/Infrastructure/DepositDeskCookie.cs`:

```csharp
namespace AssociationSaleTool.Web.Infrastructure;

/// <summary>
/// Identifies this computer's deposit desk. A cookie rather than a form field:
/// the deposit pages are plain form posts rendered by the server, so the desk
/// reaches every page and every save without client-side script.
/// </summary>
public static class DepositDeskCookie
{
    public const string Name = "poste-depot";

    public static string? Read(HttpRequest request)
    {
        return request.Cookies.TryGetValue(Name, out var token) ? token : null;
    }

    public static void Write(HttpResponse response, string token)
    {
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(4)
        });
    }

    public static void Delete(HttpResponse response)
    {
        response.Cookies.Delete(Name);
    }
}
```

- [ ] **Step 4: Add the desk page**

Create `src/AssociationSaleTool.Web/Pages/Sellers/Desk.cshtml.cs`:

```csharp
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class DeskModel(DepositDeskService depositDeskService) : PageModel
{
    [BindProperty]
    public string? DeskName { get; set; }

    public CurrentDesk Desk { get; private set; } = CurrentDesk.None;

    public IReadOnlyList<char> JustOpenedLetters { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
    }

    public async Task<IActionResult> OnPostOpenAsync(CancellationToken cancellationToken)
    {
        var current = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        if (current.State == DeskState.Open)
        {
            Desk = current;
            ModelState.AddModelError(string.Empty, "Un poste est déjà ouvert sur cet ordinateur.");
            return Page();
        }

        var opened = await depositDeskService.OpenDeskAsync(DeskName, cancellationToken);
        DepositDeskCookie.Write(Response, opened.Token);
        JustOpenedLetters = opened.Letters;
        Desk = await depositDeskService.GetCurrentDeskAsync(opened.Token, cancellationToken);
        // The banner reads the request cookie, which this response only sets.
        HttpContext.Items[DepositDeskCookie.Name] = opened.Token;
        return Page();
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken cancellationToken)
    {
        var current = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        if (current.DeskId is { } deskId)
        {
            await depositDeskService.CloseDeskAsync(deskId, cancellationToken);
        }

        DepositDeskCookie.Delete(Response);
        HttpContext.Items[DepositDeskCookie.Name] = string.Empty;
        Desk = CurrentDesk.None;
        return Page();
    }
}
```

Create `src/AssociationSaleTool.Web/Pages/Sellers/Desk.cshtml`:

```cshtml
@page "/vendeurs/poste"
@model AssociationSaleTool.Web.Pages.Sellers.DeskModel
@using AssociationSaleTool.Web.Services.Models
@{
    ViewData["Title"] = "Poste de dépôt";
}

<section class="page-card">
    <div class="page-header">
        <div>
            <p class="eyebrow">Dépôt</p>
            <h1 class="page-title">Poste de dépôt</h1>
            <p class="seller-meta">Un poste par ordinateur. Chaque poste reçoit ses lettres d'étiquettes : ne prenez que les planches de ces lettres.</p>
        </div>
    </div>

    <div asp-validation-summary="ModelOnly" class="validation"></div>

    @if (Model.JustOpenedLetters.Count > 0)
    {
        <p class="deposit-desk-letters-callout">
            Allez chercher les planches @string.Join(" et ", Model.JustOpenedLetters).
        </p>
    }

    @if (Model.Desk.State == DeskState.Open)
    {
        <section class="subsection">
            <h2>@Model.Desk.Name</h2>
            @if (Model.Desk.Letters.Count == 0)
            {
                <p class="validation">Aucune lettre disponible pour ce poste. Une lettre lui sera attribuée dès qu'un autre poste en libère une.</p>
            }
            else
            {
                <ul class="deposit-desk-letter-list">
                    @foreach (var letter in Model.Desk.Letters)
                    {
                        <li><strong>@letter.Letter</strong> : @letter.LabelsLeft étiquettes restantes</li>
                    }
                </ul>
            }
            <form method="post" asp-page-handler="Close" class="inline-form"
                  onsubmit="return confirm('Fermer ce poste ? Rapportez ses planches d\'étiquettes sur la table centrale.');">
                <button type="submit" class="button-link">Fermer le poste</button>
            </form>
            <p class="seller-meta">Fermez le poste avant de ranger l'ordinateur, et rapportez ses planches sur la table centrale.</p>
        </section>
    }
    else
    {
        <section class="subsection">
            @if (Model.Desk.State == DeskState.Closed)
            {
                <p class="validation">Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.</p>
            }
            <form method="post" asp-page-handler="Open" class="stack-form">
                <label>
                    <span>Nom du poste (facultatif)</span>
                    <input asp-for="DeskName" autocomplete="off" placeholder="Poste 1" />
                </label>
                <button type="submit" class="button-link button-link-solid">Ouvrir un poste</button>
            </form>
        </section>
    }
</section>
```

- [ ] **Step 5: Add the banner**

Create `src/AssociationSaleTool.Web/Pages/Shared/_DepositDeskBanner.cshtml`:

```cshtml
@using AssociationSaleTool.Web.Infrastructure
@using AssociationSaleTool.Web.Services
@using AssociationSaleTool.Web.Services.Models
@inject DepositDeskService DepositDeskService
@{
    // A desk page that just opened or closed the desk passes the new token in
    // HttpContext.Items, because the request cookie still holds the old one.
    var token = Context.Items.TryGetValue(DepositDeskCookie.Name, out var fresh)
        ? fresh as string
        : DepositDeskCookie.Read(Context.Request);
    var desk = await DepositDeskService.GetCurrentDeskAsync(token);
}
<div class="deposit-desk-banner" data-deposit-desk-banner>
    @if (desk.State == DeskState.Open)
    {
        <a href="/vendeurs/poste">@desk.Name · lettres @string.Join(", ", desk.Letters.Select(x => x.Letter))</a>
        @foreach (var letter in desk.Letters.Where(x => x.LabelsLeft <= DepositDeskService.LowLabelsThreshold))
        {
            <span class="deposit-desk-warning">Plus que @letter.LabelsLeft étiquettes en @letter.Letter.</span>
        }
    }
    else if (desk.State == DeskState.Closed)
    {
        <span class="deposit-desk-warning">Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.</span>
        <a href="/vendeurs/poste">Ouvrir un poste</a>
    }
    else
    {
        <span>Aucun poste de dépôt ouvert sur cet ordinateur.</span>
        <a href="/vendeurs/poste">Ouvrir un poste</a>
    }
</div>
```

In `_Layout.cshtml`, right after the closing `</header>` of `app-status-header`:

```cshtml
    @if (sellersActive)
    {
        <partial name="_DepositDeskBanner" />
    }
```

Append to `site.css` (colors from existing tokens only; `tests/css-variables.test.mjs` fails on undeclared variables):

```css
.deposit-desk-banner {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 1rem;
  width: min(var(--page-max-width), calc(100% - 3rem));
  margin: 0.75rem auto 0;
  padding: 0.5rem 1rem;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: var(--panel);
}

.deposit-desk-warning {
  font-weight: 700;
  color: var(--accent-strong);
}

.deposit-desk-letters-callout {
  font-size: 1.5rem;
  font-weight: 700;
}
```

- [ ] **Step 6: Run the tests**

Run: `rtk dotnet test --filter "FullyQualifiedName~DepositDeskSmokeTests"`, then `rtk dotnet test` and the Node suite (`for f in tests/*.test.mjs; do node --test "$f"; done`).
Expected: all pass.

- [ ] **Step 7: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Infrastructure/DepositDeskCookie.cs src/AssociationSaleTool.Web/Pages/Sellers/Desk.cshtml src/AssociationSaleTool.Web/Pages/Sellers/Desk.cshtml.cs src/AssociationSaleTool.Web/Pages/Shared/_DepositDeskBanner.cshtml src/AssociationSaleTool.Web/Pages/Shared/_Layout.cshtml src/AssociationSaleTool.Web/wwwroot/css/site.css tests/AssociationSaleTool.Web.Tests/Smoke/DepositDeskSmokeTests.cs
rtk git commit -m "Open and close a deposit desk from its computer and show it on seller pages"
```

---

### Task 7: Seller page — Commencer le dépôt, allocation on save, finalization guard

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/Sellers/Details.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/DepositDeskSmokeTests.cs`

**Interfaces:**
- Consumes: `IntakeService.StartDepositAsync`, `CreateItemBatchAsync` with `DepositDeskId`, `DepositAllocationStatus` (Task 5); `DepositDeskService.GetCurrentDeskAsync`, `DepositDeskCookie.Read` (Tasks 3 and 6).
- Produces: handler `StartDeposit` on `/vendeurs/{id}`; `DetailsModel.ToAllocationMessage(DepositAllocationResult)`.

- [ ] **Step 1: Write the failing smoke tests**

Add to `DepositDeskSmokeTests.cs`. The seeding uses the app's own services so it does not depend on other smoke-test helpers:

```csharp
    private async Task<long> SeedPreregisteredSellerAsync(int itemCount)
    {
        using var scope = factory.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<AssociationSaleTool.Web.Services.IntakeService>();
        var seller = await intake.CreateSellerAsync(new AssociationSaleTool.Web.Services.Models.CreateSellerRequest(
            "Alex", $"Depot{Guid.NewGuid():N}"[..12], null, "0601020304", "1 rue des Tests", null));
        await intake.CreateItemBatchAsync(new AssociationSaleTool.Web.Services.Models.CreateItemBatchRequest(
            seller.Id,
            AssociationSaleTool.Web.Services.Models.IntakeMode.EmailPreregistration,
            Enumerable.Range(1, itemCount).Select(i => new AssociationSaleTool.Web.Services.Models.CreateItemRowRequest($"Jouet {i}", 300)).ToList()));
        return seller.Id;
    }

    private async Task<HttpClient> ClientWithOpenDeskAsync(string deskName = "")
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var token = await AntiforgeryTokenAsync(client, "/vendeurs/poste");
        await client.PostAsync("/vendeurs/poste?handler=Open", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DeskName"] = deskName
        }));
        return client;
    }

    [Fact]
    public async Task StartingADepositShowsTheAllocatedReferences()
    {
        var client = await ClientWithOpenDeskAsync();
        var sellerId = await SeedPreregisteredSellerAsync(3);
        var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

        var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=StartDeposit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Matches(@"Références attribuées : [A-Z]\d{3} à [A-Z]\d{3}\.", content);
        Assert.DoesNotContain("Commencer le dépôt</button>", content);
    }

    [Fact]
    public async Task StartingADepositWithoutADeskIsRefused()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        var sellerId = await SeedPreregisteredSellerAsync(1);
        var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

        var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=StartDeposit", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Contains(
            "Ouvrez un poste de dépôt sur cet ordinateur pour attribuer des références.",
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task FinalizingANotStartedDepositIsRefused()
    {
        var client = await ClientWithOpenDeskAsync();
        var sellerId = await SeedPreregisteredSellerAsync(1);
        var token = await AntiforgeryTokenAsync(client, $"/vendeurs/{sellerId}");

        var response = await client.PostAsync($"/vendeurs/{sellerId}?handler=BeginDepositFinalization", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));

        Assert.Contains(
            "Commencez le dépôt pour attribuer les références avant de le finaliser.",
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
```

Add `using Microsoft.Extensions.DependencyInjection;` to the file.

- [ ] **Step 2: Run them to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~DepositDeskSmokeTests"`
Expected: the three new tests FAIL (no `StartDeposit` handler; finalization not guarded).

- [ ] **Step 3: Add the start handler and messages to the page model**

In `DetailsModel`, add the dependency to the primary constructor (keep `adminOptions` last):

```csharp
public sealed class DetailsModel(
    IntakeService intakeService,
    DepositDeskService depositDeskService,
    IOptions<AdminOptions>? adminOptions = null) : PageModel
```

Update every `new DetailsModel(...)` in `SellersPageModelTests.cs` to pass `new DepositDeskService(context)` as the second argument.

Add properties:

```csharp
    [TempData]
    public string? AllocationMessage { get; set; }

    public bool IsDepositStarted => Seller?.DepositStartedAt is not null;
```

Add the handler:

```csharp
    public async Task<IActionResult> OnPostStartDepositAsync(long id, CancellationToken cancellationToken)
    {
        var desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        var result = await intakeService.StartDepositAsync(id, desk.State == DeskState.Open ? desk.DeskId : null, cancellationToken);
        if (result.Status == DepositAllocationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == DepositAllocationStatus.Done)
        {
            AllocationMessage = ToAllocationMessage(result);
            return RedirectToPage("/Sellers/Details", null, new { id }, "articles");
        }

        var loaded = await intakeService.GetSellerWithItemsAsync(id, cancellationToken);
        if (loaded is null)
        {
            return NotFound();
        }

        PopulateSellerState(loaded);
        Input = CreateDefaultInput();
        await PopulatePermissionStateAsync(loaded, cancellationToken);
        ModelState.AddModelError(string.Empty, ToAllocationError(result.Status, desk.State));
        return Page();
    }

    public static string ToAllocationMessage(DepositAllocationResult result)
    {
        var parts = new List<string>();
        var references = result.AssignedReferences;
        if (references.Count == 1)
        {
            parts.Add($"Référence attribuée : {references[0]}.");
        }
        else if (references.Count > 1)
        {
            // A run split across letters cannot be written as a range.
            var contiguous = references
                .Skip(1)
                .Select((reference, index) => (Previous: references[index], Current: reference))
                .All(pair => pair.Current[0] == pair.Previous[0]
                    && int.Parse(pair.Current[1..]) == int.Parse(pair.Previous[1..]) + 1);
            parts.Add(contiguous
                ? $"Références attribuées : {references[0]} à {references[^1]}."
                : $"Références attribuées : {string.Join(", ", references)}.");
        }
        else
        {
            parts.Add("Dépôt commencé. Chaque article enregistré recevra sa référence.");
        }

        foreach (var letter in result.TakenLetters)
        {
            parts.Add($"Allez chercher la planche {letter}.");
        }

        return string.Join(" ", parts);
    }

    private static string ToAllocationError(DepositAllocationStatus status, DeskState deskState)
    {
        return status switch
        {
            DepositAllocationStatus.NoDesk or DepositAllocationStatus.DeskClosed when deskState == DeskState.Closed =>
                "Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.",
            DepositAllocationStatus.NoDesk or DepositAllocationStatus.DeskClosed =>
                "Ouvrez un poste de dépôt sur cet ordinateur pour attribuer des références.",
            DepositAllocationStatus.NoLabelsLeft =>
                "Plus aucune étiquette disponible pour ce poste. Demandez à l'administration d'augmenter le nombre d'étiquettes par lettre ou de libérer une lettre.",
            DepositAllocationStatus.Conflict =>
                "Un autre poste vient de modifier ces lettres. Réessayez.",
            _ => "Le dépôt ne peut pas être commencé dans le statut actuel de la vente."
        };
    }
```

Add `using AssociationSaleTool.Web.Infrastructure;` and `using AssociationSaleTool.Web.Services.Models;` if missing.

- [ ] **Step 4: Pass the desk when adding rows**

In `OnPostAsync`, replace the `CreateItemBatchAsync` call and its redirect with:

```csharp
        var desk = await depositDeskService.GetCurrentDeskAsync(DepositDeskCookie.Read(Request), cancellationToken);
        var result = await intakeService.CreateItemBatchAsync(
            new CreateItemBatchRequest(id, IntakeMode.EmailPreregistration, rowsToCreate, desk.State == DeskState.Open ? desk.DeskId : null),
            cancellationToken);
        if (result.Status != DepositAllocationStatus.Done)
        {
            ModelState.AddModelError(string.Empty, ToAllocationError(result.Status, desk.State));
            return Page();
        }

        if (result.AssignedReferences.Count > 0)
        {
            AllocationMessage = ToAllocationMessage(result);
        }

        return RedirectToPage("/Sellers/Details", null, new { id }, "articles");
```

The rows the operator typed stay in `Input` on refusal, so nothing is lost.

- [ ] **Step 5: Guard finalization**

In `BuildDepositValidationState`, after the item checks, add:

```csharp
        if (seller.DepositStartedAt is null)
        {
            messages.Add("Commencez le dépôt pour attribuer les références avant de le finaliser.");
            hasItemMessages = true;
        }
```

In `OnPostValidateDepositAsync`, the existing `referencesNotYetAllocated` message branch becomes unreachable for started deposits but stays as the last line of defense; leave it.

- [ ] **Step 6: Update the markup**

In `Details.cshtml`, show the allocation message under the existing `DepositSuccessMessage` block:

```cshtml
    @if (!string.IsNullOrWhiteSpace(Model.AllocationMessage))
    {
        <p class="success-message deposit-allocation-message">@Model.AllocationMessage</p>
    }
```

Inside `<div class="form-actions seller-deposit-actions">`, before the `@if (Model.CanValidateDeposit)` block:

```cshtml
            @if (Model.CanEditItemMasterData && !Model.IsDepositValidated && !Model.IsDepositStarted)
            {
                <form method="post"
                      asp-page-handler="StartDeposit"
                      asp-route-id="@Model.Seller.Id"
                      class="inline-form">
                    <button type="submit" class="button-link button-link-solid">Commencer le dépôt</button>
                </form>
            }
```

Make the item table list rows by reference once the deposit is started, so the operator matches toys to the peeled run in order. In `PopulateSellerState`, where `Items` is assigned, use:

```csharp
        Items = seller.Items
            .OrderBy(item => item.Reference is null)
            .ThenBy(item => item.Reference)
            .ThenBy(item => item.Id)
            .ToList();
```

(keep the existing assignment's other filtering, if any).

- [ ] **Step 7: Run all tests**

Run: `rtk dotnet test` and the Node suite.
Expected: all pass. `SellersPageModelTests` and the deposit smoke tests in `WebApplicationSmokeTests.cs` that finalize a deposit now need the deposit started first: where such a test seeds a seller and then posts `BeginDepositFinalization` or `UpdateDepositFee`, start the deposit in the seed (`await intake.StartDepositAsync(seller.Id, (await scope.ServiceProvider.GetRequiredService<DepositDeskService>().OpenDeskAsync(null)).DeskId);`). Fix each failure that way, not by weakening the guard.

- [ ] **Step 8: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/Sellers tests/AssociationSaleTool.Web.Tests
rtk git commit -m "Start a seller's deposit at the desk and label rows as they are saved"
```

---

### Task 8: Administration — labels per letter, letters, open desks, force close

**Files:**
- Modify: `src/AssociationSaleTool.Web/Pages/SaleStatus/Index.cshtml.cs`
- Modify: `src/AssociationSaleTool.Web/Pages/SaleStatus/Index.cshtml`
- Test: `tests/AssociationSaleTool.Web.Tests/Smoke/DepositDeskSmokeTests.cs`

**Interfaces:**
- Consumes: `DepositDeskService.SetLabelsPerLetterAsync`, `GetLabelsPerLetterAsync`, `ListLettersAsync`, `ListOpenDesksAsync`, `CloseDeskAsync` (Task 3); `SaleStatusAdminSession` (existing).
- Produces: handlers `SaveLabelsPerLetter` and `ForceCloseDesk` on `/administration`.

- [ ] **Step 1: Write the failing smoke tests**

Add to `DepositDeskSmokeTests.cs`. Unlock the admin page the way `WebApplicationSmokeTests` does (it posts the shared password to `/administration?handler=Unlock`; reuse its helper or copy its form fields):

```csharp
    private async Task UnlockAdminAsync(HttpClient client)
    {
        var token = await AntiforgeryTokenAsync(client, "/administration");
        await client.PostAsync("/administration?handler=Unlock", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["PasswordInput.Password"] = "test-password"
        }));
    }

    [Fact]
    public async Task AdminForceCloseMakesTheDeskComputerSeeItsDeskClosed()
    {
        // ASCII name: the regex runs on raw HTML, where Razor encodes accents.
        var deskClient = await ClientWithOpenDeskAsync("PosteAFermer");
        var admin = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        await UnlockAdminAsync(admin);
        var adminPage = await admin.GetStringAsync("/administration");
        var deskId = Regex.Match(adminPage, "PosteAFermer</td>[\\s\\S]*?name=\"DeskId\" value=\"(\\d+)\"").Groups[1].Value;
        var token = Regex.Match(adminPage, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        await admin.PostAsync("/administration?handler=ForceCloseDesk", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["DeskId"] = deskId
        }));

        var sellers = WebUtility.HtmlDecode(await deskClient.GetStringAsync("/vendeurs"));
        Assert.Contains("Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.", sellers);
    }

    [Fact]
    public async Task AdminCannotSetLabelsPerLetterAboveTheFormatLimit()
    {
        var admin = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        await UnlockAdminAsync(admin);
        var token = await AntiforgeryTokenAsync(admin, "/administration");

        var response = await admin.PostAsync("/administration?handler=SaveLabelsPerLetter", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["LabelsPerLetterInput"] = "1000"
        }));

        Assert.Contains(
            "Le nombre d'étiquettes par lettre doit être compris entre 1 et 999.",
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
```

The factory configures `Admin:SharedPassword` as `test-password` (`IntakeWebApplicationFactory.cs`). The force-close test opens its desk under a unique ASCII name and matches the `DeskId` input in that desk's row, because other tests in the class leave desks open.

- [ ] **Step 2: Run them to verify they fail**

Run: `rtk dotnet test --filter "FullyQualifiedName~DepositDeskSmokeTests"`
Expected: the two new tests FAIL.

- [ ] **Step 3: Extend the page model**

Add `DepositDeskService depositDeskService` to `IndexModel`'s primary constructor (before `IOptions<AdminOptions> adminOptions`) and update any `new IndexModel(...)` in tests accordingly.

Add properties and handlers:

```csharp
    public int LabelsPerLetter { get; private set; }

    public IReadOnlyList<ReferenceLetterRow> ReferenceLetters { get; private set; } = [];

    public IReadOnlyList<OpenDeskRow> OpenDesks { get; private set; } = [];

    [BindProperty]
    public string? LabelsPerLetterInput { get; set; }

    [BindProperty]
    public long DeskId { get; set; }

    public async Task<IActionResult> OnPostSaveLabelsPerLetterAsync(CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = int.TryParse(LabelsPerLetterInput, NumberStyles.None, FrenchCulture, out var value)
            ? await depositDeskService.SetLabelsPerLetterAsync(value, cancellationToken)
            : new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.OutOfRange);

        if (result.Status == LabelsPerLetterUpdateStatus.Saved)
        {
            SuccessMessage = "Nombre d'étiquettes par lettre enregistré.";
            return RedirectToPage();
        }

        ModelState.AddModelError(
            nameof(LabelsPerLetterInput),
            result.Status == LabelsPerLetterUpdateStatus.BelowUsed
                ? $"Le nombre d'étiquettes par lettre ne peut pas être inférieur à {result.HighestUsed}, déjà utilisé pour la lettre {result.Letter}."
                : "Le nombre d'étiquettes par lettre doit être compris entre 1 et 999.");
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostForceCloseDeskAsync(CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        SuccessMessage = await depositDeskService.CloseDeskAsync(DeskId, cancellationToken)
            ? "Poste fermé. Ses lettres sont de nouveau disponibles."
            : "Ce poste était déjà fermé.";
        return RedirectToPage();
    }
```

In `LoadPageAsync`, add:

```csharp
        LabelsPerLetter = await depositDeskService.GetLabelsPerLetterAsync(cancellationToken);
        LabelsPerLetterInput ??= LabelsPerLetter.ToString(FrenchCulture);
        ReferenceLetters = await depositDeskService.ListLettersAsync(cancellationToken);
        OpenDesks = await depositDeskService.ListOpenDesksAsync(cancellationToken);
```

- [ ] **Step 4: Add the section to the page**

In `SaleStatus/Index.cshtml`, after the `Caisses` section (inside the unlocked part), add:

```cshtml
        <section class="subsection">
            <h2>Postes de dépôt et étiquettes</h2>

            <form method="post" asp-page-handler="SaveLabelsPerLetter" class="stack-form">
                <label for="labels-per-letter">
                    Étiquettes imprimées par lettre
                    <input id="labels-per-letter" name="LabelsPerLetterInput" value="@Model.LabelsPerLetterInput"
                           type="text" inputmode="numeric" pattern="[0-9]+" autocomplete="off" />
                </label>
                <span asp-validation-for="LabelsPerLetterInput" class="validation"></span>
                <p class="seller-meta">Augmentez ce nombre si vous imprimez des planches supplémentaires. Il ne peut pas descendre sous un numéro déjà utilisé.</p>
                <button type="submit" class="button-link">Enregistrer</button>
            </form>

            <div class="data-list-scroll">
            <table class="data-table">
                <thead>
                    <tr><th>Lettre</th><th>Utilisées</th><th>Restantes</th><th>Poste</th></tr>
                </thead>
                <tbody>
                @foreach (var letter in Model.ReferenceLetters)
                {
                    <tr>
                        <td>@letter.Letter</td>
                        <td>@letter.Used</td>
                        <td>@(letter.LabelsLeft == 0 ? "Épuisée" : letter.LabelsLeft.ToString())</td>
                        <td>@(letter.HeldByDeskName ?? "Libre")</td>
                    </tr>
                }
                </tbody>
            </table>
            </div>

            <h3>Postes ouverts</h3>
            @if (Model.OpenDesks.Count == 0)
            {
                <p class="empty-state">Aucun poste ouvert.</p>
            }
            else
            {
                <div class="data-list-scroll">
                <table class="data-table">
                    <thead>
                        <tr><th>Poste</th><th>Lettres</th><th>Ouvert le</th><th>Dernière activité</th><th></th></tr>
                    </thead>
                    <tbody>
                    @foreach (var desk in Model.OpenDesks)
                    {
                        <tr>
                            <td>@desk.Name</td>
                            <td>@(desk.Letters.Count == 0 ? "Aucune" : string.Join(", ", desk.Letters))</td>
                            <td>@desk.OpenedAt.ToLocalTime().ToString("dd/MM HH:mm")</td>
                            <td>@desk.LastActivityAt.ToLocalTime().ToString("dd/MM HH:mm")</td>
                            <td>
                                <form method="post" asp-page-handler="ForceCloseDesk" class="inline-form"
                                      onsubmit="return confirm('Forcer la fermeture de ce poste ? Ses lettres redeviendront disponibles pour les autres postes.');">
                                    <input type="hidden" name="DeskId" value="@desk.Id" />
                                    <button type="submit" class="button-link">Forcer la fermeture</button>
                                </form>
                            </td>
                        </tr>
                    }
                    </tbody>
                </table>
                </div>
            }
        </section>
```

- [ ] **Step 5: Run all tests**

Run: `rtk dotnet test` and the Node suite.
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
rtk git add src/AssociationSaleTool.Web/Pages/SaleStatus tests/AssociationSaleTool.Web.Tests
rtk git commit -m "Manage labels per letter and deposit desks from the administration page"
```

---

### Task 9: Documentation and end-to-end check

**Files:**
- Modify: `docs/domain-model-and-references.md` (sections "Item Reference Allocation" and "Why This Reference Scheme Was Chosen"; the em-dash paragraph in "Reference Scheme")
- Modify: `docs/features.md` (deposit flow steps 1–9 around line 266; feature list line 33; administration section)
- Modify: `docs/guide-utilisateur.md` (deposit section)
- Modify: `docs/scenarios-recette-utilisateur.md` (deposit scenarios, `Nouveautés depuis la recette du 17 septembre 2026` table)
- Modify: `docs/database-schema.md`, `docs/sql/sqlite.sql` if they list tables and columns
- `docs/decisions.md` already contains D-051 (written with the spec).

- [ ] **Step 1: Update the canonical docs**

Rewrite each section to match the spec: desks own letters, `Commencer le dépôt` allocates the run, saved rows get the next number, counters never decrease, labels per letter is an admin setting, no allocation at `Continuer`, force-close from admin. Keep French for all operator-facing text, English for canonical technical docs (`AGENTS.md`). In `features.md`, the deposit flow becomes:

```markdown
When the seller physically gives the toys to the association:

1. The operator's computer has an open deposit desk (`/vendeurs/poste`), which holds two letters.
2. The operator opens the seller detail page and clicks `Commencer le dépôt`; every existing row receives the desk's next numbers in one consecutive run, shown on screen.
3. The operator peels that many labels and sticks each on the matching toy, using the list sorted by reference.
4. A toy that was not registered is added as a row; saving it gives it the next number, and the operator peels the next label.
5. A refused or forgotten toy is deleted; its label is thrown away and its number is never reused.
6. The operator completes any missing seller details, then `Finaliser le dépôt`, the fee step, `Imprimer le contrat`, signature and `Valider le dépôt` as before. `Continuer` no longer allocates references.
```

- [ ] **Step 2: Update the acceptance scenarios**

In `scenarios-recette-utilisateur.md`, add a dated row to the `Nouveautés depuis la recette du 17 septembre 2026` table describing desks, letters and `Commencer le dépôt`, and update the deposit scenarios' steps and acceptance criteria accordingly. Add a scenario for opening and closing a desk and one for an admin force-close.

- [ ] **Step 3: Full verification**

```bash
rtk dotnet build
rtk dotnet test
for f in tests/*.test.mjs; do node --test "$f"; done
```

Expected: build with no new warnings (`dotnet build` without rtk to count `warning CS`), all .NET tests pass, every Node file reports `# fail 0`.

Then run the app (delete the local `*.db*` files first, never the folder) and walk acceptance examples 1–6 of the spec in the browser with two browser profiles as two desks.

- [ ] **Step 4: Commit**

```bash
rtk git add docs
rtk git commit -m "Document deposit desks owning reference letters"
```
