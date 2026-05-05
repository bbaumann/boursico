using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Domain.Pricing;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SaleCorrectionService(AppDbContext dbContext)
{
    public async Task<List<SaleCorrectionItem>> ListActiveSoldItemsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaleLines
            .AsNoTracking()
            .Where(x => x.CorrectedAt == null && x.RefundedAt == null && x.Item.Status == "sold")
            .OrderBy(x => x.Item.Reference)
            .Select(x => new SaleCorrectionItem(
                x.Id,
                x.SaleId,
                x.ItemId,
                x.Item.Reference,
                x.Item.Title,
                x.Item.Seller.SellerCode,
                $"{x.Item.Seller.FirstName} {x.Item.Seller.LastName}",
                x.SellerPriceAtSaleCents,
                x.Sale.SoldAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<SaleCorrectionResult> CorrectSaleLineAsync(
        long saleLineId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var saleLine = await dbContext.SaleLines
            .Include(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == saleLineId, cancellationToken);

        if (saleLine is null)
        {
            return SaleCorrectionResult.NotFound;
        }

        await dbContext.Entry(saleLine).ReloadAsync(cancellationToken);
        await dbContext.Entry(saleLine).Reference(x => x.Item).LoadAsync(cancellationToken);
        await dbContext.Entry(saleLine.Item).ReloadAsync(cancellationToken);

        if (saleLine.CorrectedAt is not null || saleLine.RefundedAt is not null || saleLine.Item.Status != "sold")
        {
            return SaleCorrectionResult.NotAllowed;
        }

        saleLine.CorrectedAt = DateTime.UtcNow;
        saleLine.Item.Status = "in_stock";
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaleCorrectionResult.Corrected;
    }

    public async Task<SaleCorrectionResult> RefundDefectiveSaleLineAsync(
        long saleLineId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var saleLine = await dbContext.SaleLines
            .Include(x => x.Item)
            .SingleOrDefaultAsync(x => x.Id == saleLineId, cancellationToken);

        if (saleLine is null)
        {
            return SaleCorrectionResult.NotFound;
        }

        await dbContext.Entry(saleLine).ReloadAsync(cancellationToken);
        await dbContext.Entry(saleLine).Reference(x => x.Item).LoadAsync(cancellationToken);
        await dbContext.Entry(saleLine.Item).ReloadAsync(cancellationToken);

        if (saleLine.CorrectedAt is not null || saleLine.RefundedAt is not null || saleLine.Item.Status != "sold")
        {
            return SaleCorrectionResult.NotAllowed;
        }

        var refundTotals = CheckoutPricingCalculator.CalculateTotals(saleLine.SellerPriceAtSaleCents);
        saleLine.RefundedAt = DateTime.UtcNow;
        saleLine.RefundItemPriceCents = saleLine.SellerPriceAtSaleCents;
        saleLine.RefundCheckoutFeeCents = refundTotals.CheckoutFeeCents;
        saleLine.RefundTotalCents = saleLine.SellerPriceAtSaleCents + refundTotals.CheckoutFeeCents;
        saleLine.RefundReason = "defective";
        saleLine.Item.Status = "defective";

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaleCorrectionResult.Refunded;
    }
}
