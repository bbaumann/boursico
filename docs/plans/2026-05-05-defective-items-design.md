# Defective Items Design

## Goal

Track items that cannot be sold because they are defective, return them to the seller, and handle buyer refunds when a defect is discovered after checkout.

The feature must keep the existing operational model simple:

- defects found during physical handover are rejected before acceptance
- defects found after acceptance are tracked in the application
- defective items cannot be sold
- defective items are returned to the seller
- defective items are not paid to the seller
- buyer refunds include the item price plus the 10 percent association fee

## Accepted Shape

Add a new item status:

- internal value: `defective`
- French label: `défectueux`

`defective` means the association has, or expects to have, the physical item, but the item is not suitable for sale.

Seller-sheet behavior:

- payable items: `sold` and `missing`, excluding refunded sale lines
- returned items: `in_stock` and `defective`
- `defective` returned items are visibly marked `DÉFECTUEUX`

Checkout behavior:

- only `in_stock` items can be sold
- `defective` items are excluded from checkout catalog and save-time validation

## Defect Workflows

### Defect Found During Seller Handover

If an operator spots the defect before accepting the item into association stock:

1. The item is not accepted.
2. If it was preregistered, the operator may delete the `registered` item.
3. If it was being entered in `Dépôt` mode, the operator does not create the item row.

No `defective` item record is required for this case because the item never became part of association stock.

### Defect Found While Item Is In Stock

If a defect is spotted after the item was accepted and is still `in_stock`:

1. An admin finds the item from an admin defect workflow.
2. The admin marks it `défectueux`.
3. The item status changes `in_stock -> defective`.
4. The item disappears from checkout.
5. The item appears in the seller sheet returned-items section and is not included in the seller payout.

An admin may restore `defective -> in_stock` only as a correction before the item has been part of a buyer refund workflow.

### Buyer Returns A Defective Sold Item

If a buyer brings back a defective item after checkout:

1. An admin finds the active sold sale line.
2. The admin confirms a defective-item refund.
3. The application records the refund on the sale line.
4. The item status changes `sold -> defective`.
5. The original sale remains stored with its original totals.
6. The sale line is excluded from seller payout.
7. The seller sheet lists the item under returned items as `DÉFECTUEUX`.
8. The buyer is reimbursed the item price plus the 10 percent association fee.

This is not the same business operation as a mistaken sale-line correction. A mistaken sale-line correction means the wrong item was sold and the item should return to normal stock. A defective-item refund means the sale happened, the buyer was refunded, and the item must not be sold again.

The UI can still live next to the existing sale correction action because both actions are rare, admin-only sale-line interventions.

## Refund Calculation

The refund amount is:

`seller_price_at_sale_cents + refund_checkout_fee_cents`

`refund_checkout_fee_cents` is calculated as 10 percent of `seller_price_at_sale_cents`, using the same rounding rule as checkout totals: midpoint values round away from zero.

For current whole-euro item prices, this is effectively the item price plus 10 percent. Keeping the calculation explicit protects the model if cent-level prices are allowed later.

Refund examples:

- item price EUR 10.00 -> fee refund EUR 1.00 -> total refund EUR 11.00
- item price EUR 15.00 -> fee refund EUR 1.50 -> total refund EUR 16.50

The original sale subtotal, checkout fee, and total paid are not recalculated. They remain the historical checkout fact. The refund is recorded separately.

## Data Model

### Item Status

Extend the allowed `item.status` values:

- `registered`
- `in_stock`
- `sold`
- `missing`
- `defective`

`defective` items remain seller items in the system. They continue to count toward the seller registration fee, like other seller items.

### Sale-Line Refund Fields

Extend `sale_line` with refund data:

- `refunded_at`
- `refund_item_price_cents`
- `refund_checkout_fee_cents`
- `refund_total_cents`
- `refund_reason`

For this feature, `refund_reason` is `defective`.

Rules:

- refund fields are null for non-refunded sale lines
- a sale line can be either corrected or refunded, not both
- a refunded sale line is not payable to the seller
- a refunded sale line remains visible in sale history as a historical fact
- `refund_total_cents = refund_item_price_cents + refund_checkout_fee_cents`

