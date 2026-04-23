# Amp Architecture Note

## Purpose

This note consolidates the current technical direction agreed for the project.

It reflects the decisions made for a V1 that is:

- self-hosted on a Windows machine
- used on a local network
- intentionally small and operationally simple
- implemented primarily with ASP.NET Core and server-rendered pages

## Agreed Stack

- Backend: ASP.NET Core
- Main UI model: Razor Pages
- Frontend interactivity: vanilla JavaScript
- Styling: plain CSS
- Hosting model: one self-hosted ASP.NET Core web application on the local server

## Why This Stack Fits

The application is small and page-oriented.

Most screens are standard internal-tool screens:

- forms
- tables
- confirmation workflows
- print-friendly pages

The main interaction-heavy screen is checkout.

Because of that, the best fit is:

- keep most of the application server-rendered
- use JavaScript only where local UI state materially improves the operator workflow
- avoid introducing a full SPA frontend

This fits both the project constraints and the developer's strongest expertise in .NET.

## Frontend Architecture

### General Rule

- Razor owns page structure, rendering, validation messages, and normal form flows
- vanilla JavaScript is used only for page-specific interactive behavior
- the server remains authoritative for business rules and persistence

### Pages That Stay Pure Razor

These pages should stay server-rendered with standard `GET` and `POST` flows:

- dashboard / home
- seller list
- seller create / edit / details
- item create / edit before sale start
- preregistration entry
- preregistration confirmation
- reconciliation overview
- seller reconciliation detail pages
- seller sheet print pages
- receipt print pages
- admin password entry page

These screens are mostly workflow and data-entry screens, not client-side applications.

### Page That Uses JavaScript Heavily

The checkout page is the only screen that should be treated as a small interactive island.

Razor still renders the page shell, but the page-specific JavaScript module owns the live interaction.

## Checkout Module Boundary

The checkout JavaScript should live in a single page-specific module and own only transient browser-side state.

### Checkout JavaScript Owns

- current search query
- debounced search requests
- cancelling stale requests
- current suggestion list
- currently highlighted suggestion
- keyboard navigation in the search results
- cart contents in memory
- client-side running total for responsiveness
- busy state while saving
- rendering of availability conflicts and network errors
- success reset after a saved sale
- redirect to or open the printable receipt page after success

### Razor Checkout Page Owns

- the page shell
- labels and operator instructions
- the search field container
- the results container
- the cart container
- total display region
- receipt details input
- action buttons
- message / banner regions

### Server Owns

- item search rules
- item availability truth
- sale validation
- concurrency checks at save time
- status transitions to `sold`
- final persisted totals
- receipt generation and print views

## Admin Protection Approach

The project does not need full authentication in V1.

For sensitive screens or actions, such as post-sale missing-item handling, use a lightweight in-app password gate:

- protected page checks for an admin-unlocked session flag
- if missing, redirect to an admin unlock page
- user enters the shared admin password
- app sets a short-lived session flag
- user is redirected back to the requested page

This matches the project's current operational requirements better than introducing a full identity system.

## Concrete Project Layout Sketch

```text
src/
  AssociationSaleTool.Web/
    Program.cs
    appsettings.json

    Pages/
      Index.cshtml
      Index.cshtml.cs

      Sellers/
        Index.cshtml
        Index.cshtml.cs
        Create.cshtml
        Create.cshtml.cs
        Edit.cshtml
        Edit.cshtml.cs
        Details.cshtml
        Details.cshtml.cs

      Items/
        Create.cshtml
        Create.cshtml.cs
        Edit.cshtml
        Edit.cshtml.cs

      Intake/
        Preregistrations.cshtml
        Preregistrations.cshtml.cs
        ConfirmDelivery.cshtml
        ConfirmDelivery.cshtml.cs

      Checkout/
        Index.cshtml
        Index.cshtml.cs
        Receipt.cshtml
        Receipt.cshtml.cs

      Reconciliation/
        Index.cshtml
        Index.cshtml.cs
        SellerDetail.cshtml
        SellerDetail.cshtml.cs
        SellerSheet.cshtml
        SellerSheet.cshtml.cs

      Admin/
        Unlock.cshtml
        Unlock.cshtml.cs

      Shared/
        _Layout.cshtml
        _StatusBanner.cshtml
        _SellerForm.cshtml
        _ItemForm.cshtml

    Endpoints/
      CheckoutEndpoints.cs

    Services/
      CheckoutService.cs
      IntakeService.cs
      ReconciliationService.cs
      SellerCodeService.cs
      ReceiptService.cs
      AdminGateService.cs

    Domain/
      Seller.cs
      Item.cs
      Sale.cs
      SaleLine.cs
      ItemStatus.cs

    Infrastructure/
      AppDbContext.cs

    wwwroot/
      css/
        site.css
        checkout.css
        print.css
      js/
        checkout-page.js
```

## Route Sketch

### Regular Razor Pages

- `GET /`
- `GET /sellers`
- `GET /sellers/create`
- `POST /sellers/create`
- `GET /sellers/{id}`
- `GET /sellers/{id}/edit`
- `POST /sellers/{id}/edit`

- `GET /sellers/{sellerId}/items/create`
- `POST /sellers/{sellerId}/items/create`
- `GET /items/{id}/edit`
- `POST /items/{id}/edit`

- `GET /intake/preregistrations`
- `GET /intake/preregistrations/{sellerId}/confirm`
- `POST /intake/preregistrations/{sellerId}/confirm`

- `GET /checkout`
- `GET /checkout/receipt/{saleId}`

- `GET /reconciliation`
- `GET /reconciliation/sellers/{sellerId}`
- `GET /reconciliation/sellers/{sellerId}/sheet`

- `GET /admin/unlock`
- `POST /admin/unlock`

### Checkout JSON Endpoints

The checkout JavaScript should talk to a very small backend surface:

- `GET /checkout-api/search?query=BK-0`
- `POST /checkout-api/sales`

That is enough for the only interaction-heavy page in the system.

## Suggested Checkout Endpoint Contracts

### Search Response

```json
[
  {
    "id": 123,
    "reference": "BK-014",
    "title": "Lego fire truck",
    "salePrice": 12.0
  }
]
```

### Save Sale Request

```json
{
  "itemIds": [123, 456, 789],
  "receiptDetails": "John Doe - reimbursement"
}
```

### Save Sale Success Response

```json
{
  "saleId": 42,
  "totalAmount": 31.2,
  "receiptUrl": "/checkout/receipt/42"
}
```

### Save Sale Conflict Response

```json
{
  "code": "item_unavailable",
  "message": "One or more items are no longer available.",
  "conflictItemIds": [456]
}
```

## Key Principles

- Keep the app as one deployable ASP.NET Core application.
- Keep most pages simple and server-rendered.
- Use JavaScript only where it clearly improves the operator experience.
- Keep checkout fast, explicit, and keyboard-friendly.
- Keep business rules and authoritative validation on the server.
- Avoid frontend framework complexity unless future scope proves it is necessary.

## Final Direction

The agreed frontend and application architecture is:

- ASP.NET Core web application
- Razor Pages for the overall UI
- vanilla JavaScript only for the checkout page interactivity
- simple, narrow JSON endpoints for checkout search and sale save
- print-friendly Razor pages for receipts and seller sheets
- lightweight in-app password gate for protected admin actions

This gives a strong fit with the project's simplicity, operational needs, and maintenance model.
