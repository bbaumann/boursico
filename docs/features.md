# Features

V1 is feature-complete but not live yet. The earlier narrow baseline is now called MVP; current V1 includes the implemented features described here.

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
- Import seller drafts and item rows from CSV/TSV exports of an external seller form
- Record seller preregistration received by email
- Record in-person seller item lists as `registered` until the signed deposit is validated
- Print a seller deposit contract recap from the seller detail page
- Validate one signed seller deposit, moving that seller's accepted `registered` items to `in_stock`
- Allow admin cancellation of a validated deposit with a local admin-password confirmation, reopening only items still `in_stock`
- Configure physical caisses with a name and initial cash amount (`fond de caisse`) from `/administration`
- Allocate each item a unique `<LETTER><NNN>` reference when its seller's deposit reaches the contract-print step, rather than at item creation
- Store one seller-set printed price per item
- Search validated `in_stock` items by `reference + title` using a live-filtered table
- Store the configured physical caisse on every sale
- Capture required buyer name or organization, postal code, and city on the checkout page
- Add selected items to a cart
- Compute subtotal, checkout fee, and final bill total automatically
- Save a sale as paid
- Store sale subtotal, checkout fee, and total paid
- Optionally print a facture from one customer `Adresse` field
- View saved sales in read-only mode from the checkout area
- Mark accepted items as defective from the admin page
- Refund defective sold items, including the 10 percent association fee
- Cancel a mistaken defective refund from the admin page
- Inspect read-only financial closing totals globally and per checkout from `/donnees`

### Sale-Day UX Constraints

- No seller information in the checkout flow
- Payment tracking is deliberately simple: a sale-level choice of `CB`, `Espèces`, or `Chèque`, with cheque number and bank name only for cheque sales
- No barcode scanner required in V1
- All operator-facing UI labels and URLs must be in French
- Search must support partial `reference + title` filtering with immediate updates on each keystroke
- The operator selects an item explicitly from the filtered table before it is added to the cart
- The checkout page uses active caisses configured by an admin; obsolete caisses can be deactivated without deleting sale history

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
- `identity_document_type`
- `identity_document_number`
- `identity_document_issued_on`
- `notes`

The following item fields must no longer be editable in normal operations:

- `reference`
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

Normal UI no longer exposes a manual `registered -> in_stock` item action. That transition happens only when the operator validates the seller's signed deposit contract.

### Expected Checkout Flow

1. Checkout loads a lightweight item catalog into page memory when the page opens.
2. If the browser has no saved active caisse, the operator chooses one configured physical caisse before sale entry is available.
3. Operator fills the required buyer name or organization, postal code, and city.
4. When the postal code is in Isère (`38xxx`), the city field offers known city suggestions from the local city lookup data and is prefilled when the postal code has exactly one known city. For other French departments, the city remains free text.
5. Operator types in the item lookup field.
6. The item table is filtered on each keystroke using `reference + title` matching.
7. Operator selects the correct item from the filtered table.
8. Item is added to the current cart.
9. Operator repeats until the customer is done.
10. Operator clicks `Finaliser`.
11. The app validates the cart against the live database before showing the finalization modal.
12. If validation succeeds, a finalization modal shows the subtotal, checkout fee, final total, item count, and payment method choice.
13. Operator chooses `CB`, `Espèces`, or `Chèque`.
14. If `Chèque` is chosen, the operator must enter the cheque number and bank name before saving.
15. Operator chooses `Retour`, `Enregistrer la vente`, or `Enregistrer et imprimer`.
16. The server re-validates that every cart item is still `in_stock` and belongs to a seller with `deposit_status = deposit_validated` at save time inside the save transaction.
17. The server rejects the sale if the phase is `post_sale`.
18. If validation succeeds, the sale, configured caisse, buyer details, payment method, and cheque traceability fields are persisted and the items become `sold`.
19. If any cart item is no longer saleable, the sale is rejected, the conflicting item is highlighted very prominently in red, and the operator must remove it before retrying.
20. After a successful save, checkout returns to a fresh state and reloads the in-memory catalog.

### Read-Only Sales View

Operators can inspect saved sales from the checkout area without admin unlock:

