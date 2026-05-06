# Checkout Receipt Print Design

## Goal

Build checkout slice 3: let the operator optionally enter the buyer address, save the sale, and print a receipt from the checkout flow after the sale has been persisted.

## Source Of Truth

This slice implements the existing accepted docs:

- `docs/features.md`: V1 receipt handling uses one optional address field.
- `docs/checkout-contract.md`: `Enregistrer et imprimer` is only available when the buyer address is filled.
- `docs/decisions.md` D-025: printing must save first and must not create ambiguity about persistence.
- `docs/architecture.md`: the operations UI owns optional receipt printing.

## Scope

This slice includes:

- one optional buyer-address text field on `/caisse`
- storing the normalized buyer address on the saved sale
- `Enregistrer et imprimer` in the finalization modal
- disabling that action when the buyer address is blank
- a print-only receipt section rendered by the checkout page
- opening the browser print dialog only after sale persistence succeeds
- returning to a fresh checkout page after printing
- keeping the existing conflict behavior for save-time conflicts

This slice does not include:

- a separate receipt route
- a receipt-requested flag
- a receipt-printed flag
- receipt reprinting or sale lookup
- printer configuration beyond the existing `Printing:ReceiptHeaderText`

## Architecture

The checkout page remains the user-facing workflow owner at `/caisse`. Razor renders the buyer address field, modal action, and a hidden print-only receipt container. The page model exposes `Printing:ReceiptHeaderText` so the printed receipt uses the configured association/event header.

The existing `POST /api/ventes` endpoint remains the sale creation API. Its request body gains an optional `buyerAddress` string. `CheckoutService.SaveSaleAsync` accepts that value, trims it, stores `null` when blank, and persists the normalized text on `sale.buyer_address` in the same transaction as the sale lines and item status updates.

The JavaScript keeps the cart in memory. `Enregistrer la vente` saves without printing. `Enregistrer et imprimer` sends the same sale request with buyer address, renders the receipt from the confirmed cart and response totals, calls `window.print()`, and then reloads the page after the print dialog returns.

## UI Behavior

The cart panel adds a French buyer address text area, empty by default. The finalization modal has three actions:

- `Retour`
- `Enregistrer la vente`
- `Enregistrer et imprimer`

`Enregistrer et imprimer` is disabled while the buyer address is blank or whitespace. When the buyer address becomes non-blank, the action is enabled. Both save actions still run the authoritative save-time validation through `POST /api/ventes`.

If save-time validation fails, the modal closes, conflicting cart rows are highlighted, the cart stays intact, and no print dialog opens.

## Printed Receipt

The printed receipt contains:

- configured receipt header text
- sale number returned by the API
- sale timestamp from the browser at print time
- buyer address entered by the operator
- cart item lines with reference, title, and price
- subtotal, checkout fee, and total paid

Only the receipt area is visible in print media. The normal checkout UI is hidden for printing.

## Testing

Tests should cover:

- `CheckoutService.SaveSaleAsync` stores the trimmed buyer address
- blank or whitespace buyer addresses are stored as `null`
- `POST /api/ventes` accepts `buyerAddress` and persists it
- `/caisse` renders French buyer address UI, print modal action, and print-only receipt hooks
- JavaScript sends buyer address to `/api/ventes`
- JavaScript calls `window.print()` only in the print save path
- print CSS hides the checkout UI and shows the receipt area
