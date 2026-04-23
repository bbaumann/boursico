# Frontend Architecture

## Tech Stack

| Layer | Choice | Rationale |
|---|---|---|
| Backend | ASP.NET Core (Razor Pages) | Developer expertise, self-hosted on Windows LAN, single deployable |
| Frontend | Razor Pages + vanilla JS | App is thin — fewer than 10 pages, simple forms; no framework justified |
| Styling | Tailwind CSS | Consistent large/high-contrast UI; agent-friendly; no custom CSS drift |
| Interactivity | Vanilla JS (checkout page only) | One page has real client-side state; all others are pure server-rendered |

Frameworks considered and rejected:
- **Vue / Angular** — overkill for this scope; adds build complexity without proportional value
- **Blazor Server** — viable, but WebSocket dependency is unnecessary; vanilla JS scope is small
- **HTMX / Alpine.js** — cart state requires client-side JS regardless; mixing tools adds complexity without benefit

## Password Protection

The `missing` action (reconciliation admin) is protected by a shared password.

Implementation: ASP.NET Core session middleware. The `/reconciliation/{sellerId}/missing` page checks for a valid session cookie before rendering. A simple login form sets the cookie. No user accounts, no JWT, no identity framework — ~30 lines of middleware.

This is not a security mechanism; it is a misuse-prevention gate as specified in the domain model.

## Pages

### Pure Razor (no JavaScript)

All pages below are server-rendered. Interactions are standard form POST → server validates → redirect.

| Page | Purpose |
|---|---|
| `/events/create` | Bootstrap a new event (code + name) |
| `/sellers` | List sellers for the current event |
| `/sellers/create` | Create a seller — first name, last name, email, phone |
| `/sellers/{id}/items` | List items for a seller |
| `/sellers/{id}/items/create` | Create an item — title, seller price (sale price auto-derived at +20%) |
| `/sellers/{id}/confirm` | Preregistration confirmation — checkbox per registered item, Check All helper |
| `/reconciliation` | List sellers with links to their sheets |
| `/reconciliation/{sellerId}` | Printable seller sheet — payable items, returned items, amount to pay |
| `/reconciliation/{sellerId}/missing` | Admin password gate + mark item as missing |

Print CSS handles the seller sheet page. No JS required.

### Checkout Page (JavaScript)

`/checkout` — the only page with client-side logic. See section below.

## Checkout JS Module

One self-contained file: `checkout.js`.

### State

```
cart[]          — [{ itemId, reference, title, salePriceAtSale, sellerPriceAtSale }]
conflictItemId  — id of the item rejected by the server at save time (null normally)
```

### Responsibilities

**Autocomplete**
- Debounced `fetch` on keyup to `GET /api/items/search?q=&eventId=`
- Renders a result list below the search input
- Only `in_stock` items are returned by the API
- Clicking a result adds it to the cart and clears the search field and result list

**Cart**
- Add item from autocomplete selection
- Remove item via per-line button
- Compute and display running total reactively

**Save sale**
- `POST /api/sales` with `{ eventId, cartItems[], receiptDetails? }`
- On success: clear cart, show brief confirmation, ready for next customer
- On conflict (HTTP 409): server returns `{ conflictingItemId }` — highlight that item in the cart, keep the rest, let operator remove and retry

**Receipt**
- Optional free-text field, empty by default
- Included in the save payload only if non-empty
- On successful save where `receiptDetails` was filled: `window.print()` on a print-only receipt section (hidden in normal view via CSS)

### API Surface

The checkout module talks to exactly two endpoints:

```
GET  /api/items/search?q={partial}&eventId={id}
     → [{ id, reference, title, salePriceAtSale, sellerPriceAtSale }]

POST /api/sales
     body: { eventId, cartItems: [{ itemId, salePriceAtSale, sellerPriceAtSale }], receiptDetails? }
     → 200 OK | 409 Conflict { conflictingItemId }
```

Everything else (seller creation, item creation, reconciliation) is handled by Razor page handlers calling the service layer directly — no JS involved.

## Print CSS

Two pages need deliberate print styling:

- `/reconciliation/{sellerId}` — seller sheet must be clean and readable when printed
- `/checkout` — receipt section is hidden on screen, visible only when printing

Both are handled with a `@media print` stylesheet. No JS required for printing the seller sheet. The checkout receipt uses `window.print()` triggered by the JS module after a successful sale with `receiptDetails` filled.
