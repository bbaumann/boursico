# Codex Architecture Note

## Purpose

This note consolidates the current technical direction discussed for the project architecture and frontend stack.

It is intentionally pragmatic and optimized for:

- self-hosting on a Windows server in a local network
- low operational complexity
- strong fit with ASP.NET Core
- frontend implementation that an AI coding agent can handle reliably

## Recommended Stack

- Backend: ASP.NET Core
- Main UI model: Razor Pages
- Frontend interactivity: vanilla JavaScript modules
- Styling: plain CSS, with project-specific styling for checkout and other operator-facing screens
- Database: server-hosted relational database as already described in the project docs

## Why This Stack

The application is mostly a workflow-heavy internal tool, not a broad consumer web app.

Most screens are naturally page-oriented:

- intake
- preregistration confirmation
- reconciliation
- admin-password-gated actions
- printable outputs

The only screen that clearly benefits from richer client-side behavior is checkout.

Because of that, the recommended approach is:

- keep most of the application server-rendered
- use JavaScript only where local UI state is genuinely needed
- avoid introducing a full SPA frontend unless future scope clearly justifies it

## Frontend Decision

Default choice:

- Razor Pages + vanilla JavaScript

Not recommended as the default frontend model:

- htmx as a primary interaction model
- Alpine.js as the main way to structure checkout behavior
- Angular or another full SPA framework for the whole application

### Why Not htmx As The Default

htmx is useful for server-rendered partial updates, but the most complex UI in this project is checkout, and checkout is mainly a local state problem:

- current search term
- suggestion list
- selected item
- cart lines
- running total
- keyboard-first flow
- conflict handling on save

That is easier to manage in a focused JavaScript module than through repeated HTML swaps.

### Why Not Alpine.js As The Default

Alpine.js is acceptable for very small interactions, but checkout is likely too stateful to keep clean in attribute-driven markup.

Vanilla JavaScript modules give clearer boundaries once the page logic becomes more than a few toggles and bindings.

## UI Split

### Pure Razor Pages

These screens should stay server-rendered with standard form posts and page loads:

- seller intake
- preregistration confirmation
- item create/edit before sale start
- reconciliation screens
- admin-password entry flows where simple forms are enough
- printable receipt page
- printable seller sheet page

### Razor Page With JavaScript

Checkout should be the main interactive page:

- server renders the page shell
- JavaScript manages interactive client-side behavior
- server validates and persists business actions

## Checkout Responsibilities

### Razor Page Owns

- initial HTML shell
- labels, layout, operator guidance
- initial configuration values
- fallback-friendly rendering

### JavaScript Owns

- search input behavior
- autocomplete rendering
- item selection
- cart state in memory
- total calculation in the browser
- keyboard navigation and focus flow
- save request handling
- conflict feedback when an item is no longer available

### Server Owns

- item lookup rules
- sale validation
- concurrency checks at save time
- sale persistence
- receipt generation and printable output

## Backend HTTP Shape

HTML remains page-oriented, but interactive backend operations should use resource-oriented endpoints rather than page-organized handlers.

### HTML Pages

- `/sales/checkout`
- `/intake/...`
- `/reconciliation/...`
- `/print/receipt/{id}`
- `/print/seller-sheet/{sellerId}`

### Resource-Oriented Endpoints

- `GET /api/items/search?q=BK-0`
- `POST /api/sales`
- `GET /api/sales/{id}`
- optionally `POST /api/items/{id}/missing`

This keeps the UI page-oriented while making the backend interaction model cleaner and more resource-focused.

## Endpoint Guidance

Keep the API surface narrow and tied to actual UI needs.

Do not build a broad generic REST API up front.

Recommended initial endpoints:

- `GET /api/items/search`
  - checkout-safe item summaries only
- `POST /api/sales`
  - cart lines
  - optional `receipt_details`
  - server-side availability revalidation
- `GET /api/sales/{id}`
  - optional detail readback if needed after save

Optional later endpoint:

- `POST /api/items/{id}/missing`
  - only if the reconciliation UI benefits from async interaction
  - otherwise a normal Razor form post is still preferred

## Suggested Code Structure

```text
Pages/
  Intake/
  Sales/
    Checkout.cshtml
    Checkout.cshtml.cs
  Reconciliation/
  Print/
    Receipt.cshtml
    SellerSheet.cshtml
  Admin/

Api/
  Items/
  Sales/
  Reconciliation/

Application/
  Items/
    SearchItems/
  Sales/
    CreateSale/
  Reconciliation/
    MarkMissing/

Domain/

Infrastructure/

wwwroot/
  js/
    checkout/
      checkout-page.js
      search-service.js
      cart-store.js
      keyboard.js
      save-sale.js
  css/
    site.css
    checkout.css
```

## JavaScript Structure For Checkout

Checkout JavaScript should stay modular but lightweight.

Suggested split:

- `checkout-page.js`
  - page bootstrap and wiring
- `search-service.js`
  - API calls for item search
- `cart-store.js`
  - in-memory cart state and total calculation
- `keyboard.js`
  - focus handling and shortcuts
- `save-sale.js`
  - sale submission and conflict handling

This is intentionally not a frontend framework architecture.

It is just enough structure to keep the interactive page understandable and maintainable.

## Key Principles

- Keep business rules on the server.
- Keep most pages simple and server-rendered.
- Use JavaScript only where local UI state materially improves operator flow.
- Keep checkout fast, keyboard-friendly, and explicit.
- Prefer a small number of focused resource endpoints over a broad API.
- Avoid introducing a second large architectural model unless the product later grows enough to justify it.

## Final Recommendation

The recommended architecture is:

- ASP.NET Core application
- Razor Pages for the overall UI
- vanilla JavaScript modules for checkout
- resource-oriented API endpoints for interactive operations

This gives a good balance between:

- production simplicity
- fit with the project's operational constraints
- fit with the developer's .NET background
- implementation friendliness for AI coding agents
