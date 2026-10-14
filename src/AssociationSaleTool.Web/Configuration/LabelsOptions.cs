namespace AssociationSaleTool.Web.Configuration;

public sealed class LabelsOptions
{
    public const string SectionName = "Labels";

    /// <summary>
    /// How many reference letters an open deposit desk holds. Each letter's
    /// pre-printed sheets sit on that desk while it is open.
    /// </summary>
    public int LettersPerDesk { get; init; } = 2;
}
