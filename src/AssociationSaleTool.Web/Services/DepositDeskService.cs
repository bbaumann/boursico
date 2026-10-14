using System.Security.Cryptography;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Services;

public sealed class DepositDeskService(AppDbContext dbContext, IOptions<LabelsOptions>? labelsOptions = null)
{
    public const int LowLabelsThreshold = 20;
    public const int MaxLabelsPerLetter = 999;

    private readonly int _lettersPerDesk = (labelsOptions?.Value ?? new LabelsOptions()).LettersPerDesk;

    public async Task<OpenDeskResult> OpenDeskAsync(string? name, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var deskCount = await dbContext.DepositDesks.CountAsync(cancellationToken);
        var labelsPerLetter = await GetLabelsPerLetterAsync(cancellationToken);
        var letters = await dbContext.ReferenceLetters.ToListAsync(cancellationToken);
        var picked = DeskLetterPicker.PickFreeLetters(ToStates(letters), _lettersPerDesk, labelsPerLetter);

        var desk = new DepositDeskEntity
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Poste {deskCount + 1}" : name.Trim(),
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            OpenedAt = now,
            LastActivityAt = now
        };
        foreach (var letter in letters.Where(x => picked.Contains(x.Letter[0])))
        {
            desk.Letters.Add(letter);
        }

        // The desk and its letter holdings are saved together: the concurrency
        // token on held_by_desk_id makes a desk opened at the same instant
        // fail here and roll the desk row back rather than leave a ghost desk
        // or share a letter; the operator re-submits and gets the next free letters.
        dbContext.DepositDesks.Add(desk);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new OpenDeskResult(desk.Id, desk.Name, desk.Token, picked);
    }

    public async Task<CurrentDesk> GetCurrentDeskAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return CurrentDesk.None;
        }

        var desk = await dbContext.DepositDesks
            .AsNoTracking()
            .Include(x => x.Letters)
            .SingleOrDefaultAsync(x => x.Token == token, cancellationToken);
        if (desk is null)
        {
            return CurrentDesk.None;
        }

        if (desk.ClosedAt is not null)
        {
            return new CurrentDesk(DeskState.Closed, desk.Id, desk.Name, []);
        }

        var labelsPerLetter = await GetLabelsPerLetterAsync(cancellationToken);
        var letters = desk.Letters
            .OrderBy(x => DeskLetterPicker.AlphabetIndex(x.Letter[0]))
            .Select(x => new DeskLetterInfo(x.Letter[0], DeskLetterPicker.Room(ToState(x), labelsPerLetter)))
            .ToList();
        return new CurrentDesk(DeskState.Open, desk.Id, desk.Name, letters);
    }

    public async Task<bool> CloseDeskAsync(long deskId, CancellationToken cancellationToken = default)
    {
        var desk = await dbContext.DepositDesks
            .Include(x => x.Letters)
            .SingleOrDefaultAsync(x => x.Id == deskId, cancellationToken);
        if (desk is null || desk.ClosedAt is not null)
        {
            return false;
        }

        desk.ClosedAt = DateTime.UtcNow;
        foreach (var letter in desk.Letters)
        {
            letter.HeldByDeskId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> GetLabelsPerLetterAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.EventState.AsNoTracking().Select(x => x.LabelsPerLetter).SingleAsync(cancellationToken);
    }

    public async Task<LabelsPerLetterUpdateResult> SetLabelsPerLetterAsync(
        int labelsPerLetter,
        CancellationToken cancellationToken = default)
    {
        if (labelsPerLetter < 1 || labelsPerLetter > MaxLabelsPerLetter)
        {
            return new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.OutOfRange);
        }

        var mostUsed = await dbContext.ReferenceLetters
            .AsNoTracking()
            .OrderByDescending(x => x.LastNumber)
            .ThenBy(x => x.Letter)
            .FirstAsync(cancellationToken);
        if (mostUsed.LastNumber > labelsPerLetter)
        {
            return new LabelsPerLetterUpdateResult(
                LabelsPerLetterUpdateStatus.BelowUsed,
                mostUsed.Letter[0],
                mostUsed.LastNumber);
        }

        var state = await dbContext.EventState.SingleAsync(cancellationToken);
        state.LabelsPerLetter = labelsPerLetter;
        state.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.Saved);
    }

    public async Task<IReadOnlyList<ReferenceLetterRow>> ListLettersAsync(CancellationToken cancellationToken = default)
    {
        var labelsPerLetter = await GetLabelsPerLetterAsync(cancellationToken);
        var letters = await dbContext.ReferenceLetters
            .AsNoTracking()
            .Include(x => x.HeldByDesk)
            .ToListAsync(cancellationToken);

        return letters
            .OrderBy(x => DeskLetterPicker.AlphabetIndex(x.Letter[0]))
            .Select(x => new ReferenceLetterRow(
                x.Letter[0],
                x.LastNumber,
                DeskLetterPicker.Room(ToState(x), labelsPerLetter),
                x.HeldByDesk?.Name))
            .ToList();
    }

    public async Task<IReadOnlyList<OpenDeskRow>> ListOpenDesksAsync(CancellationToken cancellationToken = default)
    {
        var desks = await dbContext.DepositDesks
            .AsNoTracking()
            .Include(x => x.Letters)
            .Where(x => x.ClosedAt == null)
            .OrderBy(x => x.OpenedAt)
            .ToListAsync(cancellationToken);

        return desks
            .Select(x => new OpenDeskRow(
                x.Id,
                x.Name,
                x.Letters
                    .Select(letter => letter.Letter[0])
                    .OrderBy(DeskLetterPicker.AlphabetIndex)
                    .ToList(),
                x.OpenedAt,
                x.LastActivityAt))
            .ToList();
    }

    internal static LetterState ToState(ReferenceLetterEntity letter)
    {
        return new LetterState(letter.Letter[0], letter.LastNumber, letter.HeldByDeskId);
    }

    internal static IReadOnlyList<LetterState> ToStates(IEnumerable<ReferenceLetterEntity> letters)
    {
        return letters.Select(ToState).ToList();
    }
}
