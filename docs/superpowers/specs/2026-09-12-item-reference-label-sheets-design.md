# Item Reference Allocation And Pre-Printed Label Sheets

## Purpose

Item references must be printable on adhesive labels **before** the event, at a
time when sellers are not yet known. Today `ItemReference.Format(sellerCode,
itemNumber)` stamps a reference such as `BK014` when the item row is created,
which makes the reference space unknowable in advance and ties it to a seller
who may not exist yet.

This design replaces the seller-derived reference with a **letter-bucket
reference allocated at deposit time**, so that a fixed, fully predictable set of
labels can be printed weeks ahead and physically selected at the deposit desk.

## Physical Process This Serves

Understanding the recollection workflow is what drives the design:

1. After the sale, volunteers sort unsold items into piles **by label letter,
   then by number**.
2. Each seller recap sheet is handed to a volunteer, who takes a cardboard box
   and collects that seller's unsold items out of the sorted piles.

The letter is therefore a **sort bucket**, not a seller identifier. Twenty
letters means twenty piles that twenty volunteers can sort in parallel. Two
properties matter:

- items must be spread reasonably evenly across letters, so the piles are
  balanced
- items belonging to one seller should usually sit close together, so the
  collecting volunteer walks one pile rather than twenty

Identifying the seller from a stray label is explicitly **not** a requirement.
An operator can always look it up in the administration screens.

## Locked Decisions

| Decision | Value |
|---|---|
| Reference format | `<LETTER><NNN>` - plain sequence, zero-padded to 3 digits, e.g. `B157` |
| Year prefix | None |
| Letters | The existing 20-letter safe alphabet |
| Sheets printed per letter | 7 (operator-side fact; 7 x 65 labels = 455) |
| Soft capacity per letter | 455 |
| Allocation moment | When the operator enters the contract-print step |
| Physical position display | None. No sheet number, no label position, no reference range, anywhere |
| Reference before deposit | Never |

The reference carries **no** seller information, and the application never
displays a sheet number, a label position or a reference range. Screens and
printed documents show individual references only, exactly as they do today.

## Reference Format

```
<LETTER><NNN>
```

- `LETTER` is one character from the safe alphabet already defined in
  `SellerCodeGenerator`: `B C D E G H J K L M N P R S T U W X Y Z` (20 letters;
  `A` is reserved for the association, and `F I O Q V` are excluded as visually
  confusable).
- `NNN` is the number within that letter, zero-padded to three digits.

Zero padding is load-bearing: `ORDER BY reference` must sort in the same order
as the physical labels, and `B009` must sort before `B010`.

Numbers normally run `001`-`455`. They may exceed `455` only in the relaxed
overflow case below. `999` is the hard format limit.

## Letter Capacity And Balance

Soft capacity per letter is **455**, exposed as
`Labels:SoftCapacityPerLetter` in `appsettings.json`. It corresponds to the 7
label sheets of 65 the association prints per letter, but the application never
reasons about sheets - it only knows the number.

Balance across letters is free from a property the code already has.
`SellerCodeGenerator.GetCodeAt` computes `first = alphabet[index % 20]`, so the
first letter of a seller code **cycles every 20 sellers**: `BB, CB, DB ... ZB`,
then `BC, CC, DC ...`. With the ~196 sellers seen in 2024, each letter serves
almost exactly 10 sellers.

Sizing against 2024 real data (4,222 items, ~196 sellers, ~21 items/seller):

- expected load per letter: ~211 of 455
- ten sellers averaging 21 items sum to ~210 with a spread of roughly +/- 47
- 455 sits about 5 standard deviations above the mean, so a letter overflowing
  is not a realistic event
- total capacity 20 x 455 = 9,100 against ~4,200 items

## Allocation Algorithm

Let:

- `N` = number of the seller's items with `reference IS NULL`
- `used(X)` = `MAX(item_number)` over items whose `reference_letter = X`, or `0`
- `L` = first character of `seller.code`
- `CAP` = `Labels:SoftCapacityPerLetter` (455)

