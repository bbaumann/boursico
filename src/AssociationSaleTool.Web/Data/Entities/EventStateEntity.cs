namespace AssociationSaleTool.Web.Data.Entities;

public sealed class EventStateEntity
{
    public int Id { get; set; }

    public string SalePhase { get; set; } = string.Empty;

    public int LabelsPerLetter { get; set; } = 455;

    public DateTime UpdatedAt { get; set; }
}
