# Process And Failover

## Normal Process

### Before The Event

1. Some sellers send their item lists outside the system, typically by email.
2. Intake operator creates sellers in the system.
3. For emailed preregistration, items are entered as `registered`.
4. When preregistered sellers physically deliver toys, operator reviews the preregistered list and marks items as `in_stock` or `not_provided`.
5. Other sellers may register in person with toys physically present; those items are created directly as `in_stock`.
6. References are generated for each item.
7. `in_stock` items are prepared for the event.

### During The Sale

Before the first checkout is processed:

- item master data is frozen for the event
- only allowed status changes remain possible after this point

1. Sale-day volunteer types part or all of a reference.
2. The visible item table is filtered on each keystroke using `reference + title` matching from the in-memory checkout catalog.
3. Volunteer selects the correct item from the filtered table.
4. Item is added to the cart.
5. When the customer is done, volunteer clicks `Finalize`.
6. The cart is validated against the live database before the finalization modal is shown.
7. If validation passes, the finalization modal shows the item count, total amount, and the actions `Go back`, `Save`, and `Save and print receipt`.
8. If the volunteer chooses `Save` or `Save and print receipt`, the server re-validates that all cart items are still `in_stock` inside the save transaction.
9. If validation passes, the sale is saved and those items become `sold`.
10. If any item is no longer `in_stock`, the save is rejected, the conflicting item is highlighted prominently in red, and the operator must remove it and retry.
11. After a successful save, checkout resets to a fresh state and reloads the in-memory catalog.

### After The Sale

1. Admin opens the reconciliation dashboard at `/rapprochement` after entering the shared admin password.
2. Admin prints all seller sheets from `/rapprochement/feuilles`; sheets are generated only for sellers with at least one `sold`, `missing`, or `in_stock` item.
3. An operator physically gathers all returned items from the stock using the sheet.
4. If a returned item cannot be found, admin marks it as `missing`.
5. Seller sheet is regenerated from the dashboard.
6. If a missing mark was a mistake and the item is found before finalization, admin restores the item to `in_stock` as a reconciliation correction and regenerates the sheet.
7. Another operator writes the check based on the final amount to pay.
8. Check, final seller sheet, and returned items are physically grouped for seller pickup.

## Preregistration Confirmation Workflow

1. Seller preregistration is recorded from email.
2. Preregistered items exist in `registered` status.
3. When toys are physically delivered, the operator reviews the list line by line.
4. Delivered items become `in_stock`.
5. Preregistered items that were not physically brought become `not_provided`.
6. A `Check all` helper may be used first and then corrected item by item.

## Seller Sheet Workflow

### Default Case

If no item is missing:

- the first generated seller sheet is already the final seller sheet

### Exceptional Case

If an item listed as returned cannot be found physically:

1. Admin enters the shared admin password and updates the item status from `in_stock` to `missing`.
2. The seller sheet is regenerated.
3. The item moves from the returned list to the payable list and is marked `MISSING`.

### Correction Case

If an item was marked `missing` by mistake and is found before the seller packet is finalized:

1. Admin enters the shared admin password and restores the item to `in_stock`.
2. The seller sheet is regenerated.
3. The item moves back from the payable list to the returned list.

## Operational Failures

## Failure Philosophy For V1

V1 intentionally prefers:

- clear failure
- manual fallback
- later reconciliation

over:

- partial hidden offline behavior
- local replay complexity
- distributed conflict handling

## Official Digital Failure Mode

If the central server is unavailable:

- digital sales stop
- operators switch to paper bills
- reconciliation is performed manually later

There is no local per-station replay in V1.

## Paper Fallback Procedure

### Prepared Materials

- pre-numbered paper bill forms
- pens and clipboards
- written fallback instructions

### Procedure

1. Note the outage time.
2. Stop using the digital system.
3. Switch all sale stations to numbered paper bills.
4. Keep completed paper bills together in order.
5. Continue the event manually.
6. After the event, reconcile paper sales against remaining physical stock.

## Hard-Crash Recovery Principle

If the server is physically lost and digital recovery is not practical in time for the event:

- use paper fallback for the rest of the event
- use the physical stock at the end of the event as the authoritative basis for manual reconciliation

Operationally, anything not present in physical stock and not returned can be treated as sold or payable according to the fallback reconciliation rules used by the organizers.

## Reliability Measures Worth Keeping Even In V1

- automatic periodic database backups
- backup before sale start
- backup after sale close
- exportable sales and item lists if practical
- written operator fallback instructions

## Sensitive Action Control

- every sale must be persisted immediately
- item selection in a cart must not create a hidden reservation state
- sale save must re-check current item availability before persisting
- item availability must prevent double sale after confirmation
- seller sheets must always be reproducible from current database facts
- marking an item as `missing` must require the shared admin password
- restoring a mistakenly marked `missing` item to `in_stock` must require the shared admin password
- the reconciliation dashboard at `/rapprochement` must be admin-gated
- all seller sheets must be printed from `/rapprochement/feuilles`
- `Admin:SharedPassword` must be configured for production; blank values and `change-me` are invalid
- V1 does not implement per-user identity or a full authorization model
- the password gate exists to reduce misuse by non-admin operators, not to provide strong security
