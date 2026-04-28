namespace AssociationSaleTool.Web.Services.Models;

public sealed record CheckoutValidationResult(
    bool IsValid,
    IReadOnlyList<long> ConflictItemIds,
    CheckoutCartTotals? Totals,
    int ItemCount);
