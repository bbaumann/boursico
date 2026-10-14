# Deposit Desks Own Reference Letters

## Purpose

Item references are allocated today when a seller's deposit reaches the
contract-print step (D-050). That does not match how the deposit desk works.
A seller hands over their toys one by one; for each toy the operator checks its
condition, registers it if it was not pre-registered, then **sticks its label
straight away**. The label must therefore be known while the toy is in the
operator's hands, long before the contract is printed.

Two constraints shape the replacement:

- **Label sheets must not travel between desks.** There are several deposit
  desks (from 2 up to about 10, varying over the two deposit days), and each
  letter's pre-printed sheets can physically sit on only one desk.
- **Pre-registered toys must not cost one click each.** Most toys arrive
  pre-registered (email list or external form import); the operator should get
  all their labels in one action.

This design gives each open desk exclusive ownership of a few letters. Because
only one desk draws numbers from a letter, the next number the application
gives is always the next label on the sheet in front of the operator, so screen
and sheet never disagree and no numbering buffer is needed.

## Physical Process This Serves

1. Before the event, the association prints label sheets per letter, numbered
   `001` upwards, as today. Sheets of letters not held by a desk sit on a
   central table.
2. When a desk opens, the application assigns it two letters; a volunteer
   fetches those sheets from the central table.
3. When a seller reaches the desk, the operator clicks **Commencer le dépôt**.
   Every row the seller already has receives the desk's next numbers, in one
   consecutive run. The operator peels that many labels from the sheet.
4. For each toy: the operator finds its row and sticks the matching label. A
   toy that was not registered is added as a new row; saving the row gives it
   the next number, and the operator peels the next label.
5. A refused or forgotten toy: the operator deletes its row and throws the
   label away. Its number is never reused.
6. The contract is printed and signed as today. Allocation is no longer part of
   that step.
7. When a desk closes, its sheets go back to the central table and its letters
   become free for the next desk to open.

Post-sale recollection is unchanged: unsold items are sorted by letter, then
number, into twenty piles, and each seller's recap sheet lists their references.

## Locked Decisions

| Decision | Value |
|---|---|
| Reference format | Unchanged: `<LETTER><NNN>`, e.g. `K041` |
| Letters | Unchanged: the 20-letter safe alphabet |
| Who owns a letter | At most one open deposit desk at a time, enforced by the server |
| Letters per desk | 2, from `Labels:LettersPerDesk` in `appsettings.json` |
| Labels per letter | One value for all letters, default 455, editable on `/administration`, stored in the database |
| Allocation moment | **Commencer le dépôt** for existing rows; saving a row after that for new rows |
| Letter choice | Decided by the desk, no longer by the seller code |
| Number reuse | Never. A per-letter counter only increases |
| Numbering buffer | None |
| Desk identity | A cookie holding the desk's random token |
| Desk configuration | None ahead of time. A desk is created when a computer opens it |
| Desk takeover after a crash | None. The admin force-closes the desk; the replacement computer opens a new one |
| Automatic desk timeout | None |

The seller code stays as the seller's identifier. It no longer influences the
reference letter.

## Deposit Desks

A deposit desk (`Poste de dépôt`) is one computer at one physical queue. There
is always exactly one computer per desk.

### Opening

Any computer on `/vendeurs/poste` can open a desk. The volunteer may type a
name; otherwise the desk is named `Poste N`, where `N` is one more than the
number of desks ever opened. Opening:

- creates the desk with a random token and stores the token in the computer's
  cookie
- assigns it `LettersPerDesk` letters automatically: the free, non-exhausted
  letters with the fewest numbers used, ties broken by alphabet order
- shows the assigned letters in large type with the instruction to fetch their
  sheets

If fewer free letters exist than `LettersPerDesk`, the desk opens with what is
available, and with no letter at all if none is free. Such a desk can still be
used to view and edit, but **Commencer le dépôt** and adding rows to a started
deposit fail until a letter becomes available (see Allocation Rules).

A computer that already holds an open desk cannot open a second one.

### Closing

- **By the desk itself:** `Fermer le poste` on `/vendeurs/poste`, after a
  confirmation that reminds the volunteer to bring the sheets back to the
  central table. The desk's letters become free and the cookie is deleted.
