using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class ItemCorrectionService(AppDbContext dbContext)
{
    private const int DefaultLimit = 15;

    public async Task<List<ItemCorrectionCandidate>> ListCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await ListCandidatesAsync(DefaultLimit, cancellationToken);
    }

    public async Task<List<ItemCorrectionCandidate>> ListCandidatesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        return await dbContext.Items
            .AsNoTracking()
            .Where(x => x.Status != "registered")
            .OrderBy(x => x.Reference)
            .Take(limit)
            .Select(x => new ItemCorrectionCandidate(
                x.Id,
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (long?)line.Id)
                    .FirstOrDefault(),
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (long?)line.SaleId)
                    .FirstOrDefault(),
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (DateTime?)line.Sale.SoldAt)
                    .FirstOrDefault(),
                x.Reference,
                x.Title,
                x.Seller.SellerCode,
                x.Seller.FirstName + " " + x.Seller.LastName,
                x.Status,
                x.SellerPriceCents,
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (int?)line.SellerPriceAtSaleCents)
                    .FirstOrDefault(),
                x.SaleLines.Any(line => line.RefundedAt != null)))
            .ToListAsync(cancellationToken);
    }
}
