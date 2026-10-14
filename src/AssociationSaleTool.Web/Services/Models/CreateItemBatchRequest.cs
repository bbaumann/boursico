namespace AssociationSaleTool.Web.Services.Models;

public sealed record CreateItemBatchRequest(
    long SellerId,
    IntakeMode IntakeMode,
    IReadOnlyList<CreateItemRowRequest> Rows,
    long? DepositDeskId = null);
