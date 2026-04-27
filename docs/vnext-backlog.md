# VNext Backlog

This backlog captures known feature requests that are intentionally outside the current V1 commitment.

## Item Intake

### Create Items From An Excel File

Allow operators to import item rows from an Excel workbook instead of entering every row manually.

The future design should define:

- the accepted workbook format
- validation and error reporting before import
- how imported rows map to seller, title, price, and intake mode
- whether duplicate detection is required
- whether import is only an operator helper or a required workflow

## Seller Fee Management

### Implement Seller Fee Management And Tracking

Track seller-facing fees inside the application instead of handling them outside the system.

The future design should define:

- the seller fee rule, including the current EUR 2 per 15 items rule
- how fees are calculated, rounded, adjusted, and displayed
- whether fees are collected at intake, deducted from payout, or only reported
- audit expectations for fee payment or waiver tracking
- how seller sheets and reconciliation outputs include fee information
