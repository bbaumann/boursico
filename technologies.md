# Technologies Recap

## Purpose

This document records the agreed technology choices for the V1 of the association sale tool.

It consolidates the strongest parts of the previous architecture proposals, mainly:

- the pragmatic stack choice and concrete UI split from `amp-architecture.md`
- the resource-oriented backend API style and lightweight modular structure from `codex-architecture.md`

The goal is a stack that fits the actual project shape:

- self-hosted on Windows
- used on a local network
- fewer than 10 main pages
- no full authentication system in V1
- one checkout screen with meaningful interaction and UX demands

## Agreed Technology Choices

### Backend

- ASP.NET Core
- .NET 8 LTS
- Razor Pages for the main HTML UI
- EF Core
- code-first migrations

Why:

- strongest fit with the developer's expertise
- natural fit for Windows self-hosting
- one deployable application
- well suited to a mostly form- and workflow-driven internal tool
- gives the implementing agent a conventional and well-supported .NET stack

### Frontend

- server-rendered Razor Pages for almost the whole application
- vanilla JavaScript for the checkout page only
- plain CSS for styling

Why:

- the application is mostly page-oriented, not a rich client app
- only checkout has meaningful local browser state
- this keeps operational and maintenance complexity low
- this gives AI agents a narrow interactive frontend problem instead of a full SPA

### Hosting

- one self-hosted ASP.NET Core application on the local server
- Kestrel only in V1
- started locally as a simple executable on the LAN
- central server on the event LAN
- one central database as the single source of truth

### Database

- SQLite for V1
- one database file per event
- `WAL` mode enabled
- foreign keys enforced
- money stored as integer cents

Why:

- aligns with the project docs and V1 reliability model
- avoids unnecessary distributed or offline-first complexity
- keeps deployment and troubleshooting simple
- keeps machine-to-machine transfer much simpler than a separate database server
- avoids event-selection complexity in the UI

### Application Structure

- one web project
- internal folders for `Controllers`, `Pages`, `Domain`, and `Infrastructure`

Why:

- keeps the solution simple for a small internal tool
- avoids premature multi-project complexity
- still provides enough structure for the implementing agent

## Deliberately Not Chosen

### Full SPA Frameworks

Not chosen:

- Vue as the main frontend architecture
- Angular
- any full client-side SPA for the whole app

Why not:

- too much architecture for a very small internal tool
- introduces build, routing, and client-state complexity the project does not need
- most pages are better handled by normal server-rendered Razor views

### htmx / Alpine.js

Not chosen as primary technologies.

Why not:

- the main interactive problem is checkout, which is mostly a local state problem
- checkout needs search state, suggestion state, cart state, keyboard flow, and save-conflict handling
- that is cleaner in a focused JavaScript module than by mixing multiple lightweight tools

### Full Authentication Framework

Not chosen:

- ASP.NET Identity
- JWT-based auth
- user-account system

Why not:

- V1 does not require real user identity or role management
- the only protected behavior is a lightweight admin capability gate for sensitive actions

### Dapper Or Raw SQL As The Default Data Access Layer

Not chosen as the implementation default.

Why not:

- EF Core is the more conventional and agent-friendly baseline for this project
- code-first migrations give a clearer path for schema evolution during early development
- the app's scope does not justify optimizing for lower-level database access complexity up front

### PostgreSQL For V1

Not chosen as the V1 default.

Why not:

- it adds installation and operational complexity on Windows
- it makes pre-sale to sale-day server transfer more cumbersome
- the current expected concurrency does not justify the extra overhead

### Multi-Event Schema And UI In V1

Not chosen.

Why not:

- one event per database file is operationally clearer
- seller and item lists stay clean for the current event
- it avoids event-selection and event-scoping complexity throughout the app

## Frontend Architecture Decision

### General Rule

- Razor owns page structure, rendering, normal form posts, validation messages, and print views
- JavaScript is used only for page-specific interactivity where local UI state matters
- the server remains authoritative for business rules, validation, and persistence

### Pages That Stay Pure Razor

These pages should remain standard server-rendered pages:

