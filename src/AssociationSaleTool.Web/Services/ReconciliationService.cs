using System.Globalization;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class ReconciliationService(AppDbContext dbContext)
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public async Task<List<ReconciliationItemSearchResult>> SearchItemsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim().ToLower(FrenchCulture);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return [];
        }

        var candidates = await dbContext.Items
            .AsNoTracking()
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

        return candidates
            .Where(x => ContainsNormalized(x.Reference, normalizedQuery) || ContainsNormalized(x.Title, normalizedQuery))
            .OrderBy(x => x.Reference)
            .Take(25)
            .ToList();
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
        var updatedRows = await dbContext.Items
            .Where(x => x.Id == itemId && x.Status == expectedStatus)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.Status, nextStatus),
                cancellationToken);

        if (updatedRows > 0)
        {
            return ReconciliationStatusChangeResult.Changed;
        }

        var itemExists = await dbContext.Items
            .AsNoTracking()
            .AnyAsync(x => x.Id == itemId, cancellationToken);

        return itemExists
            ? ReconciliationStatusChangeResult.NotAllowed
            : ReconciliationStatusChangeResult.NotFound;
    }

    private static bool ContainsNormalized(string value, string normalizedQuery)
    {
        return value.ToLower(FrenchCulture).Contains(normalizedQuery);
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
