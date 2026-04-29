using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class ReconciliationService(AppDbContext dbContext)
{
    public async Task<List<SellerSheet>> ListSellerSheetsAsync(
        string? sellerCode = null,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[] { "sold", "missing", "in_stock" };
        var normalizedSellerCode = string.IsNullOrWhiteSpace(sellerCode)
            ? null
            : sellerCode.Trim().ToUpperInvariant();

        var sellers = await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.SaleLine)
            .Where(x => normalizedSellerCode == null || x.SellerCode == normalizedSellerCode)
            .Where(x => x.Items.Any(item => activeStatuses.Contains(item.Status)))
            .OrderBy(x => x.SellerCode)
            .ToListAsync(cancellationToken);

        return sellers
            .Select(seller =>
            {
                var payableItems = seller.Items
                    .Where(item => item.Status is "sold" or "missing")
                    .OrderBy(item => item.ItemNumber)
                    .Select(BuildPayableItem)
                    .ToList();
                var returnedItems = seller.Items
                    .Where(item => item.Status == "in_stock")
                    .OrderBy(item => item.ItemNumber)
                    .Select(BuildReturnedItem)
                    .ToList();

                return new SellerSheet(
                    seller.Id,
                    seller.SellerCode,
                    $"{seller.FirstName} {seller.LastName}",
                    seller.Email,
                    seller.Phone,
                    payableItems,
                    returnedItems,
                    payableItems.Sum(item => item.PriceCents));
            })
            .ToList();
    }

    private static SellerSheetItem BuildPayableItem(ItemEntity item)
    {
        var isMissing = item.Status == "missing";
        var priceCents = isMissing
            ? item.SellerPriceCents
            : item.SaleLine!.SellerPriceAtSaleCents;

        return new SellerSheetItem(
            item.Id,
            item.Reference,
            item.Title,
            item.Status,
            priceCents,
            isMissing);
    }

    private static SellerSheetItem BuildReturnedItem(ItemEntity item)
    {
        return new SellerSheetItem(
            item.Id,
            item.Reference,
            item.Title,
            item.Status,
            item.SellerPriceCents,
            false);
    }
}
