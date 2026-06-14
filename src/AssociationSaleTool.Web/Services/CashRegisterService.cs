using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class CashRegisterService(AppDbContext dbContext)
{
    public async Task<IReadOnlyList<CashRegisterOption>> ListActiveCashRegistersAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CashRegisters
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CashRegisterOption(x.Id, x.Name, x.InitialCashCents))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashRegisterAdminRow>> ListCashRegistersAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CashRegisters
            .AsNoTracking()
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new CashRegisterAdminRow(
                x.Id,
                x.Name,
                x.InitialCashCents,
                x.IsActive,
                x.DisplayOrder,
                x.Sales.Any() || x.RefundSaleLines.Any()))
            .ToListAsync(cancellationToken);
    }

    public async Task<CashRegisterSaveResult> SaveCashRegisterAsync(
        long? cashRegisterId,
        string? name,
        int initialCashCents,
        bool isActive,
        int? displayOrder = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeOptionalText(name);
        if (normalizedName is null)
        {
            return CashRegisterSaveResult.NameRequired;
        }

        if (initialCashCents < 0)
        {
            return CashRegisterSaveResult.NegativeInitialCash;
        }

        var duplicateExists = await dbContext.CashRegisters
            .AsNoTracking()
            .AnyAsync(
                x => x.Name == normalizedName
                    && (cashRegisterId == null || x.Id != cashRegisterId.Value),
                cancellationToken);
        if (duplicateExists)
        {
            return CashRegisterSaveResult.DuplicateName;
        }

        if (cashRegisterId is null)
        {
            dbContext.CashRegisters.Add(new CashRegisterEntity
            {
                Name = normalizedName,
                InitialCashCents = initialCashCents,
                IsActive = isActive,
                DisplayOrder = displayOrder ?? await GetNextDisplayOrderAsync(cancellationToken)
            });
        }
        else
        {
            var cashRegister = await dbContext.CashRegisters
                .Include(x => x.Sales)
                .Include(x => x.RefundSaleLines)
                .SingleOrDefaultAsync(x => x.Id == cashRegisterId.Value, cancellationToken);
            if (cashRegister is null)
            {
                return CashRegisterSaveResult.NotFound;
            }

            var hasMovements = cashRegister.Sales.Count > 0 || cashRegister.RefundSaleLines.Count > 0;
            if (hasMovements && !string.Equals(cashRegister.Name, normalizedName, StringComparison.Ordinal))
            {
                return CashRegisterSaveResult.RenameNotAllowed;
            }

            cashRegister.Name = normalizedName;
            cashRegister.InitialCashCents = initialCashCents;
            cashRegister.IsActive = isActive;
            cashRegister.DisplayOrder = displayOrder ?? cashRegister.DisplayOrder;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return CashRegisterSaveResult.Saved;
    }

    private async Task<int> GetNextDisplayOrderAsync(CancellationToken cancellationToken)
    {
        var hasCashRegisters = await dbContext.CashRegisters.AnyAsync(cancellationToken);
        if (!hasCashRegisters)
        {
            return 0;
        }

        return await dbContext.CashRegisters.MaxAsync(x => x.DisplayOrder, cancellationToken) + 1;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
