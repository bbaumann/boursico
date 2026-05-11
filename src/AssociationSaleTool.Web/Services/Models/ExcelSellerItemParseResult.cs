namespace AssociationSaleTool.Web.Services.Models;

public sealed record ExcelSellerItemParseResult(
    bool Succeeded,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string PostalAddress,
    string? PostalCode,
    string? City,
    string? Notes,
    IntakeMode IntakeMode,
    IReadOnlyList<CreateItemRowRequest> Rows,
    IReadOnlyList<string> Errors)
{
    public static ExcelSellerItemParseResult Success(
        string firstName,
        string lastName,
        string? email,
        string? phone,
        string postalAddress,
        string? postalCode,
        string? city,
        string? notes,
        IntakeMode intakeMode,
        IReadOnlyList<CreateItemRowRequest> rows)
    {
        return new ExcelSellerItemParseResult(
            true,
            firstName,
            lastName,
            email,
            phone,
            postalAddress,
            postalCode,
            city,
            notes,
            intakeMode,
            rows,
            []);
    }

    public static ExcelSellerItemParseResult Failure(params string[] errors)
    {
        return new ExcelSellerItemParseResult(
            false,
            string.Empty,
            string.Empty,
            null,
            null,
            string.Empty,
            null,
            null,
            null,
            IntakeMode.EmailPreregistration,
            [],
            errors);
    }

    public static ExcelSellerItemParseResult Failure(IReadOnlyList<string> errors)
    {
        return new ExcelSellerItemParseResult(
            false,
            string.Empty,
            string.Empty,
            null,
            null,
            string.Empty,
            null,
            null,
            null,
            IntakeMode.EmailPreregistration,
            [],
            errors);
    }
}
