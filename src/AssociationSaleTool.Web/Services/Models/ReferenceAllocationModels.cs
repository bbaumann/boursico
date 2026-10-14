namespace AssociationSaleTool.Web.Services.Models;

public enum ReferenceAllocationStatus
{
    Allocated,
    NoDesk,
    DeskClosed,
    NoLabelsLeft
}

public sealed record ReferenceAllocationResult(
    ReferenceAllocationStatus Status,
    IReadOnlyList<string> AssignedReferences,
    IReadOnlyList<char> TakenLetters)
{
    public static ReferenceAllocationResult Failed(ReferenceAllocationStatus status) => new(status, [], []);
}