- **By an admin:** `Forcer la fermeture` on `/administration`, with the admin
  page unlocked. Used when a laptop crashed, was replaced, or was put away
  without closing its desk. The letters become free; the cookie on the old
  computer is left in place but now points at a closed desk.

The good practice, shown on the desk page, is to close the desk before putting
the computer away.

### Identity

The desk token travels in an `HttpOnly`, `SameSite=Strict` cookie named
`poste-depot`, valid for 4 days, so it covers the two deposit days and is
present on every page load and form submission without client-side script.

On every request that allocates numbers, the server resolves the cookie to an
**open** desk. A missing cookie, an unknown token, or a closed desk refuses the
allocation: nothing is written and the page shows
`Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.` (closed desk)
or `Ouvrez un poste de dépôt sur cet ordinateur pour attribuer des références.`
(no desk). There is no automatic refresh: a page left open on a closed desk
learns it at its next load or submission. Because a number only appears on
screen after its save has succeeded, a refused save never leads to a label
being stuck.

Each successful allocation updates the desk's `last_activity_at`, shown on
`/administration` to spot desks left open.

### Banner

The seller pages (`/vendeurs` and below) show a desk banner under the sale
status header:

- open desk: `Poste 3 · lettres K, R` and a link to `/vendeurs/poste`
- a held letter with 20 or fewer labels left:
  `Plus que 12 étiquettes en K.`
- closed desk: `Ce poste a été fermé. Ouvrez un nouveau poste pour continuer.`
- no desk: `Aucun poste de dépôt ouvert sur cet ordinateur.` and a link to
  open one

## Labels Per Letter

`labels_per_letter` is a single integer stored on `event_state`, default 455
(7 sheets of 65), editable from `/administration` with the admin page unlocked.
It replaces `Labels:SoftCapacityPerLetter`.

- It must be between 1 and 999 (the format limit).
- It cannot be set below the highest number already used in any letter,
  because those labels are already on toys:
  `Le nombre d'étiquettes par lettre ne peut pas être inférieur à 212, déjà utilisé pour la lettre K.`
- A letter is **exhausted** when its counter has reached `labels_per_letter`.
  Exhaustion is computed, never stored, so raising the value (after printing
  more sheets) makes exhausted letters usable again.

## Allocation Rules

Allocation always happens inside the transaction that saves the rows, at the
desk resolved from the cookie, and only during `presale` (the existing master
data rule).

### Per-letter counter

Each letter has a `last_number` that starts at 0 and only increases. The next
reference in a letter is `last_number + 1`. Deleting an item never decreases
it, so a number whose label was already peeled is never handed out again.

### Commencer le dépôt

Available on a seller's page while their deposit is `deposit_pending` and not
yet started. It allocates a run for every row of the seller that has no
reference, in row creation order, then marks the deposit as started
(`seller.deposit_started_at`). A seller with no rows yet (live registration)
can start too: nothing is allocated and the deposit is marked started.

Starting is idempotent from the operator's point of view: once started, the
button is replaced by the list of references, and a second submission is
refused without allocating.

### Rows added after the start

Once a deposit is started, saving new rows allocates their references in the
same save, in row order. Saving rows on a non-started deposit allocates
nothing, exactly as today.

### Choosing letters for a run of N numbers

The desk's **usable letters** are the letters it holds that are not exhausted.

1. **Seller's letter**: if the seller already has references, take the letter
   of their most recently allocated reference. If the desk holds it and it has
   room for all N, use it. This keeps a seller's extras next to their run.
2. **One letter for the whole run**: otherwise, the usable letter with the
   most room left, if it has room for all N. A pre-registered seller's run is
   therefore never split while the desk has a letter that can take it.
3. **Split**: otherwise, fill the seller's letter (if usable), then the other
   usable letters from most room to least, then free letters taken on the
   spot, least-used first, until N numbers are allocated.
4. **No room**: if all of that cannot provide N numbers, nothing is allocated
   and the save is refused:
   `Plus aucune étiquette disponible pour ce poste. Demandez à l'administration d'augmenter le nombre d'étiquettes par lettre ou de libérer une lettre.`

A live seller has no reference yet, so tier 1 does not apply to their first
toy; it goes to the usable letter with the most room, and the following toys
stay in that letter through tier 1.

A seller whose references end up in two letters (a letter ran out mid-seller,
or the seller came back to another desk) is acceptable: the contract and the
recap sheet list every reference.

### After every allocation