Allocation for one seller:

1. If `N = 0`, do nothing.
2. Choose the letter:
   1. **Top-up.** If the seller already has referenced items, let `M` be the
      `reference_letter` of their most recently allocated one - that is, the
      highest `item.id` among their referenced items, not the highest letter or
      number. If `used(M) + N <= CAP`, choose `M`. This keeps a seller's items
      in one sort pile when they pull more toys out of the bag mid-deposit.
   2. **Own letter.** Otherwise, if `used(L) + N <= CAP`, choose `L`.
   3. **Spill.** Otherwise choose the letter with the smallest `used(X)` among
      those where `used(X) + N <= CAP`, ties broken by alphabet order. Picking
      the least-loaded letter rather than the next one alphabetically is what
      keeps the sort piles even.
   4. **Relax.** Otherwise no letter can take the seller. Choose `L` and ignore
      `CAP`. Numbers then run past 455 into labels the association prints on the
      day. `L` applies here even for a top-up whose existing letter is `M`: at
      that point every letter is full, and the rule stays predictable rather
      than trying to preserve a pile that no longer has room.
3. Assign numbers `used(chosen) + 1 ... used(chosen) + N` to the reference-less
   items ordered by `item.id` ascending, so the run follows entry order and is
   deterministic.
4. Write `reference_letter`, `item_number`, and
   `reference = letter + number.ToString("000")`.

### Hard cap

If an allocation would push a number past `999`, throw
`ReferenceSpaceExhaustedException`. There is deliberately no operator-facing
recovery: total capacity is 19,980 references against an event of ~4,200 items,
so this cannot occur in practice. The guard exists only to prevent silent
corruption of the 3-digit format and of reference sort order. It surfaces as a
generic application error, not as a French operator message - deliberately, so
that no one is tempted to treat it as a recoverable situation.

### Concurrency

Allocation reads `used(X)` and writes the new references **inside the
transaction that handles the deposit step**. SQLite serializes writers, so two
deposit desks finalizing at the same moment cannot obtain overlapping runs.

## Allocation Timing

The deposit dialog has three steps: fees, contract, signature.

1. `Finaliser le depot` posts `BeginDepositFinalization`, opening the dialog on
   the fee step.
2. `Continuer` posts `UpdateDepositFee`, which saves the paid fee and moves the
   dialog to the contract step. **Allocation happens here.**
3. `Imprimer le contrat` is a GET on `/vendeurs/{id}/contrat-depot`. By then
   references exist, so the signed contract lists them.
4. `Valider le depot` posts `ValidateDeposit`, setting
   `deposit_status = deposit_validated` and moving items to `in_stock`.

Allocating at step 2 rather than step 4 is what lets the signed paper contract
record which labels went on which toys.

Allocation is **idempotent**: it only touches items where `reference IS NULL`.
The dialog's `Retour` link back to the fee step re-posts `UpdateDepositFee`, and
a second pass is a no-op. Items added between two passes are topped up by rule
2.1.

### Accepted imperfections

- If another desk allocates between a seller's first pass and a top-up, the
  extra items land at the letter's new tail rather than adjacent to the
  seller's run. Contiguity is a nice-to-have; this is tolerated.
- Abandoning a deposit after allocation leaves a gap in the letter. The cost is
  a few wasted printed labels, and gaps are affordable at ~211 of 455 used.

### Deposit cancellation

The existing admin cancellation moves `in_stock` items back to `registered`.
It **keeps allocated references**. The labels are physically stuck on the toys
by then, so releasing the numbers would be actively wrong. Revalidating the
deposit reuses the existing references and allocates only for items still
lacking one.

## The "No Reference" Invariant

An item without a reference must never be sellable. This needs **no new rule**.
`CheckoutService` already applies the same triple gate in both the catalog
query (`CheckoutService.cs:38`) and the authoritative save-time re-validation
(`CheckoutService.cs:284`):

```
Status == "in_stock"
&& Seller.DepositStatus == SellerDepositStatuses.Validated
&& SellerPriceCents.HasValue
```

