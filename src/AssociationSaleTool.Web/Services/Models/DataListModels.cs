namespace AssociationSaleTool.Web.Services.Models;

public sealed record DataListItemRow(
    long ItemId,
    long SellerId,
    string? Reference,
    string Title,
    string SellerCode,
    string SellerName,
    string Status,
    string StatusLabel,
    int? SellerPriceCents,
    string SearchText);

public sealed record DataListSellerRow(
    long SellerId,
    string SellerCode,
    string SellerName,
    string? Email,
    string Phone,
    int ItemCount,
    int RegisteredCount,
    int InStockCount,
    int SoldCount,
    int MissingCount,
    int DefectiveCount,
    int ExpectedFeeCents,
    int PaidFeeCents,
    int RemainingFeeCents,
    string SearchText);

public sealed record DataListSaleRow(
    long SaleId,
    DateTime SoldAt,
    int ItemCount,
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalPaidCents,
    string BuyerDisplay,
    string ItemSummary,
    string PaymentMethodLabel,
    bool HasInvoice,
    string SearchText);

public sealed record DataListChequeRow(
    long SaleId,
    string ChequeNumber,
    string ChequeBankName,
    int TotalPaidCents,
    string SearchText);

public sealed record DataListCashRegisterChequeGroup(
    long CashRegisterId,
    string CashRegisterName,
    IReadOnlyList<DataListCashRegisterChequeRow> Cheques,
    int TotalCents);

public sealed record DataListCashRegisterChequeRow(
    long SaleId,
    string? ChequeBankName,
    string? ChequeNumber,
    int TotalPaidCents,
    string? BuyerName);

public sealed record DataListClosingRow(
    string Label,
    long? CashRegisterId,
    bool IsGlobal,
    int SaleCount,
    int ItemCount,
    int InitialCashCents,
    int SubtotalSellerPriceCents,
    int CheckoutFeeCents,
    int TotalCollectedCents,
    int RefundTotalCents,
    int NetCollectedCents,
    int CardTotalCents,
    int CashTotalCents,
    int ChequeTotalCents,
    int ExpectedCashCents);