1. Every letter held by the desk that is now exhausted is released (it stays
   exhausted, so no other desk takes it).
2. If the desk now holds fewer than `LettersPerDesk` usable letters, it takes
   free letters (least-used first) to get back to `LettersPerDesk`.
3. The page lists the references just allocated and, for each letter taken in
   step 2 or in tier 3, the instruction `Allez chercher la planche R.`

### Taking a free letter safely

Taking a letter is a conditional update (`held_by_desk_id IS NULL`). If two
desks try to take the same free letter at the same instant, only one update
succeeds; the other allocation fails as a whole and the operator retries. As
with D-050, SQLite may surface such a collision as a busy-snapshot error page;
re-submitting succeeds and nothing was written.

## Deposit Finalization And Validation

- `Finaliser le dépôt` requires the deposit to be started. If it is not, the
  finalization is refused with
  `Commencez le dépôt pour attribuer les références avant de le finaliser.`
- The fee step's `Continuer` no longer allocates references; it saves the fee
  and moves to the print step as before.
- The existing guard in `ValidateSellerDepositAsync` (no `registered` item with
  a null reference) is unchanged and still protects validation.
- Canceling a validated deposit keeps references and the started marker.
  Rows added afterwards get references when saved, because the deposit is still
  started.

## Administration

A new `Postes de dépôt et étiquettes` section on `/administration`, visible
once the admin page is unlocked:

- **Étiquettes par lettre**: the current value and a form to change it, with
  the validation above.
- **Lettres**: one row per letter with numbers used, labels left (or
  `Épuisée`), and the desk holding it (or `Libre`).
- **Postes ouverts**: name, letters held, opened at, last activity, and a
  `Forcer la fermeture` button with a confirmation.

## Data Model

New table `deposit_desk`:

| Column | Type | Notes |
|---|---|---|
| `id` | integer PK | |
| `name` | text, not null | trimmed, non-blank |
| `token` | text, not null, unique | random, stored in the cookie |
| `opened_at` | datetime, not null | UTC |
| `closed_at` | datetime, null | null while open |
| `last_activity_at` | datetime, not null | UTC |

New table `reference_letter`, seeded with the 20 letters:

| Column | Type | Notes |
|---|---|---|
| `letter` | text(1) PK | one of the safe alphabet |
| `last_number` | integer, not null, default 0 | check `0..999` |
| `held_by_desk_id` | integer, null, FK `deposit_desk` | null when free |

Changed tables:

- `seller.deposit_started_at` (datetime, null)
- `event_state.labels_per_letter` (integer, not null, default 455, check `1..999`)

The application is not live, so, as for the 2026-09-29 reset, the model change
regenerates the single `InitialCreate` migration. Existing databases must be
deleted before the first start of this version.

## Removed

- `ReferenceAllocator` and its four-tier, seller-code-based letter choice
- `IntakeService.AllocateDepositReferencesAsync` and its call from the fee step
- `Labels:SoftCapacityPerLetter`

## Out Of Scope

- Moving a letter from one open desk to another without closing a desk
- Choosing letters by hand when opening a desk
- Printing labels from the application
- Any change to post-sale sorting, recap sheets or checkout

## Acceptance Examples

These are the examples the implementation's tests encode.

1. Desk P1 opens on an empty event and receives `B` and `C`. Desk P2 opens and
   receives `D` and `E`.
2. At P1, a pre-registered seller with 12 rows starts their deposit: the rows
   receive `B001`–`B012`. Two extra toys are then added one at a time and
   receive `B013` and `B014`.
3. At P1, row `B014` is deleted; the next toy added for any seller at P1 in
   `B` receives `B015`, never `B014`.
4. With `labels_per_letter` at 20 and `B` at 18 used, a seller starting with 5
   rows at P1 goes to `C` (tier 2). If `C` also has fewer than 5 left, the run
   is split: `B019`–`B020`, then the rest in `C`, and then in a newly taken
   letter if needed. `B` is released as exhausted and P1 takes the least-used
   free letter; the page says which sheet to fetch.
5. An admin force-closes P2: `D` and `E` become free. On P2's computer, saving a
   row for a started deposit is refused with the closed-desk message and no
   item is created.
6. Setting `labels_per_letter` to 10 while `B` has used 15 is refused; setting
   it to 30 makes an exhausted `B` free to take again.
