# Features

## Feature Split

The system is intentionally split into two independent features over a shared database:

1. Event operations
2. Post-sale reconciliation

The split is important because the users, UI needs, and operational risks are different.

## Event Operations

### Goal

Allow organizers to register toys before the event and allow sale-day volunteers to create sales reliably with the smallest possible cognitive load.

### Current Capabilities

- Create sellers
- Prevent accidental duplicate sellers during seller creation and seller editing
- Search sellers by code or name using a live-filtered sellers table
- Create items manually from seller-provided lists
- Prefill seller and item rows from a one-seller Excel workbook before reviewed save
- Record seller preregistration received by email
- Register sellers and items in person when toys are physically brought in
- Confirm preregistered items against physically delivered toys
- Assign one unique reference per item
- Store one seller-set printed price per item
- Search items by `reference + title` using a live-filtered table
- Capture required buyer name or organization, postal code, and city on the checkout page
- Add selected items to a cart
- Compute subtotal, checkout fee, and final bill total automatically
- Save a sale as paid
- Store sale subtotal, checkout fee, and total paid
- Optionally print a receipt from one `Adresse` field
- View saved sales in read-only mode from the checkout area
- Mark accepted items as defective from the admin page
- Refund defective sold items, including the 10 percent association fee
- Cancel a mistaken defective refund from the admin page

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

### Sale Phases

The event has one persisted sale phase:

- `presale` / `Prévente`
- `sale` / `Vente`
- `post_sale` / `Après-vente`

`presale` is the default. Sales are allowed in `presale` and `sale` so the sale day is not blocked if organizers are still doing final corrections. `sale` locks seller and item master data. `post_sale` blocks new sale validation and sale save server-side.

The phase appears near the event/software name on every page as information only. The admin page is available as a fourth main navigation item. Admins unlock `/administration` with the shared admin password, then change the phase from that page without re-entering the password for every action.

### Seller And Item Data Freeze At Sale Start

Once the event is in `sale` or `post_sale`, seller and item master data become read-only in normal operations.

The following seller fields must no longer be editable in normal operations:

- `first_name`
- `last_name`
- `email`
- `phone`
- `postal_address`
- `postal_code`
- `city`
- `notes`

The following item fields must no longer be editable in normal operations:

- `reference`
- `item_number`
- `seller_id`
- `title`
- `seller_price`

When seller and item master data are locked, only workflow-driven status changes remain allowed:

- `in_stock -> sold` when saving a sale
- `in_stock -> missing` during post-sale reconciliation
- `in_stock -> defective` when an admin finds a defect after acceptance
- restoring a mistakenly marked `missing` item to `in_stock` as an admin reconciliation correction
- restoring a `defective` item to `in_stock` only when it has no buyer refund history
- correcting a mistaken sale line as an admin sale correction, which marks the sale line corrected and returns the item to `in_stock`
- refunding a defective sold item as an admin sale correction, which records the refund and moves the item to `defective`
- canceling a mistaken defective refund as an admin correction, which clears the refund fields and restores the item to `sold`

### Expected Checkout Flow

1. Checkout loads a lightweight item catalog into page memory when the page opens.
2. Operator fills the required buyer name or organization, postal code, and city.
3. When the postal code is in Isère (`38xxx`), the city field offers known city suggestions from the local city lookup data and is prefilled when the postal code has exactly one known city. For other French departments, the city remains free text.
4. Operator types in the item lookup field.
5. The item table is filtered on each keystroke using `reference + title` matching.
6. Operator selects the correct item from the filtered table.
7. Item is added to the current cart.
8. Operator repeats until the customer is done.
9. Operator clicks `Finalize`.
10. The app validates the cart against the live database before showing the finalization modal.
11. If validation succeeds, a finalization modal shows the subtotal, checkout fee, final total, item count, and payment method choice.
12. Operator chooses `CB`, `Espèces`, or `Chèque`.
13. If `Chèque` is chosen, the operator must enter the cheque number and bank name before saving.
14. Operator chooses `Go back`, `Save`, or `Save and print receipt`.
15. The server re-validates that every cart item is still `in_stock` at save time inside the save transaction.
16. The server rejects the sale if the phase is `post_sale`.
17. If validation succeeds, the sale, buyer details, payment method, and cheque traceability fields are persisted and the items become `sold`.
18. If any cart item is no longer `in_stock`, the sale is rejected, the conflicting item is highlighted very prominently in red, and the operator must remove it before retrying.
19. After a successful save, checkout returns to a fresh state and reloads the in-memory catalog.

