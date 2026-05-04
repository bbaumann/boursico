using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerResult(
    CreateSellerOutcome Outcome,
    SellerEntity? Seller,
    IReadOnlyList<SellerDuplicateMatch> DuplicateMatches)
{
    public long Id => Seller?.Id ?? throw new InvalidOperationException("No seller was created.");

    public string SellerCode => Seller?.SellerCode ?? throw new InvalidOperationException("No seller was created.");

    public static CreateSellerResult Created(SellerEntity seller)
    {
        return new CreateSellerResult(CreateSellerOutcome.Created, seller, []);
    }

    public static CreateSellerResult DuplicateFound(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        return new CreateSellerResult(CreateSellerOutcome.DuplicateFound, null, matches);
    }
}
