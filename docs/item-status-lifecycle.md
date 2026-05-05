# Item Status Lifecycle

## Purpose

This document summarizes the V1 item status model and the allowed status changes across event phases.

It also defines the operational mutability rule introduced by sale phases:

- in `presale`, intake staff may still correct seller master data and unsold item master data
- in `sale`, checkout remains open but seller and item master data become read-only in normal operations
- in `post_sale`, checkout is closed and seller and item master data remain read-only
- after seller and item master data are locked, only the allowed status transitions below remain editable

## Item Master Data Frozen At Sale Start

When the event phase is `sale` or `post_sale`, the following seller fields become read-only in normal operations:

- `first_name`
- `last_name`
- `email`
- `phone`
- `postal_address`
- `notes`

The following item fields also become read-only in normal operations:

- `reference`
- `item_number`
- `seller_id`
- `title`
- `seller_price_cents`

V1 stores one item price: `seller_price_cents`. Sold items keep their read-only sale-time snapshot on `sale_line.seller_price_at_sale_cents`.

The status field remains editable only through the workflow transitions defined in this document.

## Status Summary

### `registered`

- preregistered from an emailed seller list
- not yet physically delivered
- excluded from checkout and seller-sheet outputs

### `in_stock`

- physically delivered and present in association stock
- eligible for sale during the event
- treated as a returned item after the sale unless later marked `missing`

### `sold`

- sold during checkout
- payable to the seller

### `missing`

- expected to be returned after the event but not found physically
- payable to the seller and marked `MANQUANT` on the seller sheet

## Allowed Transitions

### Before Sale Start

- create item directly as `in_stock` during in-person intake
- create item as `registered` from emailed preregistration
- `registered -> in_stock` during physical delivery confirmation
- delete a `registered` item when a preregistered toy was not physically delivered
- delete an `in_stock` item when a physically present item was entered by mistake

### During The Sale

- `in_stock -> sold` when a sale is saved

No reservation status exists in V1:

- adding an item to a cart does not change its status
- the server re-validates `in_stock` status when the sale is saved
- if validation fails, the sale is rejected and no partial status transition is applied

### After The Sale

- `in_stock -> missing` during post-sale reconciliation by an admin
- restore a mistakenly marked `missing` item to `in_stock` only as an explicit admin reconciliation correction

## Final States In V1

The following are final in normal V1 operations:

- `sold`, except for an explicit admin sale-line correction when the item was added to a sale by mistake
- `missing`, except for an explicit admin reconciliation correction when the item was marked `missing` by mistake

No plain item-status transition back out of `sold` is defined in V1.

## Forbidden Transitions In V1

- `registered -> sold`
- `registered -> missing`
- ad hoc `sold -> in_stock`
- `sold -> missing`

V1 does not use a `not_provided` status. If a preregistered item was not physically delivered, the item is removed while it is still `registered`. If an item was mistakenly recorded as present, it may be removed while it is still `in_stock`.

If an operator mistake is discovered after an item has become `sold` or `missing`, correction must be handled by an explicit admin procedure rather than by ad hoc state changes in the standard UI. Restoring a mistakenly marked `missing` item to `in_stock` is allowed only through the reconciliation correction workflow. Restoring a mistakenly sold item to `in_stock` is allowed only by correcting the active sale line, which leaves the original sale stored and marks the sale line corrected.

## Role Summary

### Intake Operator

Before sale start:

- create `registered` items from emailed lists
- create `in_stock` items when toys are physically present
- confirm preregistered delivery by marking delivered items `in_stock`
- delete preregistered items that were not physically delivered
- delete `registered` or `in_stock` items that were entered by mistake
- correct seller and item master data if needed

### Sale-Day Volunteer

During the sale:

- move `in_stock` items to `sold` only by saving a sale
- cannot edit seller or item master data

### Reconciliation Admin

After the sale:

- move `in_stock` items to `missing` when a physically expected return cannot be found
- restore a mistakenly marked `missing` item to `in_stock` only as a correction
- must enter the shared admin password before confirming that action
- cannot reopen or rewrite seller or item master data as part of normal reconciliation

V1 does not include:

- per-user identity
- per-user authorization
- audit-trail requirements for `missing` changes

## Cross-References

- [Features](./features.md)
- [Process And Failover](./process-and-failover.md)
- [Database Schema](./database-schema.md)
