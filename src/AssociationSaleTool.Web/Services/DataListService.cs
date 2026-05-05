using System.Globalization;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class DataListService(AppDbContext dbContext)
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public async Task<IReadOnlyList<DataListItemRow>> ListItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.Items
            .AsNoTracking()
            .Include(x => x.Seller)
            .OrderBy(x => x.Reference)
            .ToListAsync(cancellationToken);

        return items
            .Select(item =>
            {
                var sellerName = FormatSellerName(item.Seller);
                var statusLabel = ItemStatusDisplay.ToFrenchLabel(item.Status);
                return new DataListItemRow(
                    item.Id,
                    item.SellerId,
                    item.Reference,
                    item.Title,
                    item.Seller.SellerCode,
                    sellerName,
                    item.Status,
                    statusLabel,
                    item.SellerPriceCents,
                    BuildSearchText(item.Reference, item.Title, item.Seller.SellerCode, sellerName, statusLabel, item.Status));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DataListSellerRow>> ListSellersAsync(CancellationToken cancellationToken = default)
    {
        var sellers = await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .OrderBy(x => x.SellerCode)
            .ToListAsync(cancellationToken);

        return sellers
            .Select(seller =>
            {
                var sellerName = FormatSellerName(seller);
                var expectedFeeCents = SellerFeeCalculator.CalculateExpectedFeeCents(seller.Items.Count);
                var remainingFeeCents = SellerFeeCalculator.CalculateRemainingFeeCents(expectedFeeCents, seller.FeePaidCents);

                return new DataListSellerRow(
                    seller.Id,
                    seller.SellerCode,
                    sellerName,
                    seller.Email,
                    seller.Phone,
                    seller.Items.Count,
                    seller.Items.Count(item => item.Status == "registered"),
                    seller.Items.Count(item => item.Status == "in_stock"),
                    seller.Items.Count(item => item.Status == "sold"),
                    seller.Items.Count(item => item.Status == "missing"),
                    expectedFeeCents,
                    seller.FeePaidCents,
                    remainingFeeCents,
                    BuildSearchText(seller.SellerCode, sellerName));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DataListSaleRow>> ListSalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Include(x => x.SaleLines)
            .ThenInclude(x => x.Item)
            .OrderByDescending(x => x.SoldAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return sales
            .Select(sale =>
            {
                var buyerDisplay = SaleBuyerDisplay.Format(sale.BuyerName, sale.BuyerPostalCode, sale.BuyerCity);
                var paymentMethodLabel = SalePaymentMethods.GetDisplayLabel(sale.PaymentMethod);
                var itemSummary = string.Join(
                    ", ",
                    sale.SaleLines
                        .OrderBy(line => line.Item.Reference)
                        .Select(line => $"{line.Item.Reference} - {line.Item.Title}"));
                var searchTerms = new List<string>
                {
                    sale.Id.ToString(FrenchCulture),
                    sale.BuyerName ?? string.Empty,
                    sale.BuyerPostalCode ?? string.Empty,
                    sale.BuyerCity ?? string.Empty,
                    paymentMethodLabel
                };
                searchTerms.AddRange(sale.SaleLines.SelectMany(line => new[] { line.Item.Reference, line.Item.Title }));

                return new DataListSaleRow(
                    sale.Id,
                    sale.SoldAt,
                    sale.SaleLines.Count,
                    sale.SubtotalSellerPriceCents,
                    sale.CheckoutFeeCents,
                    sale.TotalPaidCents,
                    buyerDisplay,
                    itemSummary,
                    paymentMethodLabel,
                    !string.IsNullOrWhiteSpace(sale.BuyerAddress),
                    BuildSearchText(searchTerms.ToArray()));
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DataListChequeRow>> ListChequesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await dbContext.Sales
            .AsNoTracking()
            .Where(x => x.PaymentMethod == SalePaymentMethods.Cheque)
            .OrderByDescending(x => x.SoldAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return sales
            .Where(sale => !string.IsNullOrWhiteSpace(sale.ChequeNumber)
                && !string.IsNullOrWhiteSpace(sale.ChequeBankName))
            .Select(sale =>
            {
                var chequeNumber = sale.ChequeNumber!.Trim();
                var chequeBankName = sale.ChequeBankName!.Trim();

                return new DataListChequeRow(
                    sale.Id,
                    chequeNumber,
                    chequeBankName,
                    sale.TotalPaidCents,
                    BuildSearchText(
                        sale.Id.ToString(FrenchCulture),
                        chequeNumber,
                        chequeBankName,
                        $"{sale.TotalPaidCents / 100m:0.00} EUR"));
            })
            .ToList();
    }

    private static string FormatSellerName(SellerEntity seller)
    {
        return $"{seller.FirstName} {seller.LastName}".Trim();
    }

    private static string BuildSearchText(params string[] terms)
    {
        return string.Join(' ', terms.Where(term => !string.IsNullOrWhiteSpace(term)))
            .ToLower(FrenchCulture);
    }
}
