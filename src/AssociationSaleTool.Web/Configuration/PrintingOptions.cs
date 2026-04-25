namespace AssociationSaleTool.Web.Configuration;

public sealed class PrintingOptions
{
    public const string SectionName = "Printing";

    public string ReceiptHeaderText { get; init; } = string.Empty;
}
