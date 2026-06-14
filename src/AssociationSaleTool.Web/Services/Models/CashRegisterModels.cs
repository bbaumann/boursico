namespace AssociationSaleTool.Web.Services.Models;

public sealed record CashRegisterOption(
    long Id,
    string Name,
    int InitialCashCents);

public sealed record CashRegisterAdminRow(
    long Id,
    string Name,
    int InitialCashCents,
    bool IsActive,
    int DisplayOrder,
    bool HasMovements);

public enum CashRegisterSaveResult
{
    Saved,
    NotFound,
    NameRequired,
    DuplicateName,
    NegativeInitialCash,
    RenameNotAllowed
}
