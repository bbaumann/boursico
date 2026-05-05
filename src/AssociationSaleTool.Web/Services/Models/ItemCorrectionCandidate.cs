using AssociationSaleTool.Web.Domain.Pricing;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record ItemCorrectionCandidate(
    long ItemId,
    long? SaleLineId,
    long? RefundedSaleLineId,
    long? SaleId,
    DateTime? SoldAt,
    string Reference,
    string Title,
    string SellerCode,
    string SellerName,
    string Status,
    int SellerPriceCents,
    int? SellerPriceAtSaleCents,
    bool HasRefundHistory)
{
    public bool CanCorrectSaleLine => IsActiveSoldItem;

    public bool CanRefundDefective => IsActiveSoldItem;

    public bool CanMarkDefective => Status == "in_stock";

    public bool CanRestoreInStock => Status == "defective" && !HasRefundHistory;

    public bool CanCancelRefund => Status == "defective" && RefundedSaleLineId is not null;

    public int DisplayPriceCents => SellerPriceAtSaleCents ?? SellerPriceCents;

    public int RefundCheckoutFeeCents =>
        SellerPriceAtSaleCents is { } priceCents
            ? CheckoutPricingCalculator.CalculateTotals(priceCents).CheckoutFeeCents
            : 0;

    public int RefundTotalCents => SellerPriceAtSaleCents.GetValueOrDefault() + RefundCheckoutFeeCents;

    private bool IsActiveSoldItem => Status == "sold" && SaleLineId is not null;
}
