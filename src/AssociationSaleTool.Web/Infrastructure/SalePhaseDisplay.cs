using AssociationSaleTool.Web.Services;

namespace AssociationSaleTool.Web.Infrastructure;

public static class SalePhaseDisplay
{
    public static string ToFrenchLabel(SalePhase phase)
    {
        return phase switch
        {
            SalePhase.Presale => "Prévente",
            SalePhase.Sale => "Vente",
            SalePhase.PostSale => "Après-vente",
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
        };
    }
}
