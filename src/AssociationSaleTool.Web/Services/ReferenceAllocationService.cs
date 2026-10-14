using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Services;

/// <summary>
/// Gives items their references from the letters a deposit desk holds. It only
/// changes tracked entities: the caller saves the items, the letter counters
/// and the desk's holdings in one SaveChanges, so a refused or failed save
/// leaves no number used. A race with another desk fails at that save: the
/// item.reference unique index fires first when both runs computed the same
/// reference (DbUpdateException), and the concurrency tokens on
/// reference_letter catch a letter taken by both (DbUpdateConcurrencyException).
/// </summary>
public sealed class ReferenceAllocationService(AppDbContext dbContext, IOptions<LabelsOptions>? labelsOptions = null)
{
    private readonly int _lettersPerDesk = (labelsOptions?.Value ?? new LabelsOptions()).LettersPerDesk;

    public async Task<ReferenceAllocationResult> AllocateAsync(
        long? deskId,
        char? sellerLetter,
        IReadOnlyList<ItemEntity> items,
        CancellationToken cancellationToken = default)
    {
        if (deskId is null)
        {
            return ReferenceAllocationResult.Failed(ReferenceAllocationStatus.NoDesk);
        }

        var desk = await dbContext.DepositDesks.SingleOrDefaultAsync(x => x.Id == deskId, cancellationToken);
        if (desk is null || desk.ClosedAt is not null)
        {
            return ReferenceAllocationResult.Failed(ReferenceAllocationStatus.DeskClosed);
        }

        var labelsPerLetter = await dbContext.EventState.Select(x => x.LabelsPerLetter).SingleAsync(cancellationToken);
        var letters = await dbContext.ReferenceLetters.ToListAsync(cancellationToken);
        var assigned = new List<string>();
        var taken = new List<char>();

        if (items.Count > 0)
        {
            var plan = DeskLetterPicker.PlanRun(
                DepositDeskService.ToStates(letters),
                desk.Id,
                sellerLetter,
                items.Count,
                labelsPerLetter);
            if (plan is null)
            {
                return ReferenceAllocationResult.Failed(ReferenceAllocationStatus.NoLabelsLeft);
            }

            foreach (var letter in plan.LettersToTake)
            {
                Find(letters, letter).HeldByDeskId = desk.Id;
                taken.Add(letter);
            }

            var index = 0;
            foreach (var segment in plan.Segments)
            {
                var letter = Find(letters, segment.Letter);
                for (var i = 0; i < segment.Count; i++)
                {
                    letter.LastNumber++;
                    var reference = ItemReference.Format(segment.Letter, letter.LastNumber);
                    items[index++].Reference = reference;
                    assigned.Add(reference);
                }
            }
        }

        // An exhausted letter goes back to the pool, still exhausted, so no
        // other desk takes it; the desk then tops up to its usual count.
        foreach (var letter in letters.Where(x => x.HeldByDeskId == desk.Id && RoomOf(x, labelsPerLetter) == 0))
        {
            letter.HeldByDeskId = null;
        }

        var usable = letters.Count(x => x.HeldByDeskId == desk.Id && RoomOf(x, labelsPerLetter) > 0);
        foreach (var letter in DeskLetterPicker.PickFreeLetters(
                     DepositDeskService.ToStates(letters),
                     _lettersPerDesk - usable,
                     labelsPerLetter))
        {
            Find(letters, letter).HeldByDeskId = desk.Id;
            taken.Add(letter);
        }

        desk.LastActivityAt = DateTime.UtcNow;
        return new ReferenceAllocationResult(ReferenceAllocationStatus.Allocated, assigned, taken);
    }

    private static int RoomOf(ReferenceLetterEntity letter, int labelsPerLetter)
    {
        return DeskLetterPicker.Room(DepositDeskService.ToState(letter), labelsPerLetter);
    }

    private static ReferenceLetterEntity Find(IEnumerable<ReferenceLetterEntity> letters, char letter)
    {
        return letters.Single(x => x.Letter[0] == letter);
    }
}
