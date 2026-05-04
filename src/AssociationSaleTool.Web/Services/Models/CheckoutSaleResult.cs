namespace AssociationSaleTool.Web.Services.Models;

public sealed record CheckoutSaleResult(
    bool Saved,
    long? SaleId,
    IReadOnlyList<long> ConflictItemIds,
    CheckoutCartTotals? Totals,
    int ItemCount,
    string? Message = null);
