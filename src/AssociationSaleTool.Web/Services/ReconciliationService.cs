using System.Globalization;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class ReconciliationService(AppDbContext dbContext)
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] SellerSheetStatuses = ["sold", "missing", "in_stock", "defective"];
    private static readonly string[] CorrectionStatuses = ["in_stock", "missing"];

    public async Task<List<ReconciliationItemSearchResult>> SearchItemsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query.Trim().ToLower(FrenchCulture);
        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return [];
        }

        var candidates = await ListCorrectionItemsAsync(cancellationToken);

        return candidates
            .Where(x =>
                ContainsNormalized(x.Reference, normalizedQuery) ||
                ContainsNormalized(x.Title, normalizedQuery) ||
                ContainsNormalized(x.SellerCode, normalizedQuery) ||
                ContainsNormalized(x.SellerName, normalizedQuery))
            .OrderBy(x => x.Reference)
            .Take(25)
            .ToList();
    }

    public async Task<List<SellerSheetOption>> ListSellerSheetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Sellers
            .AsNoTracking()
            .Where(x => x.Items.Any(item => SellerSheetStatuses.Contains(item.Status)
                && item.SellerPriceCents.HasValue))
            .OrderBy(x => x.SellerCode)
            .Select(x => new SellerSheetOption(
                x.SellerCode,
                $"{x.FirstName} {x.LastName}"))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ReconciliationItemSearchResult>> ListCorrectionItemsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Items
            .AsNoTracking()
            .Where(x => CorrectionStatuses.Contains(x.Status) && x.SellerPriceCents.HasValue)
            .OrderBy(x => x.Reference)
            .Select(x => new ReconciliationItemSearchResult(
                x.Id,
                x.SellerId,
                x.Seller.SellerCode,
                $"{x.Seller.FirstName} {x.Seller.LastName}",
                x.Reference,
                x.Title,
                x.Status,
                x.SellerPriceCents!.Value))
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
        var normalizedSellerCode = string.IsNullOrWhiteSpace(sellerCode)
            ? null
            : sellerCode.Trim().ToUpperInvariant();

        var sellers = await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .ThenInclude(x => x.SaleLines)
            .Where(x => normalizedSellerCode == null || x.SellerCode == normalizedSellerCode)
            .Where(x => x.Items.Any(item => SellerSheetStatuses.Contains(item.Status)))
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
                    .Where(item => item.Status is "in_stock" or "defective")
                    .OrderBy(item => item.ItemNumber)
                    .Select(BuildReturnedItem)
                    .ToList();
                var itemSubtotalCents = payableItems.Sum(item => item.PriceCents);
                var expectedFeeCents = SellerFeeCalculator.CalculateExpectedFeeCents(seller.Items.Count);
                var remainingFeeCents = SellerFeeCalculator.CalculateRemainingFeeCents(expectedFeeCents, seller.FeePaidCents);
                var totalPayableCents = Math.Max(itemSubtotalCents - remainingFeeCents, 0);

                return new SellerSheet(
                    seller.Id,
                    seller.SellerCode,
                    $"{seller.FirstName} {seller.LastName}",
                    seller.Email,
                    seller.Phone,
                    seller.PostalAddress,
                    seller.PostalCode,
                    seller.City,
                    payableItems,
                    returnedItems,
                    itemSubtotalCents,
                    expectedFeeCents,
                    seller.FeePaidCents,
                    remainingFeeCents,
                    totalPayableCents);
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
        int priceCents;

        if (isMissing)
        {
            priceCents = item.SellerPriceCents ?? throw new ReconciliationSellerSheetDataException(item.Reference);
        }
        else
        {
            var activeSaleLine = item.SaleLines.SingleOrDefault(line => line.CorrectedAt is null && line.RefundedAt is null);
            if (activeSaleLine is null)
            {
                throw new ReconciliationSellerSheetDataException(item.Reference);
            }

            priceCents = activeSaleLine.SellerPriceAtSaleCents;
        }

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
            item.SellerPriceCents ?? throw new ReconciliationSellerSheetDataException(item.Reference),
            false);
    }
}

public sealed class ReconciliationSellerSheetDataException(string itemReference)
    : InvalidOperationException($"Sold item {itemReference} has no sale line.")
{
    public string ItemReference { get; } = itemReference;
}
