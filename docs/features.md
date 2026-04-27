# Features

## Feature Split

The system is intentionally split into two independent features over a shared database:

1. Event operations
2. Post-sale reconciliation

The split is important because the users, UI needs, and operational risks are different.

## Event Operations

### Goal

Allow organizers to register toys before the event and allow sale-day volunteers to create sales reliably with the smallest possible cognitive load.

### V1 Capabilities

- Create sellers
- Create items manually from seller-provided lists
- Record seller preregistration received by email
- Register sellers and items in person when toys are physically brought in
- Confirm preregistered items against physically delivered toys
- Assign one unique reference per item
- Store one seller-set printed price per item
- Search items by `reference + title` using a live-filtered table
- Add selected items to a cart
- Compute subtotal, checkout fee, and final bill total automatically
- Save a sale as paid
- Store sale subtotal, checkout fee, and total paid
- Optionally print a receipt from one receipt-details field

### Sale-Day UX Constraints

- No seller information in the checkout flow
- No payment method tracking beyond the fact that the sale was completed
- No barcode scanner required in V1
- All operator-facing UI labels and URLs must be in French
- Search must support partial `reference + title` filtering with immediate updates on each keystroke
- The operator selects an item explicitly from the filtered table before it is added to the cart

### Checkout Pricing Rule

- the seller-entered item price is also the printed price seen by the customer
- cart subtotal = sum of item seller prices
- checkout fee = 10 percent of subtotal, rounded to the nearest cent with midpoint values rounded away from zero
- final amount paid = subtotal + checkout fee
- seller payout never includes the checkout fee

### Item Data Freeze At Sale Start

Once checkout opens for the event, item master data becomes read-only.

The following fields must no longer be editable in normal operations:

- `reference`
- `item_number`
- `seller_id`
- `title`
- `seller_price`

After sale start, only workflow-driven status changes remain allowed in V1:

- `in_stock -> sold` when saving a sale
- `in_stock -> missing` during post-sale reconciliation

### Expected Checkout Flow

1. Checkout loads a lightweight item catalog into page memory when the page opens.
2. Operator types in the lookup field.
3. The item table is filtered on each keystroke using `reference + title` matching.
4. Operator selects the correct item from the filtered table.
5. Item is added to the current cart.
6. Operator repeats until the customer is done.
7. Operator clicks `Finalize`.
8. The app validates the cart against the live database before showing the finalization modal.
9. If validation succeeds, a finalization modal shows the subtotal, checkout fee, final total, and item count.
10. Operator chooses `Go back`, `Save`, or `Save and print receipt`.
11. The server re-validates that every cart item is still `in_stock` at save time inside the save transaction.
12. If validation succeeds, the sale is persisted and the items become `sold`.
13. If any cart item is no longer `in_stock`, the sale is rejected, the conflicting item is highlighted very prominently in red, and the operator must remove it before retrying.
14. After a successful save, checkout returns to a fresh state and reloads the in-memory catalog.

### Concurrent Checkout Rule

V1 does not use cart-level reservation.

- adding an item to a cart does not reserve it
- an item remains `in_stock` until a sale is successfully saved
- the app may validate the cart before showing the finalization modal for UX reasons
- the server must re-check all cart items at save time inside the sale transaction
- if any cart item is no longer available, the save must fail clearly rather than partially succeed

### Receipt UX Rule

V1 receipt handling is intentionally simple:

- the checkout form shows one optional receipt-details text field
- the field is empty by default
- if the operator leaves it empty, `Save and print receipt` stays disabled in the finalization modal
- if the operator fills it, `Save and print receipt` becomes available in the finalization modal
- `Save and print receipt` saves first, then opens the browser print dialog
- V1 does not track a separate receipt-requested flag or receipt-printed flag

### Intake Modes

#### Email Preregistration

- Seller and items are entered from the emailed list.
- Items start in `registered` state.
- When the seller physically delivers toys, the operator reviews the preregistered list line by line.
- Delivered items become `in_stock`.
- Items declared but not actually delivered become `not_provided`.
- A `Check all` helper may be provided to speed up confirmation, with manual correction for exceptions.

#### In-Person Registration

- Seller and items are entered while the toys are physically present.
- Items are created directly in `in_stock` state.

## Post-Sale Reconciliation

### Goal

Produce per-seller outputs for physical item gathering and seller payout after the sale is finished.

### V1 Capabilities

- Read authoritative item and sale data from the shared database
- Group items by seller
- Compute payable items and returned items per seller
- Generate one printable seller sheet per seller
- Allow marking an unsold item as missing through an admin-only action
- Regenerate the seller sheet after a missing-item correction

### Seller Sheet Content

The seller sheet must be human-readable and printable.

It must contain:

- seller identity
- payable items list
- returned items list
- final amount to pay the seller

### Payable Items List

The payable list contains:

- sold items
- missing items

For each payable item, show:

- reference
- short description if useful
- seller price
- a visible `MISSING` marker when relevant

### Returned Items List

The returned list contains all items still marked `in_stock`.

For each returned item, show:

- reference
- short description if useful

### Final Amount To Pay

In V1, the final amount to pay the seller is:

- the sum of seller prices for all items in the payable list

The following stay outside the system in V1:

- the EUR 2 fee per 15 items
- any seller-facing fee computation

### Physical Workflow Supported By The Sheet

1. An operator uses the sheet to gather all returned items.
2. If a returned item cannot be found physically, an admin marks it as `missing`.
3. The sheet is regenerated.
4. Another operator uses the final sheet to prepare the seller payout check.
5. The check, final sheet, and returned items are physically grouped for seller pickup.

In V1, `missing` is an operationally restricted action:

- only an admin may mark an item as `missing`
- in V1, "admin" means someone who knows the shared admin password
- this control exists to reduce misuse by other operators, not to provide full identity-based security

### Excluded Items

The following item states are excluded from seller-sheet payable/returned outputs:

- `registered`
- `not_provided`

## V1 Non-Goals

- Seller self-service portal
- Generic stock management beyond event needs
- Integrated payment terminal flows
- Complex ERP/accounting features
- Multi-station offline sync
- In-app fee management for the EUR 2 per 15 items seller fee
