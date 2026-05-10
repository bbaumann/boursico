# Closing Dashboard Design

## Purpose

Add a read-only financial closing dashboard for events with multiple physical checkout stations. The feature helps organizers inspect what the system says was collected and refunded, both globally and per physical checkout.

This is not a payment reconciliation workflow. Physical validation of cash, cheques, and card terminal totals stays offline.

## Scope

The first implementation covers:

- a checkout code recorded on every new sale
- a refund checkout code recorded when an admin refunds a defective sold item
- a read-only `Clôture` tab under `/donnees`
- global totals and per-checkout totals

Out of scope:

- server-side uniqueness enforcement for checkout codes
- a checkout station registry table
- saved closing sessions
- manual cash/card/cheque validation
- accounting ledger tables
- treasurer exports

## Checkout Code Capture

The application stores the checkout code directly on `sale` as required text for new sales.

The checkout page owns the station setup for normal sales:

- if no checkout code exists in browser `localStorage`, the checkout page blocks sale entry with a French setup prompt
- the operator enters a code such as `CAISSE-1`
- the browser stores that code in `localStorage`
- every sale request sends that code
- `CheckoutService` trims it, rejects blank values, and persists it on the sale

Uniqueness is handled operationally by event organizers. The database does not create a checkout station table or enforce unique checkout codes.

## Refund Checkout Code

Defective-item refunds can physically happen at any checkout. The refund must therefore record the checkout that paid the refund, not the checkout that made the original sale.

The admin refund dialog shows a required dropdown named `Caisse de remboursement`.

The dropdown options come from distinct checkout codes already present on sales in the database:

- distinct non-empty `sale.checkout_code` values
- sorted alphabetically
- no default from browser `localStorage`

When the admin confirms the refund, the selected code is stored on `sale_line.refund_checkout_code`. Canceling a mistaken refund clears this value with the other refund fields.

## Closing Report

The report lives under `/donnees?onglet=cloture` as a fifth read-only tab named `Clôture`.

It shows:

- one global summary row
- one row per checkout code
- sales count
- item count
- subtotal
- checkout fee total
- total collected
- refunds paid
- net collected
- payment-method totals for card, cash, and cheque

Per-checkout sales are grouped by `sale.checkout_code`.

Per-checkout refunds are grouped by `sale_line.refund_checkout_code`.

Per-checkout net collected is:

```text
total collected by sale checkout - refunds paid by refund checkout
```

The global row is:

```text
all sale totals - all refund totals
```

## Documentation Updates

The canonical docs must describe:

- checkout code storage on sales
- refund checkout code storage on sale lines
- `Données > Clôture`
- the feature as current V1/VNext-absorbed work if implemented before first live use

## Testing

Tests should cover:

- sale save rejects a blank checkout code
- sale save persists a normalized checkout code
- refunding requires and stores the selected refund checkout code
- refund checkout options come from distinct sale checkout codes
- closing rows compute global and per-checkout sales, refunds, and net totals
- `/donnees?onglet=cloture` renders the read-only tab
