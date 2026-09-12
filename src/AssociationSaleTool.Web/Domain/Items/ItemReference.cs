namespace AssociationSaleTool.Web.Domain.Items;

public static class ItemReference
{
    /// <summary>
    /// Formats a reference as letter + three zero-padded digits, for example
    /// "B157". The padding is load-bearing: ORDER BY reference must produce the
    /// same order as the physical label sheets.
    /// </summary>
    public static string Format(char letter, int number) => $"{letter}{number:000}";
}
