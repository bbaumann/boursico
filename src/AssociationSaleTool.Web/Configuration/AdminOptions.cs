namespace AssociationSaleTool.Web.Configuration;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string SharedPassword { get; init; } = string.Empty;
}
