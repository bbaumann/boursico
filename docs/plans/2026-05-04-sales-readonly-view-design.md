# Read-Only Sales View Design

## Goal

Allow every checkout operator to inspect saved sales from the checkout area without changing sale creation, correction, or admin phase workflows.

## Accepted Shape

- `/caisse` remains the default checkout creation page.
- The checkout header exposes a prominent `LISTE DES VENTES` action.
- `/caisse/ventes` lists saved sales, newest first, in read-only mode.
- `/caisse/ventes/{id}` shows one saved sale in read-only detail mode.
- The feature is visible to all operators and is not guarded by the shared admin password.

## List Behavior

The list shows sale id, sale timestamp, item count, subtotal, checkout fee, total paid, and receipt presence. A local search field filters sales by:

- sale id
- item reference
- item title
- buyer address

The first version can load the displayed sale set server-side and filter it client-side, matching the existing checkout and reconciliation table style. Newest sales appear first.

## Detail Behavior

The detail page uses the checkout/cart mental model but has no editable controls, remove actions, save action, print action, or correction action.

It shows:

- sale id
- sold timestamp
- buyer address when present
- item references
- item titles
- seller price at sale time
- subtotal
- checkout fee
- total paid

Corrected sale lines remain visible as historical sale facts and are labeled `Corrigée`. Sale totals are historical and are not recalculated after corrections.

## Boundaries

Sale-line corrections stay on `/administration`. The read-only sales view must not add admin-only actions or mutate sale, item, or correction state.

## Testing

Service tests should cover newest-first ordering, search text content, corrected-line labeling data, and missing sale lookup. Page smoke tests should verify the French routes render for all operators without admin unlock.
