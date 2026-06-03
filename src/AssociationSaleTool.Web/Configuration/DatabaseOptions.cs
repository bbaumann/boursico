namespace AssociationSaleTool.Web.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string? ActiveProfile { get; init; }

    public string SqlitePath { get; init; } = string.Empty;

    public Dictionary<string, string?> Profiles { get; init; } = [];

    public string ResolveSqlitePath()
    {
        if (string.IsNullOrWhiteSpace(ActiveProfile))
        {
            return SqlitePath;
        }

        var profile = ActiveProfile.Trim();
        if (Profiles.TryGetValue(profile, out var profilePath) && !string.IsNullOrWhiteSpace(profilePath))
        {
            return profilePath;
        }

        throw new InvalidOperationException($"Database profile '{profile}' is not configured.");
    }

    public bool HasResolvedSqlitePath()
    {
        if (string.IsNullOrWhiteSpace(ActiveProfile))
        {
            return !string.IsNullOrWhiteSpace(SqlitePath);
        }

        return Profiles.TryGetValue(ActiveProfile.Trim(), out var profilePath)
            && !string.IsNullOrWhiteSpace(profilePath);
    }
}
