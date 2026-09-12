using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class ItemCorrectionService(AppDbContext dbContext)
{
    public async Task<List<ItemCorrectionCandidate>> ListCandidatesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Items
            .AsNoTracking()
            .Where(x => x.Status != "registered" && x.SellerPriceCents.HasValue)
            .OrderBy(x => x.Reference)
            .Select(x => new ItemCorrectionCandidate(
                x.Id,
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (long?)line.Id)
                    .FirstOrDefault(),
                x.SaleLines
                    .Where(line => line.RefundedAt != null && line.RefundReason == "defective")
                    .OrderByDescending(line => line.RefundedAt)
                    .Select(line => (long?)line.Id)
                    .FirstOrDefault(),
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (long?)line.SaleId)
                    .FirstOrDefault()
                ?? x.SaleLines
                    .Where(line => line.RefundedAt != null && line.RefundReason == "defective")
                    .OrderByDescending(line => line.RefundedAt)
                    .Select(line => (long?)line.SaleId)
                    .FirstOrDefault(),
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (DateTime?)line.Sale.SoldAt)
                    .FirstOrDefault()
                ?? x.SaleLines
                    .Where(line => line.RefundedAt != null && line.RefundReason == "defective")
                    .OrderByDescending(line => line.RefundedAt)
                    .Select(line => (DateTime?)line.Sale.SoldAt)
                    .FirstOrDefault(),
                // By convention only: any post-deposit status implies a
                // reference was already allocated by the deposit wizard, but
                // that order is not enforced server-side yet (see the
                // allocation task).
                x.Reference!,
                x.Title,
                x.Seller.SellerCode,
                x.Seller.FirstName + " " + x.Seller.LastName,
                x.Status,
                x.SellerPriceCents!.Value,
                x.SaleLines
                    .Where(line => line.CorrectedAt == null && line.RefundedAt == null)
                    .OrderByDescending(line => line.Sale.SoldAt)
                    .Select(line => (int?)line.SellerPriceAtSaleCents)
                    .FirstOrDefault()
                ?? x.SaleLines
                    .Where(line => line.RefundedAt != null && line.RefundReason == "defective")
                    .OrderByDescending(line => line.RefundedAt)
                    .Select(line => (int?)line.SellerPriceAtSaleCents)
                    .FirstOrDefault(),
                x.SaleLines.Any(line => line.RefundedAt != null)))
            .ToListAsync(cancellationToken);
    }
}
