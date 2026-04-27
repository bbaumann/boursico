namespace AssociationSaleTool.Web.Domain.Pricing;

public readonly record struct CheckoutTotals(
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents);