Since references are allocated at deposit, an item with no reference is still
`registered` and its seller is still `deposit_pending`, so it fails two of the
three conditions independently. The invariant "has a reference if and only if
the deposit reached the contract step" rides on machinery that already exists.

## Schema Changes

`item`:

| Column | Change |
|---|---|
| `reference` | becomes **nullable**; keeps its unique index |
| `reference_letter` | **new**, nullable, 1 character, indexed |
| `item_number` | becomes **nullable**; semantics change from per-seller sequence to per-letter sequence |

Indexes:

- drop unique `(seller_id, item_number)` - the concept no longer exists
- add unique `(reference_letter, item_number)`

SQLite treats `NULL`s as distinct in unique indexes, so any number of
`registered` items may carry `(NULL, NULL)`.

A migration is required. The application is not live, so no data backfill is
needed.

## Code Changes

**Domain**

- `ItemReference.Format(char letter, int number)` replaces the seller-code
  signature.
- New `ReferenceAllocator` holding the pure letter-choice logic, so it is
  testable without a database: given `used(X)` for every letter, the seller's
  own letter, any existing letter, and `N`, return the chosen letter.

**Services**

- `IntakeService`: stop generating references at item creation. Add
  `AllocateDepositReferencesAsync(sellerId, ct)` performing the transactional
  allocation.
- `SellerFormImportService`: stop generating references on import.
- Move the missing-price precondition out of `Pages/Sellers/Details.cshtml.cs:544`
  into `IntakeService`, next to allocation, so both deposit preconditions are
  enforced server-side rather than in the page model. This is a targeted fix to
  a pre-existing soft spot in code this change already touches.

**Pages**

- `Details.cshtml.cs` `OnPostUpdateDepositFeeAsync` calls allocation.
- `Details.cshtml` renders an em dash for items with no reference yet.
- `DepositContract` and the reconciliation seller sheet are unchanged: they
  already render individual references, which exist by the time either is
  printed.

**Configuration**

- New `LabelsOptions` bound to `Labels:SoftCapacityPerLetter`, default `455`.

## What Does Not Change

- `seller.code` stays a 2-letter code and remains the seller identifier used in
  search, seller lists and data lists. It is simply no longer embedded in item
  references.
- `SellerCodeGenerator` and its assignment order are untouched - the design
  depends on the existing spread order for balance.
- Pricing, fees, payout, sale phases, the checkout flow, the deposit gate and
  all correction workflows.
- Checkout catalog ordering: `ORDER BY reference` still produces physical label
  order thanks to zero padding.

## Testing

- `ReferenceAllocatorTests`: own letter chosen when it fits; spill to the
  least-loaded letter when it does not; relaxation when no letter fits; top-up
  prefers the seller's existing letter; assignment follows `item.id` order;
  hard cap throws.
- `IntakeServiceTests`: items are created with no reference; allocation is
  idempotent across two passes; added items are topped up; cancellation keeps
  references; revalidation allocates only the missing ones.
- `ItemReferenceTests`: formatting and zero padding.
- Existing `CheckoutServiceTests` must keep passing unchanged - that is the
  evidence the sellability invariant still holds.

## Out Of Scope

- Any display of sheet number, label position or reference range.
- References before deposit, and emailing ranges to sellers so they label at
  home. This would force allocation earlier and reintroduce the buffer problem
  this design removes.
- Generating the label sheets themselves (see open question).
- Multi-event reference history.

## Open Question

**Should the application generate the printable label sheets?**

The application now owns the reference space but does not produce the labels.
If the sheets are built by hand in a spreadsheet and the two ever disagree - a
letter skipped, a number off by one - the mismatch would only surface at the
deposit desk on the day.

A small print-only page rendering all 20 letters x 455 references in a 65-per-A4
layout would make the two impossible to desynchronise. It is a modest amount of
work and reuses the invoice/contract print-CSS approach already in the codebase.
Left out of this design pending a decision.
