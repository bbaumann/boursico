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
- Store seller price and customer sale price per item
- Search items by reference using typed autocomplete
- Add selected items to a cart
- Compute the bill total automatically
- Save a sale as paid
- Optionally print a receipt

### Sale-Day UX Constraints

- No seller information in the checkout flow
- No payment method tracking beyond the fact that the sale was completed
- No barcode scanner required in V1
- Search must support partial reference entry with fast suggestions
- The operator must select the item explicitly from the matching list before it is added to the cart

### Item Data Freeze At Sale Start

Once checkout opens for the event, item master data becomes read-only.

The following fields must no longer be editable in normal operations:

- `reference`
- `item_number`
- `seller_id`
- `title`
- `seller_price`
- `sale_price`

After sale start, only workflow-driven status changes remain allowed in V1:

- `in_stock -> sold` when saving a sale
- `in_stock -> missing` during post-sale reconciliation

### Expected Checkout Flow

1. Operator types part or all of the toy reference.
2. Matching items are proposed in a high-contrast list.
3. Operator selects the correct item.
4. Item is added to the current cart.
5. Operator repeats until the customer is done.
6. Operator saves the sale.
7. The server re-validates that every cart item is still `in_stock` at save time.
8. If validation succeeds, the sale is persisted and the items become `sold`.
9. If any cart item is no longer `in_stock`, the sale is rejected, the conflicting item is highlighted, and the operator must remove it before retrying.
10. Operator optionally prints a receipt in rare reimbursement cases.

### Concurrent Checkout Rule

V1 does not use cart-level reservation.

- adding an item to a cart does not reserve it
- an item remains `in_stock` until a sale is successfully saved
- the server must re-check all cart items at save time inside the sale transaction
- if any cart item is no longer available, the save must fail clearly rather than partially succeed

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
- Allow marking an unsold item as missing
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
