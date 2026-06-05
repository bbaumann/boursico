using AssociationSaleTool.Web.Data.Entities;

namespace AssociationSaleTool.Web.Services.Models;

public sealed record SellerFormImportParseResult(
    bool Succeeded,
    IReadOnlyList<SellerFormImportSubmission> Submissions,
    IReadOnlyList<string> Errors);

public sealed record SellerFormImportSubmission(
    int SourceRowNumber,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string PostalAddress,
    string PostalCode,
    string City,
    string? Notes,
    string? ImportNotes,
    IReadOnlyList<CreateItemRowRequest> ItemRows,
    IReadOnlyList<SellerFormIgnoredItemLine> IgnoredItemLines,
    IReadOnlyList<SellerFormIncompleteItemLine> IncompleteItemLines)
{
    public IReadOnlyList<CreateItemRowRequest> ValidRows =>
        ItemRows.Where(row => row.SellerPriceCents is not null).ToList();

    public IReadOnlyList<CreateItemRowRequest> IncompleteRows =>
        ItemRows.Where(row => row.SellerPriceCents is null).ToList();
}

public sealed record SellerFormIgnoredItemLine(
    int LineNumber,
    string RawValue,
    string Reason);

public sealed record SellerFormIncompleteItemLine(
    int LineNumber,
    string RawValue,
    string Reason,
    string Title);

public sealed record SellerFormImportResult(
    IReadOnlyList<SellerFormImportRowResult> Rows,
    IReadOnlyList<string> Errors);

public sealed record SellerFormImportRowResult(
    SellerFormImportSubmission Submission,
    SellerFormImportRowOutcome Outcome,
    long? SellerId,
    string? SellerCode,
    IReadOnlyList<SellerDuplicateMatch> DuplicateMatches,
    string? Message)
{
    public bool CanAppendToExistingSeller => Outcome == SellerFormImportRowOutcome.Duplicate
        && DuplicateMatches.Count == 1;
}

public enum SellerFormImportRowOutcome
{
    Imported,
    Duplicate,
    SellerError,
    ImportError
}

public sealed record SellerFormDuplicateAppendResult(
    SellerFormDuplicateAppendOutcome Outcome,
    SellerEntity? Seller,
    int CreatedItemCount,
    int IgnoredExistingItemCount,
    string? Message);

public enum SellerFormDuplicateAppendOutcome
{
    Appended,
    NotFound,
    NotAllowed
}