### Read-Only Sales View

Operators can inspect saved sales from the checkout area without admin unlock:

- `/caisse` remains the default sale-creation page
- `/caisse` links to `/caisse/ventes` with a prominent `LISTE DES VENTES` action
- `/caisse/ventes` lists saved sales newest first
- the list supports client-side filtering by sale id, buyer details, item reference, item title, receipt address details, payment method, cheque number, and bank name
- `/caisse/ventes/{id}` shows one sale in read-only detail mode
- the detail view shows sale id, sold timestamp, buyer details, payment method, cheque traceability when present, receipt address details when present, sale lines, seller prices at sale time, subtotal, checkout fee, and total paid
- corrected sale lines remain visible as historical sale facts and are labeled `Corrigée`
- refunded defective sale lines remain visible as historical sale facts and are labeled `Remboursé` with the refund date
- when a buyer refund exists, sale totals show the original total, then `Remboursements`, then `Total corrigé`

The read-only sales view does not expose correction actions. Mistaken sale-line correction and exceptional item correction remain on `/administration`.

### Read-Only Data Lists

Operators can inspect the main event data from `/donnees` without admin unlock:

- the main navigation exposes a `DONNÉES` entry
- the page is read-only and does not expose edit, correction, or workflow actions
- the page uses four tabs: `Articles`, `Vendeurs`, `Ventes`, and `Chèques`
- the `Articles` tab lists all item statuses and supports a status filter in addition to live text search
- article text search matches item reference, title, seller code, seller name, and status
- the `Vendeurs` tab supports live text search by seller code and seller name only
- the `Ventes` tab supports live text search by sale id, buyer details, payment method, item reference, and item title
- sale receipt address details, cheque number, and bank name stay visible in checkout sales history, but are not part of the general data-list sale search
- the `Chèques` tab lists only cheque-payment sales with stored cheque number and bank name
- cheque rows show sale id, cheque number, bank name, and total paid amount, with the sale id linking to read-only sale detail

### Concurrent Checkout Rule

V1 does not use cart-level reservation.

- adding an item to a cart does not reserve it
- an item remains `in_stock` until a sale is successfully saved
- the app may validate the cart before showing the finalization modal for UX reasons
- the server must re-check all cart items at save time inside the sale transaction
- the server must reject cart validation and sale persistence when the event phase is `post_sale`
- if any cart item is no longer available, the save must fail clearly rather than partially succeed

### Sale Correction

If a volunteer saved a sale with the wrong item reference, an admin may correct the active sale line from `/administration`.

The correction:

- requires the admin page to be unlocked with the shared admin password
- records a correction timestamp on the sale line
- returns the item to `in_stock`
- keeps the original sale and original sale totals unchanged
- excludes the corrected sale line from seller-sheet payable output
- lets the item be sold again normally

### Defective Items And Buyer Refunds

If a defective item is spotted during physical handover, the item is not accepted. If it was preregistered, the operator may delete the `registered` item; if it was being entered in `Dépôt` mode, the row is not created.

If a defect is spotted after the item is already `in_stock`, an admin uses `/administration` to mark it `défectueux`.

The defective status:

- prevents checkout
- keeps the item on the seller sheet as an item to return
- excludes the item from seller payout
- allows `defective -> in_stock` only as an admin correction when no buyer refund exists
- allows `defective -> sold` only when an admin cancels a defective refund that was entered by mistake

If a buyer returns a defective sold item, an admin uses `Rembourser` from `Correction des articles` on `/administration`.

The refund workflow:

- records refund fields on the sale line
- reimburses the buyer the item price plus the 10 percent association fee for that item
- keeps the original sale and original sale totals unchanged
- changes the item from `sold` to `defective`
- excludes the sale line from seller payout
- shows the item on seller sheets as `DÉFECTUEUX`

If the refund was entered by mistake, an admin may use `Annuler le remboursement`. This clears the sale-line refund fields and restores the item to `sold`; it is not the normal path for returned defective items.

### Receipt UX Rule

V1 receipt handling is intentionally simple:

- the checkout form shows one optional `Adresse` text field for receipt or invoice printing
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
- Items not yet delivered may remain `registered`; they can be marked `in_stock` later if the seller returns with them.
- A `registered` item may be deleted when it was entered by mistake or the operator wants to clean up the seller record.
- If an item was mistakenly entered as physically present, it may also be deleted while still `in_stock`.
- V1 does not use a `not_provided` status.

