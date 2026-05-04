using System.Globalization;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SalesViewService(AppDbContext dbContext)
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public async Task<IReadOnlyList<SalesListItem>> ListSalesAsync(CancellationToken cancellationToken = default)
    {
        var sales = await dbContext.Sales
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
        var sale = await dbContext.Sales
            .AsNoTracking()
            .Include(x => x.SaleLines)
            .ThenInclude(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == saleId, cancellationToken);

        return sale is null ? null : BuildDetail(sale);
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
            !string.IsNullOrWhiteSpace(sale.ReceiptDetails),
            BuildSearchText(sale));
    }

    private static SaleDetail BuildDetail(SaleEntity sale)
    {
        var lines = sale.SaleLines
            .OrderBy(x => x.Item.Reference)
            .Select(x => new SaleDetailLine(
                x.Id,
                x.Item.Reference,
                x.Item.Title,
                x.SellerPriceAtSaleCents,
                x.CorrectedAt))
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
            sale.ReceiptDetails,
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
            sale.ReceiptDetails ?? string.Empty
        };

        terms.AddRange(sale.SaleLines.SelectMany(x => new[] { x.Item.Reference, x.Item.Title }));
        return string.Join(' ', terms).ToLower(FrenchCulture);
    }
}
