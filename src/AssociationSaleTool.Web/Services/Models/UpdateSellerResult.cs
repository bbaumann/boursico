namespace AssociationSaleTool.Web.Services.Models;

public sealed record UpdateSellerResult(
    UpdateSellerOutcome Outcome,
    IReadOnlyList<SellerDuplicateMatch> DuplicateMatches)
{
    public static UpdateSellerResult Updated { get; } = new(UpdateSellerOutcome.Updated, []);

    public static UpdateSellerResult NotFound { get; } = new(UpdateSellerOutcome.NotFound, []);

    public static UpdateSellerResult NotAllowed { get; } = new(UpdateSellerOutcome.NotAllowed, []);

    public static UpdateSellerResult DuplicateFound(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        return new UpdateSellerResult(UpdateSellerOutcome.DuplicateFound, matches);
    }
}

public enum UpdateSellerOutcome
{
    Updated,
    NotFound,
    NotAllowed,
    DuplicateFound
}
