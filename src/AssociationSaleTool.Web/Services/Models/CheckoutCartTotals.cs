namespace AssociationSaleTool.Web.Services.Models;

public sealed record CheckoutCartTotals(
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents);
