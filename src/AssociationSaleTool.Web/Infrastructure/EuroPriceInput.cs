using System.Globalization;

namespace AssociationSaleTool.Web.Infrastructure;

public static class EuroPriceInput
{
    public static bool TryParseToCents(string? rawValue, out int cents)
    {
        cents = 0;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim().Replace(',', '.');

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var euros))
        {
            return false;
        }

        var roundedEuros = Math.Round(euros, 2, MidpointRounding.AwayFromZero);
        var roundedCents = decimal.Round(roundedEuros * 100m, 0, MidpointRounding.AwayFromZero);

        if (roundedCents is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        cents = (int)roundedCents;
        return true;
    }

    public static string FormatFromCents(int cents)
    {
        return (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
