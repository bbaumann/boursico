namespace AssociationSaleTool.Web.Services.Models;

public sealed record SaleStatistics(
    SaleStatisticsFees Fees,
    SaleStatisticsClientMoney Clients,
    SaleStatisticsSellerSales Sellers,
    SaleStatisticsItemCounts Items,
    IReadOnlyList<SaleStatisticsPriceBracket> PriceBrackets,
    IReadOnlyList<SaleStatisticsCity> Cities);

public sealed record SaleStatisticsFees(
    int FlatFeeExpectedCents,
    int FlatFeePaidAtDepositCents,
    int FlatFeeDeductedAtPayoutCents,
    int FlatFeeUncollectedCents,
    int SellerMarginCents,
    int ClientFeeCents)
{
    public int TotalCents => FlatFeePaidAtDepositCents + FlatFeeDeductedAtPayoutCents + SellerMarginCents + ClientFeeCents;
}

public sealed record SaleStatisticsClientMoney(
    int SaleCount,
    int CollectedCents,
    int RefundedCents,
    int CardCents,
    int CashCents,
    int ChequeCents)
{
    public int NetCents => CollectedCents - RefundedCents;
}

public sealed record SaleStatisticsSellerSales(
    int SoldAmountCents,
    int SellersWithSalesCount,
    int MissingItemCount,
    int MissingItemAmountCents);

public sealed record SaleStatisticsItemCounts(
    int TotalCount,
    int RegisteredCount,
    int InStockCount,
    int SoldCount,
    int MissingCount,
    int DefectiveCount);

public sealed record SaleStatisticsPriceBracket(
    string Label,
    int ItemCount,
    int ItemAmountCents,
    int SoldCount,
    int SoldAmountCents);

public sealed record SaleStatisticsCity(
    string City,
    int SaleCount);
