# Main Navigation Design

## Goal

Add a simple, persistent top navigation menu so laptop users can move between the two current operator sections without knowing URLs or relying on browser history.

## Scope

This includes:

- a shared top menu rendered on every normal page
- two large French buttons: `VENDEURS` and `CAISSE`
- links to `/vendeurs` and `/caisse`
- a visible active state for the current section
- laptop-focused layout only

This does not include:

- mobile-specific navigation behavior
- new sections beyond sellers and checkout
- icons, dropdowns, breadcrumbs, or side navigation

## Design

The menu belongs in `Pages/Shared/_Layout.cshtml`, above the page content. It should be visually stronger than small text links and use the existing button language so it feels like an obvious action area for non-technical operators.

The active state is computed from the current request path:

- `/vendeurs`, `/vendeurs/...`, and `/articles/...` highlight `VENDEURS`
- `/caisse` highlights `CAISSE`

The menu uses uppercase labels exactly as approved: `VENDEURS` and `CAISSE`.

## Testing

Smoke tests should verify that:

- seller pages render both menu links
- checkout renders both menu links
- seller pages mark `VENDEURS` active
- checkout marks `CAISSE` active
- the stylesheet contains the main navigation classes
