using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Domain.Pricing;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SaleCorrectionService(AppDbContext dbContext)
{
    public async Task<List<CashRegisterOption>> ListRefundCashRegistersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.CashRegisters
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CashRegisterOption(x.Id, x.Name, x.InitialCashCents))
            .ToListAsync(cancellationToken);
    }

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
                // "sold" implies a reference was already allocated:
                // IntakeService.ValidateSellerDepositAsync refuses to move a
                // registered item to in_stock while its reference is null.
                x.Item.Reference!,
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
        long? refundCashRegisterId,
        CancellationToken cancellationToken = default)
    {
        if (refundCashRegisterId is null)
        {
            return SaleCorrectionResult.InvalidCashRegister;
        }

        var cashRegisterExists = await dbContext.CashRegisters
            .AsNoTracking()
            .AnyAsync(x => x.Id == refundCashRegisterId.Value && x.IsActive, cancellationToken);
        if (!cashRegisterExists)
        {
            return SaleCorrectionResult.InvalidCashRegister;
        }

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
        saleLine.RefundCashRegisterId = refundCashRegisterId.Value;
        saleLine.Item.Status = "defective";

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaleCorrectionResult.Refunded;
    }

    public async Task<SaleCorrectionResult> CancelDefectiveRefundAsync(
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

        if (saleLine.CorrectedAt is not null
            || saleLine.RefundedAt is null
            || saleLine.RefundReason != "defective"
            || saleLine.Item.Status != "defective")
        {
            return SaleCorrectionResult.NotAllowed;
        }

        var hasOtherActiveSaleLine = await dbContext.SaleLines
            .AsNoTracking()
            .AnyAsync(x =>
                x.ItemId == saleLine.ItemId
                && x.Id != saleLine.Id
                && x.CorrectedAt == null
                && x.RefundedAt == null,
                cancellationToken);

        if (hasOtherActiveSaleLine)
        {
            return SaleCorrectionResult.NotAllowed;
        }

        saleLine.RefundedAt = null;
        saleLine.RefundItemPriceCents = null;
        saleLine.RefundCheckoutFeeCents = null;
        saleLine.RefundTotalCents = null;
        saleLine.RefundReason = null;
        saleLine.RefundCashRegisterId = null;
        saleLine.Item.Status = "sold";

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaleCorrectionResult.RefundCanceled;
    }
}
