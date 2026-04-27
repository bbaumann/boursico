namespace AssociationSaleTool.Web.Infrastructure;

public static class ItemStatusDisplay
{
    public static string ToFrenchLabel(string status)
    {
        return status switch
        {
            "registered" => "enregistré",
            "in_stock" => "en stock",
            "not_provided" => "non fourni",
            "sold" => "vendu",
            "missing" => "manquant",
            _ => status
        };
    }
}