- dashboard / home
- seller list
- seller create / edit / details
- item create / edit before sale start
- preregistration entry
- preregistration confirmation
- reconciliation overview
- seller reconciliation detail pages
- printable seller sheet pages
- printable receipt pages
- admin password entry page

These are forms, lists, print views, and operator workflows, not client-side applications.

### Checkout Page

The checkout page is the only page that should be treated as an interactive frontend island.

Razor still renders the page shell, but the checkout JavaScript module owns the live interaction.

## Checkout JavaScript Boundary

### Checkout JavaScript Owns

- current search query
- debounced item search requests
- cancelling stale requests
- current suggestion list
- highlighted search result
- keyboard navigation and selection behavior
- cart contents in memory
- running total for immediate feedback
- busy state while saving
- rendering of conflicts and network failures
- success reset after a saved sale
- redirect to or open the printable receipt after success

### Razor Checkout Page Owns

- the page shell
- labels and operator instructions
- search input container
- suggestion list container
- cart container
- total display
- receipt details input
- action buttons
- message and banner regions

### Server Owns

- item search rules
- item availability truth
- sale validation
- concurrency checks at save time
- item status transition to `sold`
- final persisted total
- receipt generation and print routes

## Backend API Style

The agreed backend style is:

- page-oriented HTML for the application UI
- resource-oriented endpoints for interactive operations

That means:

- Razor Pages remain the primary UI mechanism
- interactive JavaScript calls clean resource-oriented endpoints under `/api/...`
- the app should not become broadly API-first just because checkout uses resource endpoints

### Preferred Endpoint Shape

HTML pages:

- `/checkout`
- `/intake/...`
- `/reconciliation/...`
- `/print/receipt/{id}`
- `/print/seller-sheet/{sellerId}`

Interactive endpoints:

- `GET /api/items/search?q=BK-0`
- `POST /api/sales`
- optionally `POST /api/items/{id}/missing` if asynchronous reconciliation interaction is later useful

The initial API surface should stay narrow and driven by real UI needs.

## Recommended Project Structure

```text
src/
  AssociationSaleTool.Web/
    Program.cs
    appsettings.json

    Controllers/

    Pages/
      Index.cshtml
      Sellers/
      Items/
      Intake/
      Checkout/
      Reconciliation/
      Print/
      Admin/
      Shared/

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
        print.css
```

This is intentionally modest:

- Razor Pages for the UI
- focused API endpoints for interactive actions
- a single-project layout with internal folders
- a lightweight modular JS structure for checkout only

## Styling Direction

- plain CSS
- project-specific styles for checkout and print views
- high-contrast, legible operator-facing UI

Why:

- avoids adding unnecessary styling toolchains
- fits the small app scope
- supports deliberate sale-day UX work without pulling in a heavier frontend stack

## Password Gate Direction

Sensitive admin-only actions should use a lightweight in-app password gate.

Recommended approach:

- shared admin password stored in configuration
- short-lived session flag after successful unlock
- redirect protected pages to an admin unlock page when needed

This is an operational misuse-prevention mechanism, not a full security model.

## Core Principles

- Keep the app as one deployable ASP.NET Core application.
- Keep most pages boring, server-rendered, and easy to debug.
- Use JavaScript only where it clearly improves operator workflow.
- Keep checkout fast, keyboard-friendly, and explicit.
- Keep business rules and authoritative validation on the server.
- Keep the API surface narrow and resource-oriented where interactivity needs it.
- Avoid introducing frameworks and layers that the project does not need.

## Final Summary

The agreed technology direction for V1 is:

- ASP.NET Core
- .NET 8 LTS
- Kestrel only
- Razor Pages
- EF Core
- code-first migrations
- one web project with internal folders
- vanilla JavaScript for checkout only
- plain CSS
- SQLite
- one database file per event
- `WAL` mode
- integer-cent money storage
- resource-oriented `/api/...` endpoints for interactive operations
- self-hosted Windows deployment on the local network
- lightweight in-app password gate for sensitive admin actions

This is the best fit for the project's scope, the operational constraints, and the developer's preferred backend style.
