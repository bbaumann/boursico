# Seller Fees Design

## Goal

Track the seller-facing registration fee inside the application for VNext, show payment status during seller intake, and deduct unpaid fees from reconciliation payouts.

## Accepted Shape

- The expected seller fee is derived from the seller's total item count in the system.
- The rule is EUR 2 for every started block of 15 items:
  - 1 to 15 items: EUR 2
  - 16 to 30 items: EUR 4
  - 31 to 45 items: EUR 6
- Zero items means zero expected fee.
- The operator records only the amount already paid by the seller.
- The missing fee is computed as `max(expected_fee - paid_fee, 0)`.
- Overpayment is allowed for display purposes but never increases the reconciliation payout.

## Seller Intake Behavior

The seller detail page shows a compact fee status near the seller contact details:

- `Frais : 4,00 EUR (payes 4,00 EUR)` with a paid/green status when the fee is fully paid.
- `Frais : 4,00 EUR (payes 2,00 EUR)` with a warning status when the fee is underpaid.

The seller edit page adds a `Frais payes` whole-euro numeric input. The stored value is integer cents on the seller row. The expected fee is not stored because it depends on the current item count.

## Reconciliation Behavior

Printable seller sheets include:

- item subtotal
- expected seller fee
- already-paid seller fee
- remaining fee due
- final amount to pay the seller

The final amount to pay is:

`payable item subtotal - remaining seller fee`

The final amount is floored at zero if the remaining fee is greater than the payable subtotal.

## Data Model

Add `seller.fee_paid_cents`, required, defaulting to `0`.

The expected fee remains a derived value. It should be calculated from all items for the seller, not only sold, missing, or returned items. This matches the rule that the fee is owed for registered items and avoids reconciliation corrections changing the fee obligation.

## Docs And Decisions

This VNext feature supersedes `D-012 Seller Fee Logic Stays Outside The System In V1`.

Add a new decision recording that seller fee tracking is now in-app and update reconciliation/database documentation that currently says the EUR 2 per 15 items fee stays outside the system.

## Testing

Tests should cover:

- expected-fee calculation for zero items and started 15-item blocks
- seller create/edit persistence of paid fees
- seller detail fee status display
- reconciliation sheet fee fields and payout deduction
- schema mapping for `seller.fee_paid_cents`
