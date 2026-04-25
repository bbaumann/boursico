namespace AssociationSaleTool.Web.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string SqlitePath { get; init; } = string.Empty;
}
