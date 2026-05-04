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

        if (euros != decimal.Truncate(euros))
        {
            return false;
        }

        var centsValue = euros * 100m;

        if (centsValue is < int.MinValue or > int.MaxValue)
        {
            return false;
        }

        cents = (int)centsValue;
        return true;
    }

    public static string FormatFromCents(int cents)
    {
        if (cents % 100 == 0)
        {
            return (cents / 100).ToString("0", CultureInfo.InvariantCulture);
        }

        return (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }
}