#### In-Person Registration

- Seller and items are entered while the toys are physically present.
- Items are created directly in `in_stock` state.

### Excel Seller And Item Prefill

Excel intake is an operator helper on `/vendeurs/creer`, not a direct import path.

- the upload accepts one `.xlsx` workbook with one visible seller sheet; hidden helper sheets are ignored
- the parser reads seller identity from the template labels and item title/price rows from columns `B:C`
- item rows may continue beyond the old 15-row template area
- upload fills the editable seller and item form and saves nothing
- the operator reviews the prefilled form, then saves through the normal intake action
- final save creates the seller and item rows in one transaction
- duplicate sellers redirect to the existing seller detail page with the parsed item rows prefilled in `Ajouter des articles`
- until workbook address handling is formally designed, Excel prefill sets seller postal address to `UNKNOWN`
- Excel prefill defaults the paid seller fee to `EUR 0`

## Post-Sale Reconciliation

### Goal

Produce per-seller outputs for physical item gathering and seller payout after the sale is finished.

Admins may open the reconciliation dashboard before the event is in `post_sale`, but the dashboard remains read-only until then.

### V1 Capabilities

- Provide an admin-gated reconciliation dashboard at `/rapprochement`
- Read authoritative item and sale data from the shared database
- Group items by seller
- Compute payable items and returned items per seller
- Print all seller sheets from the dashboard through `/rapprochement/feuilles`
- Generate one printable seller sheet per seller with at least one `sold`, `missing`, `in_stock`, or `defective` item
- Allow marking an unsold item as `missing` through an admin-only action
- Allow restoring a mistakenly marked `missing` item to `in_stock` only as an admin reconciliation correction
- Regenerate the seller sheet after a `missing` status correction
- Track seller registration fees and deduct unpaid fees from reconciliation payout

Seller-sheet printing and missing-item reconciliation corrections are enabled only when the event phase is `post_sale`.

### Seller Sheet Content

The seller sheet must be human-readable and printable.

It must contain:

- seller identity
- returned items list
- payable items list
- fee breakdown
- final amount to pay the seller after unpaid fee deduction

The returned items list appears first because operators use the sheet to gather physical returns before preparing seller payout.
Returned rows include a printed checkbox column so operators can mark each item as found.
The payable items list appears second, visually separated from the return checklist and adjacent to the final amount.

### Payable Items List

The payable list contains:

- active sold items that have not been corrected or refunded
- items with status `missing`

For each payable item, show:

- reference
- item title when useful
- seller price
- a visible `MANQUANT` marker when relevant

### Returned Items List

The returned list contains all items still marked `in_stock` or `defective`.

For each returned item, show:

- checkbox
- reference
- item title when useful
- a visible `DÉFECTUEUX` marker when relevant

### Seller Fees And Final Amount To Pay

The expected seller fee is:

- EUR 0 when the seller has no items
- EUR 2 for each started block of 15 seller items in the system, including items that remain `registered`

The operator records the amount already paid by the seller on the seller form.

The seller sheet shows:

- item subtotal for payable items
- expected seller fee
- already-paid seller fee
- remaining fee due
- final amount to pay the seller

The final amount to pay the seller is:

- item subtotal minus remaining fee due, floored at zero

### Physical Workflow Supported By The Sheet

1. An operator uses the sheet to gather all returned items.
2. If a returned item cannot be found physically, an admin marks it as `missing`.
3. The sheet is regenerated.
4. If the `missing` status change was a mistake and the item is found before finalization, an admin restores the item to `in_stock` as a reconciliation correction and regenerates the sheet.
5. Another operator uses the final sheet to prepare the seller payout check.
6. The check, final sheet, and returned items are physically grouped for seller pickup.

In V1, `missing` is an operationally restricted action:

- only an admin may mark an item as `missing`
- only an admin may restore a mistakenly marked `missing` item to `in_stock`
- in V1, "admin" means someone who knows the shared admin password
- the dashboard at `/rapprochement` is admin-gated
- `Admin:SharedPassword` must be configured for production; blank values and `change-me` are invalid
- this control exists to reduce misuse by other operators, not to provide full identity-based security

### Excluded Items

The following item states are excluded from seller-sheet payable/returned outputs:

- `registered`

Sellers whose items are only in excluded states do not get seller sheets.

## V1 Non-Goals

- Seller self-service portal
- Generic stock management beyond event needs
- Integrated card-payment device flows
- Complex ERP/accounting features
- Multi-station offline sync
