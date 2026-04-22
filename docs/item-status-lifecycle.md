# Item Status Lifecycle

## Purpose

This document summarizes the V1 item status model and the allowed status changes across event phases.

It also defines the operational mutability rule introduced for sale start:

- before sale start, intake staff may still correct item master data
- once checkout opens, item master data becomes read-only in normal operations
- after sale start, only the allowed status transitions below remain editable

## Item Master Data Frozen At Sale Start

When checkout opens for the event, the following item fields become read-only in normal operations:

- `reference`
- `item_number`
- `seller_id`
- `title`
- `seller_price`
- `sale_price`

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

### `not_provided`

- preregistered but never physically delivered
- excluded from checkout and seller-sheet outputs

### `sold`

- sold during checkout
- payable to the seller

### `missing`

- expected to be returned after the event but not found physically
- payable to the seller and marked `MISSING` on the seller sheet

## Allowed Transitions

### Before Sale Start

- create item directly as `in_stock` during in-person intake
- create item as `registered` from emailed preregistration
- `registered -> in_stock` during physical delivery confirmation
- `registered -> not_provided` during physical delivery confirmation

### During The Sale

- `in_stock -> sold` when a sale is saved

No reservation status exists in V1:

- adding an item to a cart does not change its status
- the server re-validates `in_stock` status when the sale is saved
- if validation fails, the sale is rejected and no partial status transition is applied

### After The Sale

- `in_stock -> missing` during post-sale reconciliation by an admin

## Terminal States In V1

The following are terminal in normal V1 operations:

- `not_provided`
- `sold`
- `missing`

No transition back out of those states is defined in V1.

## Forbidden Transitions In V1

- `registered -> sold`
- `registered -> missing`
- `not_provided -> in_stock`
- `sold -> in_stock`
- `sold -> missing`
- `missing -> in_stock`

If an operator mistake is discovered, correction should be handled by an explicit admin procedure rather than by ad hoc state changes in the standard UI.

## Role Summary

### Intake Operator

Before sale start:

- create `registered` items from emailed lists
- create `in_stock` items when toys are physically present
- confirm preregistered delivery to `in_stock` or `not_provided`
- correct item master data if needed

### Sale-Day Volunteer

During the sale:

- move `in_stock` items to `sold` only by saving a sale
- cannot edit item master data

### Reconciliation Admin

After the sale:

- move `in_stock` items to `missing` when a physically expected return cannot be found
- must enter the shared admin password before confirming that action
- cannot reopen or rewrite item master data as part of normal reconciliation

V1 does not include:

- per-user identity
- per-user authorization
- audit-trail requirements for `missing` changes

## Cross-References

- [Features](./features.md)
- [Process And Failover](./process-and-failover.md)
- [Database Schema](./database-schema.md)
