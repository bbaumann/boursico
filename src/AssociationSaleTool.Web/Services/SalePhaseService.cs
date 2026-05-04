using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssociationSaleTool.Web.Services;

public sealed class SalePhaseService(AppDbContext dbContext)
{
    private const int EventStateId = 1;

    public async Task<SalePhase> GetCurrentPhaseAsync(CancellationToken cancellationToken = default)
    {
        var eventState = await GetOrCreateEventStateAsync(cancellationToken);
        return FromStorageValue(eventState.SalePhase);
    }

    public async Task SetPhaseAsync(SalePhase phase, CancellationToken cancellationToken = default)
    {
        var eventState = await GetOrCreateEventStateAsync(cancellationToken);
        eventState.SalePhase = ToStorageValue(phase);
        eventState.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CanCreateSalesAsync(CancellationToken cancellationToken = default)
    {
        return await GetCurrentPhaseAsync(cancellationToken) != SalePhase.PostSale;
    }

    public async Task<bool> CanEditItemMasterDataAsync(CancellationToken cancellationToken = default)
    {
        return await GetCurrentPhaseAsync(cancellationToken) == SalePhase.Presale;
    }

    public static string ToStorageValue(SalePhase phase)
    {
        return phase switch
        {
            SalePhase.Presale => "presale",
            SalePhase.Sale => "sale",
            SalePhase.PostSale => "post_sale",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
    }

    public static bool TryParseStorageValue(string? value, out SalePhase phase)
    {
        phase = value switch
        {
            "presale" => SalePhase.Presale,
            "sale" => SalePhase.Sale,
            "post_sale" => SalePhase.PostSale,
            _ => SalePhase.Presale
        };

        return value is "presale" or "sale" or "post_sale";
    }

    private static SalePhase FromStorageValue(string value)
    {
        if (TryParseStorageValue(value, out var phase))
        {
            return phase;
        }

        throw new InvalidOperationException($"Unknown sale phase '{value}'.");
    }

    private async Task<EventStateEntity> GetOrCreateEventStateAsync(CancellationToken cancellationToken)
    {
        var eventState = await dbContext.EventState.SingleOrDefaultAsync(x => x.Id == EventStateId, cancellationToken);
        if (eventState is not null)
        {
            return eventState;
        }

        eventState = new EventStateEntity
        {
            Id = EventStateId,
            SalePhase = "presale",
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.EventState.Add(eventState);
        await dbContext.SaveChangesAsync(cancellationToken);
        return eventState;
    }
}
