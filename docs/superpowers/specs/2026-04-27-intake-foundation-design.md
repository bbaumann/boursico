# Intake Foundation Design

## Scope

This design covers the first real event-operations slice after the application foundation:

- seller creation and listing
- seller detail view
- seller-centric multi-item intake
- support for both intake modes:
  - email preregistration
  - in-person intake
- pre-sale correction of seller and item master data

This slice does not yet include preregistered-delivery confirmation, checkout, or reconciliation.

## Goals

- Make the intake operator productive on real seller lists
- Keep the workflow centered on one seller at a time
- Persist all documented seller and item rules in application code
- Create authoritative data that later checkout and reconciliation flows can rely on

## Non-Goals

- `registered -> in_stock` confirmation workflow and deletion of undelivered preregistered items
- bulk import
- sale-start freeze controls
- checkout lookup, cart, or sales save
- reconciliation pages and seller sheets

## Supported Use Cases

This slice implements:

- `UC-01 Create Seller`
- `UC-02 Create Item`
- `UC-02A Record Email Preregistration`
- `UC-02B Register Seller In Person`

It intentionally leaves `UC-02C Confirm Preregistered Delivery` for the next intake-related slice.

## UX Model

The intake workflow is seller-centric rather than item-centric.

The operator-facing implementation for this workflow must use French UI labels and French user-facing URLs.

The operator first creates or opens a seller, then works on that seller’s items from a single seller detail page. The page includes:

- seller identity summary
- current items for that seller
- one multi-row item entry form
- an intake-mode choice that sets initial item status for newly added rows

This matches the real task better than forcing the operator through one item-create page per toy.

## Item Entry Screen

The seller detail page is the main intake work surface.

It must support:

- adding multiple items in one submission
- entering item title and seller price per row
- persisting new rows with the chosen intake mode
- keeping existing items visible below or beside the entry form

The initial version does not need spreadsheet-like client behavior. A simple server-rendered multi-row form is enough if it is clear and efficient.

## Intake Modes

The page supports two creation modes for new rows:

- `Email preregistration`
  - newly created items start as `registered`
- `In-person intake`
  - newly created items start as `in_stock`

The mode applies to the submitted batch of new rows. Existing items are not rewritten by changing the mode selector.

## Seller Rules

- creating a seller assigns the next seller code in the documented spread order
- seller code remains stable after creation
- seller identity fields can still be corrected before sale start

For this slice, seller editing is still allowed because the sale-start freeze is not yet being enforced as an application phase.

## Item Rules

- each submitted row creates exactly one item
- each item belongs to one seller
- item numbers are allocated per seller and increment monotonically
- item references are stored in final form as `SELLERCODE-NNN`
- seller price is entered by the operator
- seller price is also the printed per-item customer price in V1
- checkout fee logic is not part of intake
- initial item status is derived from intake mode

Pre-sale correction is allowed in this slice for:

- title
- seller price

The reference, seller link, and item number should be treated as stable once assigned.

## Application Structure

The slice will extend the current structure rather than introducing new projects:

- `Services/IntakeService.cs` will own seller creation, batch item creation, and update workflows
- `Pages/Sellers/` will contain seller list, create, and detail/edit pages
- focused page models will delegate to the intake service

The service must be the place where seller code allocation, item number allocation, and status derivation are enforced so those rules do not leak into page handlers.

## Data Flow

1. Operator creates a seller.
2. The system allocates the next seller code.
3. Operator opens the seller page.
4. Operator chooses intake mode and enters multiple item rows.
5. The service validates rows, allocates seller-local item numbers, computes references, and inserts the items.
6. The page reloads with the newly created items visible.

## Validation

The slice should reject:

- blank seller first or last name
- blank item title
- negative seller prices
- empty item batches where every row is blank

The UI should tolerate partially blank entry rows by ignoring fully empty rows but rejecting half-filled rows with clear validation messages.

## Testing Strategy

Automated coverage for this slice should focus on:

- seller creation assigns the expected next code
- item batch creation allocates sequential seller-local item numbers
- item batch creation persists correct references
- intake mode maps to the correct initial status
- pages load and post successfully for the basic happy paths

## Milestone Definition

The intake foundation slice is complete when:

- a seller can be created through the web UI
- sellers can be listed and opened
- multiple items can be added for one seller in a single submission
- both preregistration and in-person intake modes work
- stored items show correct reference, numbering, seller price, and status
- pre-sale seller and item correction is possible through the UI
