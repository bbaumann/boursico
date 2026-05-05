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
    bool HasBuyerAddress,
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
    string PaymentMethodLabel,
    string? ChequeNumber,
    string? ChequeBankName,
    IReadOnlyList<SaleDetailLine> Lines)
{
    public bool HasBuyerDetails =>
        !string.IsNullOrWhiteSpace(BuyerName)
        || !string.IsNullOrWhiteSpace(BuyerPostalCode)
        || !string.IsNullOrWhiteSpace(BuyerCity);

    public string BuyerDisplay => SaleBuyerDisplay.Format(BuyerName, BuyerPostalCode, BuyerCity);
}

public sealed record SaleDetailLine(
    long SaleLineId,
    string Reference,
    string Title,
    int SellerPriceAtSaleCents,
    DateTime? CorrectedAt)
{
    public bool IsCorrected => CorrectedAt is not null;
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
