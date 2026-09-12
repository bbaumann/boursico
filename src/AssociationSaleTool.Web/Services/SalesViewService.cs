using System.Globalization;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SalesViewService
{
    private readonly AppDbContext _dbContext;
    private readonly InvoiceNumberService _invoiceNumberService;

    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public SalesViewService(AppDbContext dbContext, InvoiceNumberService? invoiceNumberService = null)
    {
        _dbContext = dbContext;
        _invoiceNumberService = invoiceNumberService ?? new InvoiceNumberService(dbContext);
    }

    public async Task<IReadOnlyList<SalesListItem>> ListSalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Include(x => x.SaleLines)
            .ThenInclude(x => x.Item)
            .OrderByDescending(x => x.SoldAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return sales
            .Select(BuildListItem)
            .ToList();
    }

    public async Task<SaleDetail?> GetSaleAsync(long saleId, CancellationToken cancellationToken = default)
    {
        var sale = await _dbContext.Sales
            .AsNoTracking()
            .Include(x => x.SaleLines)
            .ThenInclude(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == saleId, cancellationToken);

        return sale is null ? null : BuildDetail(sale);
    }

    public async Task<SaleBuyerAddressUpdateResult> UpdateBuyerAddressAsync(
        long saleId,
        string? buyerAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedBuyerAddress = NormalizeOptionalText(buyerAddress);
        if (normalizedBuyerAddress is null)
        {
            return SaleBuyerAddressUpdateResult.MissingBuyerAddress;
        }

        var sale = await _dbContext.Sales
            .SingleOrDefaultAsync(x => x.Id == saleId, cancellationToken);

        if (sale is null)
        {
            return SaleBuyerAddressUpdateResult.NotFound;
        }

        sale.BuyerAddress = normalizedBuyerAddress;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return SaleBuyerAddressUpdateResult.Updated;
    }

    public async Task<SaleInvoicePrintResult> EnsureInvoiceAsync(long saleId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sale = await _dbContext.Sales
            .SingleOrDefaultAsync(x => x.Id == saleId, cancellationToken);

        if (sale is null)
        {
            return SaleInvoicePrintResult.NotFound;
        }

        if (string.IsNullOrWhiteSpace(sale.BuyerAddress))
        {
            return SaleInvoicePrintResult.MissingBuyerAddress;
        }

        if (string.IsNullOrWhiteSpace(sale.InvoiceNumber))
        {
            sale.InvoiceNumber = await _invoiceNumberService.CreateNextInvoiceNumberAsync(cancellationToken);
            sale.InvoiceIssuedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (sale.InvoiceIssuedAt is null)
        {
            sale.InvoiceIssuedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return SaleInvoicePrintResult.Ready;
    }

    private static SalesListItem BuildListItem(SaleEntity sale)
    {
        return new SalesListItem(
            sale.Id,
            sale.SoldAt,
            sale.SaleLines.Count,
            sale.SubtotalSellerPriceCents,
            sale.CheckoutFeeCents,
            sale.TotalPaidCents,
            sale.BuyerName,
            sale.BuyerPostalCode,
            sale.BuyerCity,
            !string.IsNullOrWhiteSpace(sale.InvoiceNumber),
            SalePaymentMethods.GetDisplayLabel(sale.PaymentMethod),
            BuildSearchText(sale));
    }

    private static SaleDetail BuildDetail(SaleEntity sale)
    {
        var lines = sale.SaleLines
            .OrderBy(x => x.Item.Reference)
            .Select(x => new SaleDetailLine(
                x.Id,
                // By convention only: a sold item implies a reference was
                // already allocated by the deposit wizard, but that order is
                // not enforced server-side yet (see the allocation task).
                x.Item.Reference!,
                x.Item.Title,
                x.SellerPriceAtSaleCents,
                x.CorrectedAt,
                x.RefundedAt,
                x.RefundItemPriceCents,
                x.RefundCheckoutFeeCents,
                x.RefundTotalCents,
                x.RefundReason))
            .ToList();

        return new SaleDetail(
            sale.Id,
            sale.SoldAt,
            sale.SubtotalSellerPriceCents,
            sale.CheckoutFeeCents,
            sale.TotalPaidCents,
            sale.BuyerName,
            sale.BuyerPostalCode,
            sale.BuyerCity,
            sale.BuyerAddress,
            sale.InvoiceNumber,
            sale.InvoiceIssuedAt,
            SalePaymentMethods.GetDisplayLabel(sale.PaymentMethod),
            sale.ChequeNumber,
            sale.ChequeBankName,
            lines);
    }

    private static string BuildSearchText(SaleEntity sale)
    {
        var terms = new List<string>
        {
            sale.Id.ToString(FrenchCulture),
            sale.BuyerName ?? string.Empty,
            sale.BuyerPostalCode ?? string.Empty,
            sale.BuyerCity ?? string.Empty,
            sale.BuyerAddress ?? string.Empty,
            sale.InvoiceNumber ?? string.Empty,
            SalePaymentMethods.GetDisplayLabel(sale.PaymentMethod),
            sale.ChequeNumber ?? string.Empty,
            sale.ChequeBankName ?? string.Empty
        };

        // Same convention as BuildDetail above — not yet enforced
        // server-side.
        terms.AddRange(sale.SaleLines.SelectMany(x => new[] { x.Item.Reference!, x.Item.Title }));
        return string.Join(' ', terms).ToLower(FrenchCulture);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
