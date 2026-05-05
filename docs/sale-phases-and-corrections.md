# Sale Phases And Sale Corrections

## Purpose

The system uses an event-level sale phase to prevent accidental operational changes without blocking the event when organizers are still preparing. V1 is complete; the current software state is VNext.

The phase is a system rule, not just a visual label. Server-side services must enforce the phase because browser pages can stay open while an admin changes the phase.

## Sale Phases

The persisted phases are:

- `presale`
- `sale`
- `post_sale`

Operator-facing labels are:

- `Prévente`
- `Vente`
- `Après-vente`

The status is shown near the event/software name on every page.

### `presale`

`presale` is the default phase for a new event database.

Rules:

- `/` opens the seller workflow at `/vendeurs`
- checkout remains available
- new sales may be saved
- seller master data remains editable
- unsold item master data remains editable
- sold items remain locked for normal item editing

This phase intentionally permits sales so opening-day operations are not blocked if an admin has not moved the system to `sale` yet.

### `sale`

`sale` is an admin-gated catalog lock.

Rules:

- `/` opens the checkout workflow at `/caisse`
- checkout remains available
- new sales may be saved
- seller master data is read-only in normal operations
- item master data is read-only in normal operations
- only workflow status changes remain allowed
- an admin may move the system back to `presale` if the real event process needs it

### `post_sale`

`post_sale` closes checkout and starts reconciliation-oriented operation.

Rules:

- `/` opens the reconciliation workflow at `/rapprochement`
- new sale validation and sale save are rejected server-side
- seller master data remains read-only
- item master data remains read-only
- reconciliation corrections and seller-sheet printing are available
- an admin may move the system back to another phase if the real event process needs it

When the event is still in `presale` or `sale`, an admin may open the reconciliation dashboard, but it is read-only. Seller-sheet printing and missing-item reconciliation corrections are only available in `post_sale`.

## Phase Administration

Phase transitions are exposed through the French route `/vente/statut`, which is also a fourth item in the main navigation.

The page is guarded by the shared admin password from `Admin:SharedPassword`. Once the page is unlocked in the current browser session, phase changes, sale-line corrections, defective-item marking, and defective-item refunds do not ask for the password again. VNext does not add per-user identity or an audit log for phase changes.

## Sale Correction

Sale correction covers the case where a volunteer typed the wrong reference and saved a sale with the wrong item.

The correction must not be a plain `sold -> in_stock` item status edit. Once a sale is saved, the database also contains a sale and a sale line. Changing only the item status would leave contradictory facts.

V1 therefore uses an explicit admin sale-line correction:

1. Admin finds the active sold sale line.
2. Admin confirms correction from the already-unlocked admin page.
3. The sale line receives a `corrected_at` timestamp.
4. The item returns to `in_stock`.
5. The original sale remains stored with its original totals.
6. The corrected line is excluded from seller-sheet payable output.
7. The item may be sold again normally.

The original sale totals are not recalculated because the persisted sale represents what happened at checkout, including any cash or receipt consequence. Sale correction fixes item availability and seller payout facts without silently rewriting the historical checkout.

Only one active payable sale line may exist for an item at a time. Active payable means `corrected_at IS NULL AND refunded_at IS NULL`. Corrected sale lines remain as historical correction evidence.

## Defective Buyer Refund

Defective buyer refund covers the case where a buyer returns a sold item because it is defective.

This is not a mistaken sale correction. The sale did happen, but the buyer must be reimbursed and the item must not be sold again.

VNext uses an explicit admin refund workflow:

1. Admin finds the active sold item in `Correction des articles` on `/vente/statut`.
2. Admin clicks `Rembourser / marquer défectueux`.
3. The confirmation pop-in shows item price, refunded 10 percent association fee, and total refund.
4. The sale line receives `refunded_at`, refund amount fields, and `refund_reason = defective`.
5. The item becomes `defective`.
6. The original sale remains stored with its original totals.
7. The refunded line is excluded from seller-sheet payable output.
8. The item appears on the seller sheet as `DÉFECTUEUX` in the returned-items list.
