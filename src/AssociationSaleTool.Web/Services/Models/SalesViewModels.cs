namespace AssociationSaleTool.Web.Services.Models;

public sealed record SalesListItem(
    long SaleId,
    DateTime SoldAt,
    int ItemCount,
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents,
    bool HasReceiptDetails,
    string SearchText);

public sealed record SaleDetail(
    long SaleId,
    DateTime SoldAt,
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents,
    string? ReceiptDetails,
    IReadOnlyList<SaleDetailLine> Lines);

public sealed record SaleDetailLine(
    long SaleLineId,
    string Reference,
    string Title,
    int SellerPriceAtSaleCents,
    DateTime? CorrectedAt)
{
    public bool IsCorrected => CorrectedAt is not null;
}
