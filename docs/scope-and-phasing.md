# Scope And Phasing

## Current V1 State

V1 is feature-complete but not live yet. It includes the original MVP baseline plus the operational hardening and organizer-facing workflows added before first live use.

### Included In V1

- seller creation and duplicate seller prevention
- manual item entry by organizers
- Excel-assisted seller and item prefill for one reviewed seller workbook
- operator-managed preregistration from seller emails
- in-person seller and item entry as `registered` draft data
- seller-level signed deposit validation and contract printing
- seller deposit cancellation during `presale` with local admin-password confirmation, reopening only current stock items
- one item record per toy
- seller-aware reference generation
- seller registration fee tracking and reconciliation deduction
- event sale phases with server-side enforcement
- sale-day lookup by live-filtered `reference + title` table
- required buyer name or organization, postal code, and city on checkout
- sale-level payment method tracking with cheque traceability
- cart, checkout fee, total computation, and sale persistence
- optional receipt printing from one `Adresse` field
- read-only sales history from the checkout area
- read-only operational data browser at `/donnees`
- post-sale reconciliation dashboard and seller sheet generation
- marking an unsold item as missing and restoring it when corrected
- marking accepted items as defective
- refunding defective sold items, including the 10 percent association fee
- regenerating seller sheets after correction workflows
- paper fallback process

## MVP Baseline

The former narrow "V1" scope is now called MVP. It covered the smallest useful event workflow:

- seller and item entry
- item reference generation
- checkout lookup and cart sale save
- optional receipt printing
- seller sheet generation
- missing-item handling during reconciliation
- paper fallback

The MVP terminology is historical. The software is now beyond MVP and should be described as current V1.

## Explicitly Out Of Scope For V1

- seller self-service portal
- spreadsheet import as a required workflow or direct persistence path
- barcode scanner dependency
- integrated card-payment terminal flows
- accounting integration
- offline client logs and replay
- multi-station sync conflicts
- generic inventory or ERP breadth
- individual user accounts and strong per-user audit logs

## VNext Candidates

These are known ideas beyond the feature-complete V1:

- seller self-service preregistration portal
- automated formal statement generation beyond the seller sheet workflow
- broader import/export tooling beyond the current CSV/TSV seller-form import
- richer accounting and refund ledgers if the association later needs them
- optional multi-event history or archive workflows
- stronger user identity and audit logging

Additional post-V1 candidates are tracked in [VNext Backlog](./vnext-backlog.md).

## Why V1 Stays Focused

- the event is yearly, not a daily retail business
- the sale-day UI must be highly reliable for non-technical volunteers
- the biggest historical pain is consolidation, not checkout complexity
- a focused first live release reduces operational risk

## Likely Evolution Path

### Phase 1

Use the feature-complete V1 for the first live event and verify the full flow with real operators.

### Phase 2

Fix any operational gaps found during first live use, especially around training, printing, reconciliation, and data inspection.

### Phase 3

Optionally add seller self-service, broader import/export, richer accounting, or stronger identity features if they become worth the extra workflow complexity.

## Success Criteria For V1

- volunteers can run checkout with minimal training
- digital sales no longer require multi-file end-of-day consolidation
- post-sale seller sheets are reproducible and correct
- missing and defective item handling is supported without manual document rewriting
- cheque and simple payment traceability are available for association follow-up
- paper fallback is documented and operationally clear
