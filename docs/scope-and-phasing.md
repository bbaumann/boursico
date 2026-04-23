# Scope And Phasing

## V1 Scope

V1 is deliberately narrow.

### Included In V1

- seller creation
- manual item entry by organizers
- operator-managed preregistration from seller emails
- in-person registration when toys are physically present
- preregistration confirmation against physically delivered toys
- one item record per toy
- seller-aware reference generation
- sale-day lookup by live-filtered `reference + title` table
- cart and total computation
- sale persistence
- optional receipt printing
- post-sale seller sheet generation
- marking an unsold item as missing
- regenerating seller sheets after missing-item corrections
- paper fallback process

## Explicitly Out Of Scope For V1

- seller self-service portal
- spreadsheet import as a required workflow
- barcode scanner dependency
- payment method tracking
- accounting integration
- seller fee automation for the EUR 2 per 15 items rule
- offline client logs and replay
- multi-station sync conflicts
- generic inventory or ERP breadth

## Stretch Goals

These are known ideas but not part of the current V1 commitment:

- seller pre-registration portal
- more organizer-friendly reconciliation UI
- automated formal statement generation beyond the seller sheet workflow
- broader import tooling

## Why V1 Is Intentionally Small

- the event is yearly, not a daily retail business
- the sale-day UI must be highly reliable for non-technical volunteers
- the biggest historical pain is consolidation, not checkout complexity
- a smaller first release reduces operational risk before the next event

## Likely Evolution Path

### Phase 1

Get event operations stable and get reliable authoritative data into one system.

### Phase 2

Strengthen reconciliation ergonomics and automate more of the seller-document generation.

### Phase 3

Optionally add seller pre-registration if it becomes worth the extra workflow complexity.

This phase refers to seller self-service preregistration, not operator-managed preregistration, which is already part of V1.

## Success Criteria For V1

- volunteers can run checkout with minimal training
- digital sales no longer require multi-file end-of-day consolidation
- post-sale seller sheets are reproducible and correct
- missing-item handling is supported without manual document rewriting
- paper fallback is documented and operationally clear
