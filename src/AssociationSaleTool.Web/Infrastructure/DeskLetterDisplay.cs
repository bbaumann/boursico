namespace AssociationSaleTool.Web.Infrastructure;

/// <summary>
/// French texts for the labels a desk letter has left, agreeing with the
/// count: every letter passes through 1 left, and a lowered setting can leave
/// a held letter at 0.
/// </summary>
public static class DeskLetterDisplay
{
    public static string LowLabelsWarning(char letter, int labelsLeft)
    {
        return labelsLeft switch
        {
            <= 0 => $"Plus aucune étiquette en {letter}.",
            1 => $"Plus que 1 étiquette en {letter}.",
            _ => $"Plus que {labelsLeft} étiquettes en {letter}."
        };
    }

    public static string LabelsLeft(int labelsLeft)
    {
        return labelsLeft switch
        {
            <= 0 => "aucune étiquette restante",
            1 => "1 étiquette restante",
            _ => $"{labelsLeft} étiquettes restantes"
        };
    }
}
