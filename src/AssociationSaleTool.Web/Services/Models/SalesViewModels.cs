namespace AssociationSaleTool.Web.Services.Models;

public sealed record SalesListItem(
    long SaleId,
    DateTime SoldAt,
    int ItemCount,
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents,
    string? BuyerName,
    string? BuyerPostalCode,
    string? BuyerCity,
    bool HasInvoice,
    string PaymentMethodLabel,
    string SearchText)
{
    public bool HasBuyerDetails =>
        !string.IsNullOrWhiteSpace(BuyerName)
        || !string.IsNullOrWhiteSpace(BuyerPostalCode)
        || !string.IsNullOrWhiteSpace(BuyerCity);

    public string BuyerDisplay => SaleBuyerDisplay.Format(BuyerName, BuyerPostalCode, BuyerCity);
}

public sealed record SaleDetail(
    long SaleId,
    DateTime SoldAt,
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents,
    string? BuyerName,
    string? BuyerPostalCode,
    string? BuyerCity,
    string? BuyerAddress,
    string? InvoiceNumber,
    DateTime? InvoiceIssuedAt,
    string PaymentMethodLabel,
    string? ChequeNumber,
    string? ChequeBankName,
    IReadOnlyList<SaleDetailLine> Lines)
{
    public bool HasBuyerDetails =>
        !string.IsNullOrWhiteSpace(BuyerName)
        || !string.IsNullOrWhiteSpace(BuyerPostalCode)
        || !string.IsNullOrWhiteSpace(BuyerCity);

    public int RefundTotalCents => Lines.Sum(line => line.RefundTotalCents ?? 0);

    public bool HasRefunds => Lines.Any(line => line.IsRefunded);

    public int CorrectedTotalPaidCents => Math.Max(0, TotalPaidCents - RefundTotalCents);

    public string BuyerDisplay => SaleBuyerDisplay.Format(BuyerName, BuyerPostalCode, BuyerCity);

    public bool CanPrintInvoice => !string.IsNullOrWhiteSpace(BuyerAddress);

    public bool HasInvoice => !string.IsNullOrWhiteSpace(InvoiceNumber);
}

public sealed record SaleDetailLine(
    long SaleLineId,
    string Reference,
    string Title,
    int SellerPriceAtSaleCents,
    DateTime? CorrectedAt,
    DateTime? RefundedAt,
    int? RefundItemPriceCents,
    int? RefundCheckoutFeeCents,
    int? RefundTotalCents,
    string? RefundReason)
{
    public bool IsCorrected => CorrectedAt is not null;

    public bool IsRefunded => RefundedAt is not null;

    public string RefundStatusLabel => IsRefunded ? "Remboursé" : string.Empty;
}

internal static class SaleBuyerDisplay
{
    public static string Format(string? buyerName, string? buyerPostalCode, string? buyerCity)
    {
        var location = string.Join(
            ' ',
            new[] { buyerPostalCode, buyerCity }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Join(
            " - ",
            new[] { buyerName, location }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}

public enum SaleInvoicePrintResult
{
    Ready,
    MissingBuyerAddress,
    NotFound
}
