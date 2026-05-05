# Association Toy Sale Tool

## Purpose

This documentation consolidates the current project understanding for:

- the primary developer(s)
- any additional developers joining later
- future agentic AI sessions that need project context

The project is a yearly event tool for an association that sells toys on behalf of sellers.

The first version is intentionally narrow:

- make intake manageable
- make sale-day operations simple and safe for non-technical volunteers
- remove the historical end-of-day Excel/VBA consolidation pain
- keep post-sale reconciliation separated from sale-day checkout while keeping it inside the same app

## Canonical Source

The English documentation in `docs/` is the canonical source of truth for implementation.

Archived or translated variants may exist elsewhere in the repository, but agent-driven development should follow the documents in `docs/` unless a newer canonical note says otherwise.

## Document Map

- [Features](./features.md)
- [Guide utilisateur simplifié](./guide-utilisateur.md)
- [Architecture](./architecture.md)
- [Checkout Contract](./checkout-contract.md)
- [Database Choice](./database-choice.md)
- [Database Schema](./database-schema.md)
- [Physical SQL DDL](./sql/README.md)
- [Use Cases And Personas](./use-cases-and-personas.md)
- [Decisions](./decisions.md)
- [Process And Failover](./process-and-failover.md)
- [Domain Model And References](./domain-model-and-references.md)
- [Item Status Lifecycle](./item-status-lifecycle.md)
- [Sale Phases And Sale Corrections](./sale-phases-and-corrections.md)
- [Scope And Phasing](./scope-and-phasing.md)
- [VNext Backlog](./vnext-backlog.md)

## Project Summary

The tool contains two independent in-app features over one shared database:

1. Event operations
2. Post-sale reconciliation

Event operations are optimized for elderly, non-technical volunteers during the sale day.

Post-sale reconciliation is optimized for correctness and auditability. It is an admin-gated in-app dashboard at `/rapprochement`, with printable seller sheets at `/rapprochement/feuilles`. Before the event phase is `post_sale`, the dashboard can be viewed but reconciliation operations remain read-only.

## Key Principles

- One physical toy maps to one item record.
- One central database is the only digital source of truth.
- Sale-day UX must feel like an appliance, not like an admin application.
- The sale phase is visible globally and enforced server-side.
- Reconciliation happens after sales are over and is treated as a separate in-app feature.
- Simplicity is preferred over cleverness.

## Current Project State

V1 is complete. The software is now in VNext: V1 remains the baseline event workflow, and VNext features are being designed, implemented, and verified incrementally.

## Current Orientation

- Custom tool, not Odoo-based
- Local-network deployment
- Central server only, no offline sync between stations
- Paper fallback if the server is unavailable
- Seller self-service remains out of scope unless a future VNext decision changes it
- `Admin:SharedPassword` must be configured for production; blank values and `change-me` are invalid.

## Suggested Extra Reading Order

If someone is new to the project, the best reading order is:

1. [Scope And Phasing](./scope-and-phasing.md)
2. [Features](./features.md)
3. [Checkout Contract](./checkout-contract.md)
4. [Domain Model And References](./domain-model-and-references.md)
5. [Database Choice](./database-choice.md)
6. [Database Schema](./database-schema.md)
7. [Physical SQL DDL](./sql/README.md)
8. [Architecture](./architecture.md)
9. [Process And Failover](./process-and-failover.md)
10. [Item Status Lifecycle](./item-status-lifecycle.md)
11. [Sale Phases And Sale Corrections](./sale-phases-and-corrections.md)
12. [Decisions](./decisions.md)
13. [VNext Backlog](./vnext-backlog.md)
