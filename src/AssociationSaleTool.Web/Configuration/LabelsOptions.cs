namespace AssociationSaleTool.Web.Configuration;

public sealed class LabelsOptions
{
    public const string SectionName = "Labels";

    /// <summary>
    /// How many references a letter may hold before allocation spills into
    /// another letter. 455 corresponds to the seven sheets of 65 adhesive
    /// labels the association prints per letter, but the application never
    /// reasons about sheets - only about this number.
    /// </summary>
    public int SoftCapacityPerLetter { get; init; } = 455;
}
