using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Domain.Sellers;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class IntakeService(AppDbContext dbContext)
{
    public async Task<List<SellerEntity>> ListSellersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items)
            .OrderBy(x => x.SellerCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<SellerEntity?> GetSellerAsync(long sellerId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Sellers
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
    }

    public async Task<SellerEntity?> GetSellerWithItemsAsync(long sellerId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Sellers
            .AsNoTracking()
            .Include(x => x.Items.OrderBy(item => item.ItemNumber))
            .SingleOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
    }

    public async Task<ItemEntity?> GetItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Items
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
    }

    public async Task<SellerEntity> CreateSellerAsync(CreateSellerRequest request, CancellationToken cancellationToken = default)
    {
        var seller = new SellerEntity
        {
            SellerCode = SellerCodeGenerator.GetCodeAt(await dbContext.Sellers.CountAsync(cancellationToken)),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = NormalizeOptional(request.Email),
            Phone = NormalizeOptional(request.Phone),
            Notes = NormalizeOptional(request.Notes)
        };

        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync(cancellationToken);
        return seller;
    }

    public async Task<List<ItemEntity>> CreateItemBatchAsync(CreateItemBatchRequest request, CancellationToken cancellationToken = default)
    {
        var seller = await dbContext.Sellers.SingleAsync(x => x.Id == request.SellerId, cancellationToken);
        var nextItemNumber = await dbContext.Items
            .Where(x => x.SellerId == request.SellerId)
            .Select(x => (int?)x.ItemNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var status = request.IntakeMode == IntakeMode.EmailPreregistration ? "registered" : "in_stock";
        var createdItems = new List<ItemEntity>();

        foreach (var row in request.Rows)
        {
            nextItemNumber++;
            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = ItemReference.Format(seller.SellerCode, nextItemNumber),
                ItemNumber = nextItemNumber,
                Title = row.Title.Trim(),
                SellerPriceCents = row.SellerPriceCents,
                Status = status
            };

            dbContext.Items.Add(item);
            createdItems.Add(item);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return createdItems;
    }

    public async Task UpdateSellerAsync(long sellerId, UpdateSellerRequest request, CancellationToken cancellationToken = default)
    {
        var seller = await dbContext.Sellers.SingleAsync(x => x.Id == sellerId, cancellationToken);
        seller.FirstName = request.FirstName.Trim();
        seller.LastName = request.LastName.Trim();
        seller.Email = NormalizeOptional(request.Email);
        seller.Phone = NormalizeOptional(request.Phone);
        seller.Notes = NormalizeOptional(request.Notes);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateItemAsync(long itemId, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.Items.SingleAsync(x => x.Id == itemId, cancellationToken);
        item.Title = request.Title.Trim();
        item.SellerPriceCents = request.SellerPriceCents;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