- `/caisse` remains the default sale-creation page
- `/caisse` links to `/caisse/ventes` with a prominent `LISTE DES VENTES` action
- `/caisse/ventes` lists saved sales newest first
- the list supports client-side filtering by sale id, buyer details, item reference, item title, invoice address details, invoice number, payment method, cheque number, and bank name
- `/caisse/ventes/{id}` shows one sale in read-only detail mode
- the detail view shows sale id, sold timestamp, buyer details, payment method, cheque traceability when present, invoice address and number when present, sale lines, seller prices at sale time, subtotal, checkout fee, and total paid
- corrected sale lines remain visible as historical sale facts and are labeled `Corrigée`
- refunded defective sale lines remain visible as historical sale facts and are labeled `Remboursé` with the refund date
- when a buyer refund exists, sale totals show the original total, then `Remboursements`, then `Total corrigé`

The read-only sales view does not expose correction actions. Mistaken sale-line correction and exceptional item correction remain on `/administration`.

### Read-Only Data Lists

Operators can inspect the main event data from `/donnees` without admin unlock:

- the main navigation exposes a `DONNÉES` entry
- the page is read-only and does not expose edit, correction, or workflow actions
- the page uses seven tabs: `Articles`, `Vendeurs`, `Ventes`, `Chèques`, `Chèques par caisse`, `Clôture`, and `Statistiques`
- the `Articles` tab lists all item statuses and supports a status filter in addition to live text search
- article text search matches item reference, title, seller code, seller name, and status
- the `Vendeurs` tab supports live text search by seller code and seller name only
- the `Ventes` tab supports live text search by sale id, buyer details, payment method, item reference, and item title
- invoice address details, cheque number, and bank name stay visible in checkout sales history, but are not part of the general data-list sale search
- the `Chèques` tab lists only cheque-payment sales with stored cheque number and bank name
- cheque rows show sale id, cheque number, bank name, and total paid amount, with the sale id linking to read-only sale detail
- the `Chèques par caisse` tab shows one section per configured caisse, listing its cheque sales sorted by bank then cheque number, with bank, cheque number, amount, buyer name, and a total line; unlike `Chèques`, it keeps cheques with missing details so each total matches the caisse's expected cheques on `Clôture`
- the `Clôture` tab shows one row per configured caisse, then a `Toutes les caisses` total row set apart by a heavy top border
- closing columns lead with what the drawer should hold (expected cash, cheques, and card), then a heavy vertical separator, then the details: sale count, fond de caisse, cash sales, and refunds paid
- per-caisse sale totals group by `sale.cash_register_id`; per-caisse refunds group by `sale_line.refund_cash_register_id`
- the `Statistiques` tab shows overall sale figures:
  - association revenue by fee: the flat registration fee paid at deposit and deducted at payout (plus the part a payout was too small to recover), the 15% seller margin on sold and missing items, and the 10% client fee net of refunds
  - money from clients: collected by payment method, refunded, and net
  - seller sales: total seller price of the items sold, at the sale price and before margin and fees, the number of sellers with at least one sale, and missing items paid to sellers
  - item counts by status and the sold share of deposited items
  - item and sold-item counts and amounts by price bracket (1–2, 3–5, 6–10, 11–20, over 20 EUR), with a separate line for items without a valid price when there are any
  - sales per buyer city, grouped case-insensitively
- seller-side statistics (margin, fee deductions, sold prices) reuse the seller sheet calculation, so they always match the `Feuilles vendeurs`

### Concurrent Checkout Rule

V1 does not use cart-level reservation.

- adding an item to a cart does not reserve it
- an item remains `in_stock` until a sale is successfully saved
- checkout catalog and save validation require the item's seller to have a validated deposit
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

If a defective item is spotted before the seller signs the deposit contract, the item is not accepted. If it was already entered, the operator may delete the `registered` item before validation.

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
- requires the admin to choose the configured physical caisse that paid the refund
- reimburses the buyer the item price plus the 10 percent association fee for that item
- keeps the original sale and original sale totals unchanged
- changes the item from `sold` to `defective`
- excludes the sale line from seller payout
- shows the item on seller sheets as `DÉFECTUEUX`

If the refund was entered by mistake, an admin may use `Annuler le remboursement`. This clears the sale-line refund fields and restores the item to `sold`; it is not the normal path for returned defective items.

### Invoice UX Rule

V1 invoice handling is intentionally simple:

