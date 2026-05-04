using System.Globalization;
using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Pages.SaleStatus;

public sealed class IndexModel(
    SalePhaseService salePhaseService,
    SaleCorrectionService saleCorrectionService,
    IOptions<AdminOptions> adminOptions) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public bool IsUnlocked { get; private set; }

    public SalePhase CurrentPhase { get; private set; }

    public string CurrentPhaseLabel => SalePhaseDisplay.ToFrenchLabel(CurrentPhase);

    public string CurrentPhaseDescription => CurrentPhase switch
    {
        SalePhase.Presale =>
            "Dans ce statut, les informations des vendeurs et des jouets sont modifiables. Les ventes restent possibles.",
        SalePhase.Sale =>
            "Dans ce statut, les informations des vendeurs et des jouets ne sont pas modifiables. Les ventes restent possibles.",
        SalePhase.PostSale =>
            "Dans ce statut, les ventes sont fermées. Les informations des vendeurs et des jouets ne sont pas modifiables, sauf pour marquer un jouet manquant pendant le rapprochement.",
        _ => throw new ArgumentOutOfRangeException(nameof(CurrentPhase), CurrentPhase, null)
    };

    public IReadOnlyList<SaleCorrectionItem> ActiveSoldItems { get; private set; } = [];

    [BindProperty]
    public PhaseInputModel Input { get; set; } = new();

    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        IsUnlocked = SaleStatusAdminSession.IsUnlocked(HttpContext);

        if (IsUnlocked)
        {
            await LoadPageAsync(cancellationToken);
        }
    }

    public Task<IActionResult> OnPostUnlockAsync()
    {
        if (PasswordInput.Password == adminOptions.Value.SharedPassword)
        {
            SaleStatusAdminSession.Unlock(HttpContext);
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        IsUnlocked = false;
        ModelState.AddModelError("PasswordInput.Password", "Mot de passe administrateur incorrect.");

        return Task.FromResult<IActionResult>(Page());
    }

    public async Task<IActionResult> OnPostChangePhaseAsync(CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        if (!SalePhaseService.TryParseStorageValue(Input.Phase, out var phase))
        {
            ModelState.AddModelError("Input.Phase", "Statut de vente invalide.");
            await LoadPageAsync(cancellationToken);
            return Page();
        }

        await salePhaseService.SetPhaseAsync(phase, cancellationToken);
        SuccessMessage = $"Statut de vente modifié : {SalePhaseDisplay.ToFrenchLabel(phase)}.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCorrectSaleLineAsync(
        long saleLineId,
        CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await saleCorrectionService.CorrectSaleLineAsync(saleLineId, cancellationToken);
        if (result == SaleCorrectionResult.Corrected)
        {
            SuccessMessage = "L'article est remis en stock. La ligne de vente reste marquée comme corrigée.";
            return RedirectToPage();
        }

        var message = result == SaleCorrectionResult.NotFound
            ? "Ligne de vente introuvable."
            : "Cette ligne de vente ne peut pas être corrigée dans son état actuel.";

        ModelState.AddModelError(string.Empty, message);
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} EUR";
    }

    public static string FormatSoldAt(DateTime soldAt)
    {
        return soldAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", FrenchCulture);
    }

    public static string BuildCorrectionFilterText(SaleCorrectionItem item)
    {
        return $"{item.Reference} {item.Title} {item.SellerCode} {item.SellerName} {item.SaleId}".ToLower(FrenchCulture);
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        CurrentPhase = await salePhaseService.GetCurrentPhaseAsync(cancellationToken);
        ActiveSoldItems = await saleCorrectionService.ListActiveSoldItemsAsync(cancellationToken);
    }

    private bool UnlockPageIfSessionAllows()
    {
        IsUnlocked = SaleStatusAdminSession.IsUnlocked(HttpContext);

        return IsUnlocked;
    }

    public sealed class PhaseInputModel
    {
        public string? Phase { get; set; }
    }

    public sealed class PasswordInputModel
    {
        public string? Password { get; set; }
    }
}
