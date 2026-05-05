using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateSellerWithItemsResult(
    CreateSellerWithItemsOutcome Outcome,
    SellerEntity? Seller,
    IReadOnlyList<SellerDuplicateMatch> DuplicateMatches)
{
    public static CreateSellerWithItemsResult Created(SellerEntity seller)
    {
        return new CreateSellerWithItemsResult(CreateSellerWithItemsOutcome.Created, seller, []);
    }

    public static CreateSellerWithItemsResult ExistingSellerFound(IReadOnlyList<SellerDuplicateMatch> matches)
    {
        return new CreateSellerWithItemsResult(CreateSellerWithItemsOutcome.ExistingSellerFound, null, matches);
    }
}

public enum CreateSellerWithItemsOutcome
{
    Created,
    ExistingSellerFound
}
