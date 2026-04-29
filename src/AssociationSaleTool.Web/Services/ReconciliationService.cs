using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class ReconciliationService(AppDbContext dbContext)
{
    public async Task<List<ReconciliationItemSearchResult>> SearchItemsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim();
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return [];
        }

        return await dbContext.Items
            .AsNoTracking()
            .Where(x => x.Reference.Contains(normalizedQuery) || x.Title.Contains(normalizedQuery))
            .OrderBy(x => x.Reference)
            .Take(25)
            .Select(x => new ReconciliationItemSearchResult(
                x.Id,
                x.SellerId,
                x.Seller.SellerCode,
                $"{x.Seller.FirstName} {x.Seller.LastName}",
                x.Reference,
                x.Title,
                x.Status,
                x.SellerPriceCents))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReconciliationStatusChangeResult> MarkItemMissingAsync(
        long itemId,
        CancellationToken cancellationToken = default)
    {
        return await ChangeItemStatusAsync(itemId, "in_stock", "missing", cancellationToken);
    }

    public async Task<ReconciliationStatusChangeResult> MarkItemInStockAsync(
        long itemId,
        CancellationToken cancellationToken = default)
    {
        return await ChangeItemStatusAsync(itemId, "missing", "in_stock", cancellationToken);
    }

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

    private async Task<ReconciliationStatusChangeResult> ChangeItemStatusAsync(
        long itemId,
        string expectedStatus,
        string nextStatus,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Items
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        if (item is null)
        {
            return ReconciliationStatusChangeResult.NotFound;
        }

        if (item.Status != expectedStatus)
        {
            return ReconciliationStatusChangeResult.NotAllowed;
        }

        item.Status = nextStatus;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ReconciliationStatusChangeResult.Changed;
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
