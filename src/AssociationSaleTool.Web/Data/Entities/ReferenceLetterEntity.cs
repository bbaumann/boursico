namespace AssociationSaleTool.Web.Data.Entities;

/// <summary>
/// A reference letter's counter and current holder. LastNumber only ever
/// increases, so a number whose label was already peeled is never handed out
/// twice, even after its item is deleted.
/// </summary>
public sealed class ReferenceLetterEntity
{
    public string Letter { get; set; } = string.Empty;

    public int LastNumber { get; set; }

    public long? HeldByDeskId { get; set; }

    public DepositDeskEntity? HeldByDesk { get; set; }
}
