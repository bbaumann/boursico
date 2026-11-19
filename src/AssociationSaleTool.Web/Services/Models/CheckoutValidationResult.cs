namespace AssociationSaleTool.Web.Services.Models;

// Items carries the current reference, title and price of each cart item that is still
// available, so the checkout can correct a cart built from an outdated catalog.
public sealed record CheckoutValidationResult(
    bool IsValid,
    IReadOnlyList<long> ConflictItemIds,
    CheckoutCartTotals? Totals,
    int ItemCount,
    string? Message = null,
    IReadOnlyList<CheckoutCatalogItem>? Items = null);
