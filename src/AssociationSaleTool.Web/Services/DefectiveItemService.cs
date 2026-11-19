using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Domain.Search;
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
        var search = TextSearch.Parse(query);
        if (search.IsEmpty)
        {
            return [];
        }

        // Accent-insensitive word matching cannot be expressed in SQLite, and the
        // candidate list (in-stock and defective items) is small enough to filter here.
        var candidates = await ListCandidatesAsync(cancellationToken);
        return candidates
            .Where(x => search.Matches(x.Reference, x.Title, x.SellerName))
            .Take(25)
            .ToList();
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