The existing active sale-line concept becomes active payable sale line:

- `corrected_at IS NULL`
- `refunded_at IS NULL`

An item with status `sold` must have an active payable sale line. An item with status `defective` may have a refunded historical sale line, but that line is no longer payable.

This follows the existing lightweight sale-line correction pattern. If future accounting needs require richer refund tracking, the fields can later be promoted to a separate refund table.

## Lifecycle Rules

Allowed normal transitions:

- `registered -> in_stock`
- `in_stock -> sold`
- `in_stock -> missing`
- `in_stock -> defective`
- `defective -> in_stock`, only as an admin correction when no buyer refund is involved
- `sold -> defective`, only through the defective-item refund workflow
- `missing -> in_stock`, only as an admin reconciliation correction

Forbidden plain status edits:

- `registered -> defective`
- `registered -> sold`
- `registered -> missing`
- ad hoc `sold -> defective` without a refund record
- ad hoc `sold -> in_stock`
- `defective -> sold`
- `defective -> missing`

Deletion remains allowed only for `registered` and `in_stock` items that are not linked to sale history. A `defective` item should not be hard-deleted in normal operations because it is a physical item that must be returned to the seller.

## Admin UI

Use the existing admin surface rather than adding a volunteer-facing control.

The existing `/vente/statut` correction area is the right home for item corrections. It should be labeled `Correction des articles` and display the first 15 non-`registered` items, with a status column.

For active sold items, the table should show two distinct admin actions:

- `Remettre en stock` for the current wrong-item correction
- `Rembourser / marquer défectueux` for a buyer-returned defective item

`Rembourser / marquer défectueux` opens a confirmation pop-in that shows the reimbursement amount before saving the refund. Reimbursement only applies to sold items.

The same table should support defect workflows for non-sold items:

- search by item reference, item title, seller code, or seller name
- show status, seller, and price
- for `in_stock` items: `Marquer défectueux`
- for eligible `defective` items with no refund history: `Remettre en stock`

The seller detail article list must not expose `Marquer défectueux`; marking an item defective is an admin task.

The refund confirmation must show:

- item reference
- item title
- original sale id
- item price at sale time
- refunded association fee
- total buyer refund

All labels and validation messages are French.

## Reconciliation And Seller Sheets

Seller sheets include sellers with at least one:

- `sold`
- `missing`
- `in_stock`
- `defective`

Payable list:

- active non-refunded sold items
- `missing` items

Returned list:

- `in_stock` items
- `defective` items, visibly marked `DÉFECTUEUX`

Refunded defective sold items are not payable to the seller. They are returned to the seller.

## Sales History

Read-only sale history keeps the original sale total visible and shows refunded lines explicitly.

A refunded line should display:

- the original item price at sale time
- the refund marker `Remboursé`
- the refund date below the marker

Refund amounts should not be repeated inside the article table. The totals block should display the original `Total`, followed by `Remboursements`, then a final `Total corrigé`.

Corrected lines and refunded lines are distinct labels.

## Boundaries

This design does not include:

- partial refunds
- exchanges or replacement items
- automatic payment-provider refund integration
- refund payment-method tracking
- changing historical sale totals after a refund
- paying the seller for defective items
- recording defects for items rejected before acceptance

## Error Handling

The admin workflow should show French errors for:

- unknown item or sale line
- item no longer eligible because another station changed its status
- attempt to mark a non-`in_stock` item defective through the in-stock defect action
- attempt to refund a sale line that is already corrected or refunded
- attempt to restore a defective item that came from a buyer refund

Status changes and refunds must reload the current item and sale-line state from the database immediately before saving.

## Testing

Tests should cover:

- `defective` is an allowed item status and displays as `défectueux`
- checkout catalog excludes `defective`
- save-time checkout validation rejects `defective`
- admin can mark `in_stock -> defective`
- admin can restore non-refunded `defective -> in_stock`
- buyer refund workflow changes `sold -> defective`
- buyer refund workflow records item-price refund, association-fee refund, and total refund
- refunded sale lines are excluded from seller-sheet payable items
- defective items appear in seller-sheet returned items with a visible marker
- sale history labels refunded defective lines distinctly from corrected lines
- invalid status transitions are rejected
