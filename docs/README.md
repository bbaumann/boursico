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
- keep post-sale reconciliation independent from sale-day operations

## Canonical Source

The English documentation in `docs/` is the canonical source of truth for implementation.

Archived or translated variants may exist elsewhere in the repository, but agent-driven development should follow the documents in `docs/` unless a newer canonical note says otherwise.

## Document Map

- [Features](./features.md)
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
- [Scope And Phasing](./scope-and-phasing.md)

## Project Summary

The tool is split into two independent features over one shared database:

1. Event operations
2. Post-sale reconciliation

Event operations are optimized for elderly, non-technical volunteers during the sale day.

Post-sale reconciliation is optimized for correctness and auditability, and can initially be implemented as a separate technical tool or script rather than as a full UI.

## Key Principles

- One physical toy maps to one item record.
- One central database is the only digital source of truth.
- Sale-day UX must feel like an appliance, not like an admin application.
- Reconciliation happens after sales are over and can be treated as a separate feature.
- Simplicity is preferred over cleverness in V1.

## Current V1 Orientation

- Custom tool, not Odoo-based
- Local-network deployment
- Central server only, no offline sync between stations
- Paper fallback if the server is unavailable
- Seller self-service is out of scope for V1

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
11. [Decisions](./decisions.md)