- the checkout form shows one optional customer `Adresse` text field for invoice printing
- the field is empty by default
- if the operator leaves it empty, `Enregistrer et imprimer` stays disabled in the finalization modal
- if the operator fills it, `Enregistrer et imprimer` becomes available in the finalization modal
- `Enregistrer et imprimer` saves first, persists invoice number and issue date, then opens the browser print dialog
- a normal sale save does not create an invoice number
- an already saved sale can print an invoice from `/caisse/ventes/{id}` only when the address was captured on the original sale
- read-only invoice printing reuses an existing invoice number or allocates the next number once for an addressed sale that has not been invoiced yet
- invoice numbers use the configured `YYMMAFBJNNNN` sequence, for example `2611AFBJ0001`
- the printed invoice title is `Facture <number>` and uses the AFSE logo
- the printed invoice hides the application navigation/header while printing
- the printed invoice shows seller identity beside one customer block with buyer name, address, postal code and city, item lines with a wide designation column and compact `P.U. HT` header, HT totals, TVA as `0,00 EUR`, TTC total, and the VAT exemption text once

### Seller Deposit Validation

Items are entered in `registered` state whether they come from an email list, external form import, or in-person operator entry.

Before validation:

- seller and item details remain editable during `presale`
- seller contact and identity document fields may be blank while the record is still a draft
- items stay excluded from checkout and seller-sheet outputs
- incomplete or mistaken `registered` items may be corrected or deleted
- items have no reference yet; the UI shows an em dash (`—`) in its place

When the seller physically gives the toys to the association:

1. The operator opens the seller detail page.
2. The operator completes any missing phone, postal address, postal code, city, identity document nature, identity document number, issue date, or issuing authority.
3. The operator starts `Finaliser le dépôt`.
4. The operator verifies the expected seller fee, the fee already paid, and the remaining fee, then updates `Frais payés` when the seller pays at deposit time.
5. The operator clicks `Continuer`, which allocates a `<LETTER><NNN>` reference to each of the seller's items that still lacks one.
6. The operator prints `Imprimer le contrat`, which lists the now-allocated references.
7. The seller signs the printed recap with the association identity, legal mentions, seller fee status, and item prices.
8. The operator clicks `Valider le dépôt`, then confirms `Le contrat est signé`.
9. The application sets `seller.deposit_status = deposit_validated` and moves that seller's `registered` items to `in_stock`, refusing the move if any of those items still has a null reference.

The printed contract uses the configured association name, address, SIRET, and legal status. It shows the seller fee rule (`EUR 2` per started block of 15 items), expected fee, paid fee, whether payment is still due, and item prices. It does not print the seller identity document fields. It contains only the seller signature area with the acceptance text for the toy-sale rules; the application sale phase is hidden from the printed document.

Remaining seller fees never block deposit validation. They remain visible in the deposit workflow and are deducted from the seller payout during reconciliation.

After validation, normal seller and item edits are locked for that seller. The signed paper contract is the legal proof; the database status is only the operational gate. Admin cancellation is available from the seller detail page during `presale` after confirming the shared admin password. It moves only items still `in_stock` back to `registered`; `sold`, `missing`, and `defective` items remain unchanged and read-only. Revalidating the deposit later tolerates those historical item states and only moves current `registered` items back to `in_stock`.

### External Seller Form Import

External form import is an operator helper on `/vendeurs/importer`.

- the upload accepts CSV and TSV exports from an external seller form provider
- the importer detects the form header row, including Framaforms exports with metadata rows before the real header
- seller drafts are imported when first name and last name are present
- phone, address, postal code, city, email, notes, and item prices may be missing at import time
- item data comes from one multiline `Jouet,Prix` field with one item per line, title first and whole-euro price last
- valid item lines become `registered` items
- item lines with a recoverable title but missing or invalid price are imported as `registered` items with a missing seller price and import notes
- item lines without a recoverable title are not imported and are recorded in import notes
- a seller with any item missing a seller price cannot be deposit-validated until the item is corrected or deleted
- unreconciled city values leave the seller city blank while keeping the submitted postal code and recording the submitted city in `seller.import_notes`
- import notes show `Import à vérifier` on the seller detail page and can be edited or cleared from seller edit
- duplicate sellers are not auto-imported
- when exactly one existing duplicate seller matches, the operator may append valid new item lines to that seller
- when appending duplicate submissions, an item line is ignored if the existing seller already has the exact same title and seller price
- when several duplicate seller matches exist, the only supported action is to ignore the submission
- imported sellers default the paid seller fee to `EUR 0`
- imported seller identity document fields stay blank until the operator completes them before deposit validation

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
- final amount to pay the seller after association margin and unpaid fee deduction

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
- association margin
- expected seller fee
- already-paid seller fee
- remaining fee due
- final amount to pay the seller

The final amount to pay the seller is:

- item subtotal minus 15 percent association margin and remaining fee due, floored at zero

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
