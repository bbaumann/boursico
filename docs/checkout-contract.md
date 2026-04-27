# Checkout Contract

## Purpose

This document defines the V1 sale-day checkout behavior in enough detail that implementation should not need to guess the interaction model.

The checkout flow is the most interaction-heavy part of the application, so it is documented separately from the broader feature overview.

## Core Model

- the checkout page loads a lightweight item catalog once when the page opens
- the catalog lives only in page memory for the lifetime of the checkout page
- there is no `sessionStorage` or other browser persistence for the catalog
- there is no background refresh while the page stays open
- after a successful save, the checkout returns to a fresh state and reloads the catalog

The server remains authoritative for item availability and sale persistence.

## Catalog Content

The lightweight checkout catalog should contain only the fields needed for lookup and cart display, such as:

- item id
- reference
- title
- seller price in cents
- current status

The catalog exists to make lookup fast and local.

It is not the final authority on whether an item can still be sold.

## Lookup Interaction

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

## Finalize Flow

When the customer says they are done, the operator clicks `Finalize`.

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

The modal has exactly three actions:

- `Go back`
- `Save`
- `Save and print receipt`

### Modal Action Rules

`Go back`

- closes the modal
- returns to the editable cart
- does not save anything

`Save`

- performs a second authoritative validation at save time
- persists the sale if validation succeeds
- returns to a fresh checkout state
- reloads the in-memory catalog

`Save and print receipt`

- is disabled unless `receipt_details` was filled on the checkout page
- performs a second authoritative validation at save time
- persists the sale if validation succeeds
- opens the browser print dialog
- returns to a fresh checkout state
- reloads the in-memory catalog

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

## Receipt Handling

- the checkout form has one optional `receipt_details` field
- if it is empty, no receipt action is available in the finalization modal
- if it is filled, `Save and print receipt` becomes available
- there is no separate receipt-requested flag
- there is no separate receipt-print page required as a user-facing workflow step

## Non-Goals For V1 Checkout

- no cart reservation state
- no background sync loop for the catalog while the page stays open
- no browser persistence of the catalog across reloads
- no barcode scanner requirement
- no payment method tracking
- no silent automatic repair of conflicts
