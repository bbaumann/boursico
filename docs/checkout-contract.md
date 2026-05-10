# Checkout Contract

## Purpose

This document defines the V1 sale-day checkout behavior in enough detail that implementation should not need to guess the interaction model.

The checkout flow is the most interaction-heavy part of the application, so it is documented separately from the broader feature overview.

## Core Model

- the checkout page loads a lightweight item catalog once when the page opens
- the catalog lives only in page memory for the lifetime of the checkout page
- there is no `sessionStorage` or other browser persistence for the catalog
- the physical checkout code is stored separately in browser `localStorage`
- there is no background refresh while the page stays open
- after a successful save, the checkout returns to a fresh state and reloads the catalog

The server remains authoritative for item availability and sale persistence.

## Checkout Code

Each physical checkout station uses a simple text checkout code.

- the checkout page blocks sale entry until the operator saves a non-empty `Nom de la caisse`
- the code is kept in browser `localStorage` so the same station keeps using it across page reloads
- every sale save request includes the current checkout code
- the server rejects blank checkout codes and stores the trimmed value on `sale.checkout_code`
- uniqueness is handled operationally by event organizers; the app does not maintain a checkout-station registry

## Catalog Content

The lightweight checkout catalog should contain only the fields needed for lookup and cart display, such as:

- item id
- reference
- title
- seller price in cents
- current status

The catalog exists to make lookup fast and local.

It is not the final authority on whether an item can still be sold.

The server also checks the event sale phase. Checkout validation and sale persistence are allowed in `presale` and `sale`, and rejected in `post_sale`.

## Lookup Interaction

## Buyer Details

The top of the checkout content includes required buyer information fields before the item lookup:

- buyer name or organization
- postal code
- city

The postal code input is numeric. When the postal code is an Isère code (`38xxx`), the city input offers a dropdown of matching cities from local static data generated from the official French Geo API commune dataset for department 38. When one city matches the postal code, the city field is prefilled. For other French departments, the city input remains free text.

Buyer details are persisted on the sale when the operator saves. The sale is rejected if buyer name, postal code, city, or checkout code is blank. Buyer details are separate from the optional `buyer_address`; filling name, postal code, and city alone does not mean an invoice was requested.

The checkout lookup is not an autocomplete widget.

It is a live-filtered table driven by a text field.

Rules:

- the table updates on each keystroke
- matching is done against `reference + title`
- filtering happens client-side from the in-memory catalog
- the operator selects an item by clicking it in the filtered table
- there is no keyboard-highlighted suggestion model to confirm with `Enter`
- there is no `Escape` behavior for closing suggestions

## Cart Behavior

- clicking a matching row adds the item to the cart
- adding an item to the cart does not reserve it
- if the operator tries to add the same item twice, the UI shows a clear `already in cart` message
- duplicate adds are not silently ignored
- duplicate adds are not allowed to proceed to save-time failure
- the cart shows each item at its printed item price, which is the seller price in V1
- the cart computes a subtotal, a checkout fee, and a final total

## Fee Computation

- item printed price = seller price
- subtotal = sum of item seller prices in the cart
- checkout fee = 10 percent of the subtotal
- fee rounding = nearest cent, with midpoint values rounded away from zero
- final total = subtotal + checkout fee

## Finalization Flow

When the customer says they are done, the operator clicks `Finaliser`.

This starts a two-step validation model.

### First Validation

Before showing the finalization modal, the app validates the cart against the live database.

Purpose:

- give the operator early feedback before the final action buttons appear
- catch stale in-memory availability before final confirmation

If validation fails:

- the modal is not shown
- the conflicting cart line is highlighted very prominently in red
- a clear error message explains that the item was already sold elsewhere or a prior operator made a mistake
- the operator must remove the item manually before retrying

### Finalization Modal

If the first validation succeeds, a finalization modal is shown.

The modal must show:

- subtotal
- checkout fee
- final total
- number of items
- payment method choice: `CB`, `Espèces`, or `Chèque`
- cheque number and bank name fields when `Chèque` is selected

The modal has exactly three actions:

- `Retour`
- `Enregistrer la vente`
- `Enregistrer et imprimer`

### Modal Action Rules

`Retour`

- closes the modal
- returns to the editable cart
- does not save anything

`Enregistrer la vente`

- performs a second authoritative validation at save time
- validates the sale-level payment method and cheque details
- persists the sale with its checkout code if validation succeeds
- returns to a fresh checkout state
- reloads the in-memory catalog

`Enregistrer et imprimer`

- is disabled unless the checkout `Adresse` field, stored internally as `buyer_address`, was filled on the checkout page
- performs a second authoritative validation at save time
- validates the sale-level payment method and cheque details
- persists the sale with its checkout code if validation succeeds
- opens the browser print dialog
- returns to a fresh checkout state
- reloads the in-memory catalog

## Payment Details

The checkout tracks a simple sale-level payment method.

- the operator must choose `CB`, `Espèces`, or `Chèque` in the finalization modal
- `Chèque` requires cheque number and bank name before save
- non-cheque saves clear cheque number and bank name
- payment method and cheque details are persisted on the sale
- payment details are visible in read-only sales history and cheque data lists

## Second Validation

The server must validate again inside the actual save transaction.

This is required even if the pre-modal validation succeeded, because another station may still sell an item in the small gap before the final save action.

The server remains the final authority.

## Conflict Handling

If save-time validation fails:

- the sale is rejected
- the conflicting cart line is highlighted very prominently in red
- the message should make clear that this should not normally happen and likely indicates a prior operator mistake or an item already sold elsewhere
- the operator must remove the conflicting item manually before retrying
- the rest of the cart remains intact

This error should look visually strong and difficult to miss.

If save-time validation fails because the event is in `post_sale`, no cart line is highlighted. The message should explain that the sale is closed and an admin must move the event back to another phase before new sales can be saved.

## Invoice Handling

- the checkout form has one optional operator-facing `Adresse` field, stored internally as `buyer_address`
- if it is empty, no invoice print action is available in the finalization modal
- if it is filled, `Enregistrer et imprimer` becomes available
- `Enregistrer et imprimer` creates a persisted invoice number and issue date only after the sale save succeeds
- normal `Enregistrer la vente` saves do not create invoice numbers
- `/caisse/ventes/{id}` opens the read-only sale detail for an already saved sale
- from the read-only sale detail, `Imprimer la facture` is available only if `buyer_address` was captured when the sale was saved
- if that sale already has invoice data, read-only printing reuses the persisted invoice number and issue date
- if that sale has `buyer_address` but no invoice number yet, the print action allocates the next server-side invoice number, persists the issue date, then opens the print dialog
- invoice numbers use the configured event period and code, for example `2611AFBJ0001`
- the printed invoice title is `Facture <invoice number>`
- the printed invoice uses one customer block containing buyer name, address, postal code, and city beside the seller identity block
- the printed invoice line table uses the compact `P.U. HT` unit-price label and gives the designation column the largest width
- the application event header and navigation are hidden while printing the invoice
- there is no separate invoice-print page required as a user-facing workflow step

## Non-Goals For V1 Checkout

- no cart reservation state
- no background sync loop for the catalog while the page stays open
- no browser persistence of the catalog across reloads
- no barcode scanner requirement
- no integrated card-payment terminal or payment-provider workflow
- no full accounting ledger
- no silent automatic repair of conflicts
