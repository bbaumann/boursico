using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class IndexModel(IntakeService intakeService) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public IReadOnlyList<SellerEntity> Sellers { get; private set; } = [];

    public bool CanEditSellerMasterData { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Sellers = await intakeService.ListSellersAsync(cancellationToken);
        CanEditSellerMasterData = await intakeService.CanEditSellerMasterDataAsync(cancellationToken);
    }

    public static string BuildSellerFilterText(SellerEntity seller)
    {
        return $"{seller.SellerCode} {seller.FirstName} {seller.LastName}".ToLower(FrenchCulture);
    }
}
