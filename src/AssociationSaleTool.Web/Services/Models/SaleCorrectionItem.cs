using AssociationSaleTool.Web.Domain.Pricing;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record SaleCorrectionItem(
    long SaleLineId,
    long SaleId,
    long ItemId,
    string Reference,
    string Title,
    string SellerCode,
    string SellerName,
    int SellerPriceAtSaleCents,
    DateTime SoldAt)
{
    public int RefundCheckoutFeeCents =>
        CheckoutPricingCalculator.CalculateTotals(SellerPriceAtSaleCents).CheckoutFeeCents;

    public int RefundTotalCents => SellerPriceAtSaleCents + RefundCheckoutFeeCents;
}
