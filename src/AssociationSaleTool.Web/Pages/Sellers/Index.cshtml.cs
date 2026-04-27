using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssociationSaleTool.Web.Pages.Sellers;

public sealed class IndexModel(IntakeService intakeService) : PageModel
{
    public IReadOnlyList<SellerEntity> Sellers { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Sellers = await intakeService.ListSellersAsync(cancellationToken);
    }
}
