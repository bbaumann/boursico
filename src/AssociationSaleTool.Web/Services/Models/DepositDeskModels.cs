namespace AssociationSaleTool.Web.Services.Models;

public enum DeskState
{
    None,
    Open,
    Closed
}

public sealed record DeskLetterInfo(char Letter, int LabelsLeft);

public sealed record CurrentDesk(DeskState State, long? DeskId, string? Name, IReadOnlyList<DeskLetterInfo> Letters)
{
    public static CurrentDesk None { get; } = new(DeskState.None, null, null, []);
}

public sealed record OpenDeskResult(long DeskId, string Name, string Token, IReadOnlyList<char> Letters);

public enum LabelsPerLetterUpdateStatus
{
    Saved,
    OutOfRange,
    BelowUsed
}

public sealed record LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus Status, char? Letter = null, int HighestUsed = 0);

public sealed record ReferenceLetterRow(char Letter, int Used, int LabelsLeft, string? HeldByDeskName);

public sealed record OpenDeskRow(long Id, string Name, IReadOnlyList<char> Letters, DateTime OpenedAt, DateTime LastActivityAt);
