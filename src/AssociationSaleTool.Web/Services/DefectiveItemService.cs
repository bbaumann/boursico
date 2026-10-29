using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class DefectiveItemService(AppDbContext dbContext)
{
    public async Task<List<DefectiveItemCandidate>> ListCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await BuildCandidateQuery()
            .OrderBy(x => x.Reference)
            .Select(x => new DefectiveItemCandidate(
                x.Id,
                x.SellerId,
                x.Seller.SellerCode,
                x.Seller.FirstName + " " + x.Seller.LastName,
                // "in_stock"/"defective" implies a reference was already
                // allocated: IntakeService.ValidateSellerDepositAsync refuses
                // to move a registered item to in_stock while its reference
                // is null, and defective items can only reach that status
                // from in_stock.
                x.Reference!,
                x.Title,
                x.Status,
                x.SellerPriceCents!.Value,
                x.SaleLines.Any(line => line.RefundedAt != null)))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<DefectiveItemCandidate>> SearchCandidatesAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        var pattern = $"%{normalizedQuery}%";
        return await BuildCandidateQuery()
            .Where(x =>
                EF.Functions.Like(x.Reference, pattern)
                || EF.Functions.Like(x.Title, pattern)
                || EF.Functions.Like(x.Seller.FirstName + " " + x.Seller.LastName, pattern)
                || EF.Functions.Like(x.Seller.LastName + " " + x.Seller.FirstName, pattern))
            .OrderBy(x => x.Reference)
            .Take(25)
            .Select(x => new DefectiveItemCandidate(
                x.Id,
                x.SellerId,
                x.Seller.SellerCode,
                x.Seller.FirstName + " " + x.Seller.LastName,
                // Same guarantee as ListCandidatesAsync above.
                x.Reference!,
                x.Title,
                x.Status,
                x.SellerPriceCents!.Value,
                x.SaleLines.Any(line => line.RefundedAt != null)))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Data.Entities.ItemEntity> BuildCandidateQuery()
    {
        return dbContext.Items
            .AsNoTracking()
            .Where(x => (x.Status == "in_stock" || x.Status == "defective")
                && x.SellerPriceCents.HasValue);
    }

    public async Task<DefectiveItemStatusChangeResult> MarkDefectiveAsync(
        long itemId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var item = await dbContext.Items.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return DefectiveItemStatusChangeResult.NotFound;
        }

        await dbContext.Entry(item).ReloadAsync(cancellationToken);

        if (item.Status != "in_stock")
        {
            return DefectiveItemStatusChangeResult.NotAllowed;
        }

        item.Status = "defective";
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DefectiveItemStatusChangeResult.Changed;
    }

    public async Task<DefectiveItemStatusChangeResult> MarkInStockAsync(
        long itemId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var item = await dbContext.Items.SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken);
        if (item is null)
        {
            return DefectiveItemStatusChangeResult.NotFound;
        }

        await dbContext.Entry(item).ReloadAsync(cancellationToken);

        if (item.Status != "defective")
        {
            return DefectiveItemStatusChangeResult.NotAllowed;
        }

        var hasRefundHistory = await dbContext.SaleLines
            .AsNoTracking()
            .AnyAsync(x => x.ItemId == itemId && x.RefundedAt != null, cancellationToken);

        if (hasRefundHistory)
        {
            return DefectiveItemStatusChangeResult.NotAllowed;
        }

        item.Status = "in_stock";
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DefectiveItemStatusChangeResult.Changed;
    }
}
