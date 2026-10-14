# Item Status Lifecycle

## Purpose

This document summarizes the current V1 item status model and the allowed status changes across event phases. V1 is feature-complete but not live yet; the `defective` status and refund workflow are part of the current V1 behavior.

It also defines the operational mutability rule introduced by sale phases:

- in `presale`, intake staff may still correct seller master data and unsold item master data
- in `sale`, checkout remains open but seller and item master data become read-only in normal operations
- in `post_sale`, checkout is closed and seller and item master data remain read-only
- after a seller's deposit is validated, that seller and the deposited items become read-only in normal intake even during `presale`
- after seller and item master data are locked, only the allowed status transitions below remain editable

## Item Master Data Frozen At Sale Start

When the event phase is `sale` or `post_sale`, the following seller fields become read-only in normal operations:

- `first_name`
- `last_name`
- `email`
- `phone`
- `postal_address`
- `postal_code`
- `city`
- `notes`

The following item fields also become read-only in normal operations:

- `reference`
- `seller_id`
- `title`
- `seller_price_cents`

`reference` is nullable and stays `NULL` until the item's seller starts their
deposit at a deposit desk (`Commencer le dépôt`), or until the row is saved
when it is added to an already started deposit. It is then allocated a
`<LETTER><NNN>` value from the letters held by that desk, which does not change
afterward.

The application stores one item price: `seller_price_cents`. Sold items keep their read-only sale-time snapshot on `sale_line.seller_price_at_sale_cents`.

If a buyer is refunded because an item is defective, the original sale totals stay unchanged. Refund facts are recorded on the sale line with `refunded_at`, refund amount fields, and `refund_reason = defective`.

The status field remains editable only through the workflow transitions defined in this document.

Seller deposits use a separate seller-level operational status:

- `deposit_pending`: the seller has not yet signed a deposit contract; contact fields may still be incomplete, and their items remain draft `registered` rows
- `deposit_validated`: the seller has signed the paper deposit contract; accepted items have been moved to `in_stock`

The signed paper contract is the legal proof of deposit and price agreement. The database status only gates normal app workflow.

## Status Summary

### `registered`

- entered from an emailed list, external form import, or in-person intake before deposit validation
- not yet accepted into stock through a signed seller deposit
- excluded from checkout and seller-sheet outputs
- may remain in this state if the seller has not brought the item yet

### `in_stock`

- accepted through a validated seller deposit and present in association stock
- eligible for sale during the event
- treated as a returned item after the sale unless later marked `missing`

### `sold`

- sold during checkout
- payable to the seller

### `missing`

- expected to be returned after the event but not found physically
- payable to the seller and marked `MANQUANT` on the seller sheet

### `defective`

- physically accepted by the association, but no longer suitable for sale
- not eligible for checkout
- returned to the seller and marked `DÉFECTUEUX` on the seller sheet
- not payable to the seller

## Allowed Transitions

### Before Sale Start

- create item as `registered`
- `registered -> in_stock` only by validating the seller's signed deposit
- optionally keep a `registered` item pending when the seller may still bring it later
- delete a `registered` item when it was entered by mistake or the seller record should be cleaned up
- cancel a validated deposit as an admin-password action during `presale`, which moves only current `in_stock` items back to `registered`
- revalidate a reopened deposit, which moves current `registered` items to `in_stock` and tolerates historical `sold`, `missing`, and `defective` items

### During The Sale

- `in_stock -> sold` when a sale is saved
- `sold -> defective` only through the admin defective-item refund workflow when a buyer returns a defective sold item
- `defective -> sold` only when an admin cancels a defective refund that was entered by mistake

No reservation status exists in V1:

- adding an item to a cart does not change its status
- the server re-validates `in_stock` status and seller deposit validation when the sale is saved
- if validation fails, the sale is rejected and no partial status transition is applied

### After The Sale

- `in_stock -> missing` during post-sale reconciliation by an admin
- restore a mistakenly marked `missing` item to `in_stock` only as an explicit admin reconciliation correction
- `in_stock -> defective` when an admin identifies a defective item after it was accepted into stock
- restore `defective -> in_stock` only as an explicit admin correction when the item has not been part of a buyer refund

## Final States In Normal Operations

The following are final in normal V1 operations:

- `sold`, except for an explicit admin sale-line correction when the item was added to a sale by mistake
- `missing`, except for an explicit admin reconciliation correction when the item was marked `missing` by mistake
- `defective`, except for an explicit admin correction before any buyer refund exists, or cancellation of a defective refund that was entered by mistake

No plain item-status transition back out of `sold` is defined. A mistaken sale uses sale-line correction. A defective buyer return uses the refund workflow and changes the item to `defective`. If the refund itself was entered by mistake, the admin refund-cancellation workflow clears the refund fields and restores the item to `sold`.

## Forbidden Transitions

- `registered -> sold`
- `registered -> missing`
- manual item-level `registered -> in_stock`
- deposit cancellation outside `presale` or without the shared admin password
- deposit cancellation as a way to rewrite `sold`, `missing`, or `defective` items
- ad hoc `sold -> in_stock`
- `sold -> missing`
- ad hoc `sold -> defective` without a refund record
- ad hoc `defective -> sold` outside refund cancellation
- `defective -> missing`

V1 does not use a `not_provided` status. If a preregistered item is not physically delivered, it can stay `registered` without affecting checkout or seller-sheet outputs. If the entry was created by mistake or the team wants a cleaner seller record, it may be deleted while still `registered`. Once a seller deposit is validated, normal item deletion stops for that seller; admin cancellation reopens only items that are still `in_stock`. Items already `sold`, `missing`, or `defective` remain unchanged and read-only.

If an operator mistake or defect is discovered after an item has become `sold`, `missing`, or `defective`, correction must be handled by an explicit admin procedure rather than by ad hoc state changes in the standard UI. Restoring a mistakenly marked `missing` item to `in_stock` is allowed only through the reconciliation correction workflow. Restoring a mistakenly sold item to `in_stock` is allowed only by correcting the active sale line, which leaves the original sale stored and marks the sale line corrected. Refunding a defective sold item leaves the original sale stored, records the refund on the sale line, and marks the item `defective`. Canceling a refund is allowed only when the refund was entered by mistake; it clears the sale-line refund fields and restores the item to `sold`.

## Role Summary

### Intake Operator

Before sale start:

- create `registered` items from emailed lists
- create `registered` items when toys are physically present
- print the seller deposit contract and validate it after signature
- leave pending preregistered items as `registered` when the seller may still bring them later
- delete preregistered items only when they were entered by mistake or should be removed as cleanup
- delete `registered` items that were entered by mistake before deposit validation
- correct seller and item master data if needed

### Sale-Day Volunteer

During the sale:

- move `in_stock` items to `sold` only by saving a sale
- cannot edit seller or item master data

### Reconciliation Admin

After the sale:

- move `in_stock` items to `missing` when a physically expected return cannot be found
- restore a mistakenly marked `missing` item to `in_stock` only as a correction
- mark `in_stock` items as `defective` when they cannot be sold and must be returned
- refund a defective sold item from `/administration`, including the buyer-paid 10 percent association fee
- cancel a mistaken defective refund from `/administration`, restoring the item to `sold`
- restore `defective` items to `in_stock` only when they have no refund history
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
