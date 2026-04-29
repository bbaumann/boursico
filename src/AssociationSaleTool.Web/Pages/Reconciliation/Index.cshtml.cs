using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace AssociationSaleTool.Web.Pages.Reconciliation;

public sealed class IndexModel(
    ReconciliationService reconciliationService,
    IOptions<AdminOptions> adminOptions) : PageModel
{
    public bool IsUnlocked { get; private set; }

    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    [BindProperty]
    public SheetInputModel SheetInput { get; set; } = new();

    [BindProperty]
    public SearchInputModel SearchInput { get; set; } = new();

    public IReadOnlyList<ReconciliationItemSearchResult> SearchResults { get; private set; } = [];

    public string? SuccessMessage { get; private set; }

    public Task OnGetAsync(CancellationToken cancellationToken)
    {
        IsUnlocked = ReconciliationAdminSession.IsUnlocked(HttpContext);

        return Task.CompletedTask;
    }

    public Task<IActionResult> OnPostUnlockAsync()
    {
        if (PasswordInput.Password == adminOptions.Value.SharedPassword)
        {
            ReconciliationAdminSession.Unlock(HttpContext);
            return Task.FromResult<IActionResult>(RedirectToPage());
        }

        IsUnlocked = false;
        ModelState.AddModelError("PasswordInput.Password", "Mot de passe administrateur incorrect.");

        return Task.FromResult<IActionResult>(Page());
    }

    public async Task<IActionResult> OnPostSearchAsync(CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        if (string.IsNullOrWhiteSpace(SearchInput.Query))
        {
            ModelState.AddModelError("SearchInput.Query", "Saisissez une référence ou un titre.");
            return Page();
        }

        SearchResults = await reconciliationService.SearchItemsAsync(SearchInput.Query, cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostMarkMissingAsync(
        long itemId,
        string? sellerCode,
        CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await reconciliationService.MarkItemMissingAsync(itemId, cancellationToken);
        ApplyStatusChangeResult(
            result,
            "Article marqué manquant.",
            "L'article ne peut pas être marqué manquant dans son état actuel.",
            sellerCode);

        return Page();
    }

    public async Task<IActionResult> OnPostMarkInStockAsync(
        long itemId,
        string? sellerCode,
        CancellationToken cancellationToken)
    {
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await reconciliationService.MarkItemInStockAsync(itemId, cancellationToken);
        ApplyStatusChangeResult(
            result,
            "Article remis en stock.",
            "L'article ne peut pas être remis en stock dans son état actuel.",
            sellerCode);

        return Page();
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} EUR";
    }

    private bool UnlockPageIfSessionAllows()
    {
        IsUnlocked = ReconciliationAdminSession.IsUnlocked(HttpContext);

        return IsUnlocked;
    }

    private void ApplyStatusChangeResult(
        ReconciliationStatusChangeResult result,
        string successMessage,
        string notAllowedMessage,
        string? sellerCode)
    {
        if (result == ReconciliationStatusChangeResult.Changed)
        {
            SuccessMessage = successMessage;
            SheetInput.SellerCode = sellerCode?.Trim().ToUpperInvariant();
            return;
        }

        var message = result == ReconciliationStatusChangeResult.NotFound
            ? "Article introuvable."
            : notAllowedMessage;

        ModelState.AddModelError(string.Empty, message);
    }

    public sealed class PasswordInputModel
    {
        public string? Password { get; set; }
    }

    public sealed class SheetInputModel
    {
        public string? SellerCode { get; set; }
    }

    public sealed class SearchInputModel
    {
        public string? Query { get; set; }
    }
}
