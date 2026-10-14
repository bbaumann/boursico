namespace AssociationSaleTool.Web.Data.Entities;

/// <summary>
/// One deposit desk: one computer at one physical queue. Created when a
/// computer opens it, identified by the random token stored in its cookie.
/// </summary>
public sealed class DepositDeskEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public DateTime LastActivityAt { get; set; }

    public List<ReferenceLetterEntity> Letters { get; set; } = [];
}
