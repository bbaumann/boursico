using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Pricing;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class CheckoutService(AppDbContext dbContext)
{
    public async Task<List<CheckoutCatalogItem>> ListCatalogItemsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Items
            .AsNoTracking()
            .Where(x => x.Status == "in_stock")
            .OrderBy(x => x.Reference)
            .Select(x => new CheckoutCatalogItem(
                x.Id,
                x.Reference,
                x.Title,
                x.SellerPriceCents))
            .ToListAsync(cancellationToken);
    }

    public async Task<CheckoutValidationResult> ValidateCartAsync(
        IReadOnlyList<long> itemIds,
        CancellationToken cancellationToken = default)
    {
        var items = await LoadCartItemsAsync(itemIds, asTracking: false, cancellationToken);
        return BuildValidationResult(itemIds, items);
    }

    public async Task<CheckoutSaleResult> SaveSaleAsync(
        IReadOnlyList<long> itemIds,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var items = await LoadCartItemsAsync(itemIds, asTracking: true, cancellationToken);
        var validation = BuildValidationResult(itemIds, items);

        if (!validation.IsValid || validation.Totals is null)
        {
            return new CheckoutSaleResult(
                false,
                null,
                validation.ConflictItemIds,
                null,
                validation.ItemCount);
        }

        var sale = new SaleEntity
        {
            SoldAt = DateTime.UtcNow,
            SubtotalSellerPriceCents = validation.Totals.SubtotalSellerPriceCents,
            CheckoutFeeCents = validation.Totals.CheckoutFeeCents,
            TotalPaidCents = validation.Totals.TotalPaidCents
        };

        foreach (var item in items)
        {
            item.Status = "sold";
            sale.SaleLines.Add(new SaleLineEntity
            {
                ItemId = item.Id,
                SellerPriceAtSaleCents = item.SellerPriceCents
            });
        }

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CheckoutSaleResult(
            true,
            sale.Id,
            [],
            validation.Totals,
            validation.ItemCount);
    }

    private async Task<List<ItemEntity>> LoadCartItemsAsync(
        IReadOnlyList<long> itemIds,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var distinctIds = itemIds.Distinct().ToList();
        var query = dbContext.Items.Where(x => distinctIds.Contains(x.Id));

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
            .OrderBy(x => x.Reference)
            .ToListAsync(cancellationToken);
    }

    private static CheckoutValidationResult BuildValidationResult(
        IReadOnlyList<long> requestedItemIds,
        List<ItemEntity> loadedItems)
    {
        var loadedById = loadedItems.ToDictionary(x => x.Id);
        var conflictItemIds = requestedItemIds
            .Where(itemId => !loadedById.TryGetValue(itemId, out var item) || item.Status != "in_stock")
            .Distinct()
            .ToList();

        if (requestedItemIds.Count == 0 || conflictItemIds.Count > 0)
        {
            return new CheckoutValidationResult(false, conflictItemIds, null, loadedItems.Count);
        }

        var subtotalCents = loadedItems.Sum(x => x.SellerPriceCents);
        var totals = CheckoutPricingCalculator.CalculateTotals(subtotalCents);
        var cartTotals = new CheckoutCartTotals(
            totals.SubtotalSellerPriceCents,
            totals.CheckoutFeeCents,
            totals.TotalPaidCents);

        return new CheckoutValidationResult(true, [], cartTotals, loadedItems.Count);
    }
}
