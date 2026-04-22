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

1. Sale-day volunteer types part or all of a reference.
2. Matching items appear.
3. Volunteer selects the correct item.
4. Item is added to the cart.
5. Volunteer saves the sale when the customer is done.
6. Receipt is printed only when requested.

### After The Sale

1. Reconciliation tool generates a seller sheet for each seller.
2. An operator physically gathers all returned items from the stock using the sheet.
3. If a returned item cannot be found, admin marks it as `missing`.
4. Seller sheet is regenerated.
5. Another operator writes the check based on the final amount to pay.
6. Check, final seller sheet, and returned items are physically grouped for seller pickup.

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

1. Admin updates the item status from `in_stock` to `missing`.
2. The seller sheet is regenerated.
3. The item moves from the returned list to the payable list and is marked `MISSING`.

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

## Auditability Requirements

- every sale must be persisted immediately
- item availability must prevent double sale after confirmation
- seller sheets must always be reproducible from current database facts
- missing-item changes must be attributable to a user or admin action when possible
