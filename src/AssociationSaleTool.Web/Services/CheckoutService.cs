using AssociationSaleTool.Web.Data;
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
}
