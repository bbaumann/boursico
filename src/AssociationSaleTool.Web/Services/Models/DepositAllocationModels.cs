using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public enum DepositAllocationStatus
{
    Done,
    NotFound,
    NotAllowed,
    NoDesk,
    DeskClosed,
    NoLabelsLeft,
    Conflict
}

public sealed record DepositAllocationResult(
    DepositAllocationStatus Status,
    IReadOnlyList<ItemEntity> Items,
    IReadOnlyList<string> AssignedReferences,
    IReadOnlyList<char> TakenLetters)
{
    public static DepositAllocationResult Failed(DepositAllocationStatus status) => new(status, [], [], []);
}
