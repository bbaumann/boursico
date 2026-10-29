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
    CashRegisterService cashRegisterService,
    DefectiveItemService defectiveItemService,
    ItemCorrectionService itemCorrectionService,
    DepositDeskService depositDeskService,
    IOptions<AdminOptions> adminOptions) : PageModel
{
    private static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");

    public const string SaleTab = "vente";
    public const string CashRegistersTab = "caisses";
    public const string DesksTab = "lettres";
    public const string ItemCorrectionsTab = "articles";

    public string ActiveTab { get; private set; } = SaleTab;

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

    public IReadOnlyList<ItemCorrectionCandidate> ItemCorrectionCandidates { get; private set; } = [];

    public IReadOnlyList<CashRegisterOption> RefundCashRegisters { get; private set; } = [];

    public IReadOnlyList<CashRegisterAdminRow> CashRegisters { get; private set; } = [];

    public int LabelsPerLetter { get; private set; }

    public IReadOnlyList<ReferenceLetterRow> ReferenceLetters { get; private set; } = [];

    public IReadOnlyList<OpenDeskRow> OpenDesks { get; private set; } = [];

    [BindProperty]
    public string? LabelsPerLetterInput { get; set; }

    [BindProperty]
    public long DeskId { get; set; }

    [BindProperty]
    public PhaseInputModel Input { get; set; } = new();

    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    [BindProperty]
    public CashRegisterInputModel CashRegisterInput { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(string? onglet, CancellationToken cancellationToken)
    {
        ActiveTab = NormalizeTab(onglet);
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
        ActiveTab = SaleTab;
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
        return RedirectToPage(new { onglet = SaleTab });
    }

    public async Task<IActionResult> OnPostSaveCashRegisterAsync(CancellationToken cancellationToken)
    {
        ActiveTab = CashRegistersTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        if (!TryParseCashRegisterInitialCash(CashRegisterInput.InitialCash, out var initialCashCents))
        {
            ModelState.AddModelError(
                "CashRegisterInput.InitialCash",
                "Saisissez un fond de caisse valide en euros.");
            await LoadPageAsync(cancellationToken);
            return Page();
        }

        var result = await cashRegisterService.SaveCashRegisterAsync(
            CashRegisterInput.Id,
            CashRegisterInput.Name,
            initialCashCents,
            CashRegisterInput.IsActive,
            CashRegisterInput.DisplayOrder,
            cancellationToken);

        if (result == CashRegisterSaveResult.Saved)
        {
            SuccessMessage = "Caisse enregistrée.";
            return RedirectToPage(new { onglet = CashRegistersTab });
        }

        ModelState.AddModelError("CashRegisterInput.Name", ToCashRegisterValidationMessage(result));
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveLabelsPerLetterAsync(CancellationToken cancellationToken)
    {
        ActiveTab = DesksTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = int.TryParse(LabelsPerLetterInput, NumberStyles.None, FrenchCulture, out var value)
            ? await depositDeskService.SetLabelsPerLetterAsync(value, cancellationToken)
            : new LabelsPerLetterUpdateResult(LabelsPerLetterUpdateStatus.OutOfRange);

        if (result.Status == LabelsPerLetterUpdateStatus.Saved)
        {
            SuccessMessage = "Nombre d'étiquettes par lettre enregistré.";
            return RedirectToPage(new { onglet = DesksTab });
        }

        ModelState.AddModelError(
            nameof(LabelsPerLetterInput),
            result.Status == LabelsPerLetterUpdateStatus.BelowUsed
                ? $"Le nombre d'étiquettes par lettre ne peut pas être inférieur à {result.HighestUsed}, déjà utilisé pour la lettre {result.Letter}."
                : "Le nombre d'étiquettes par lettre doit être compris entre 1 et 999.");
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostForceCloseDeskAsync(CancellationToken cancellationToken)
    {
        ActiveTab = DesksTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        SuccessMessage = await depositDeskService.CloseDeskAsync(DeskId, cancellationToken)
            ? "Lettres libérées. Elles sont de nouveau disponibles pour les autres ordinateurs."
            : "Ces lettres étaient déjà libérées.";
        return RedirectToPage(new { onglet = DesksTab });
    }

    public async Task<IActionResult> OnPostCorrectSaleLineAsync(
        long saleLineId,
        CancellationToken cancellationToken)
    {
        ActiveTab = ItemCorrectionsTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await saleCorrectionService.CorrectSaleLineAsync(saleLineId, cancellationToken);
        if (result == SaleCorrectionResult.Corrected)
        {
            SuccessMessage = "L'article est remis en stock. La ligne de vente reste marquée comme corrigée.";
            return RedirectToPage(new { onglet = ItemCorrectionsTab });
        }

        var message = result == SaleCorrectionResult.NotFound
            ? "Ligne de vente introuvable."
            : "Cette ligne de vente ne peut pas être corrigée dans son état actuel.";

        ModelState.AddModelError(string.Empty, message);
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRefundDefectiveSaleLineAsync(
        long saleLineId,
        long? refundCashRegisterId,
        CancellationToken cancellationToken)
    {
        ActiveTab = ItemCorrectionsTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await saleCorrectionService.RefundDefectiveSaleLineAsync(saleLineId, refundCashRegisterId, cancellationToken);
        if (result == SaleCorrectionResult.Refunded)
        {
            SuccessMessage = "L'article est marqué défectueux. Remboursez l'acheteur du montant indiqué et rendez l'article au vendeur en fin de vente.";
            return RedirectToPage(new { onglet = ItemCorrectionsTab });
        }

        var message = result switch
        {
            SaleCorrectionResult.NotFound => "Ligne de vente introuvable.",
            SaleCorrectionResult.InvalidCashRegister => "Choisissez une caisse de remboursement existante.",
            _ => "Cette ligne de vente ne peut pas être remboursée dans son état actuel."
        };

        ModelState.AddModelError(string.Empty, message);
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkDefectiveItemAsync(
        long itemId,
        CancellationToken cancellationToken)
    {
        ActiveTab = ItemCorrectionsTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await defectiveItemService.MarkDefectiveAsync(itemId, cancellationToken);
        if (result == DefectiveItemStatusChangeResult.Changed)
        {
            SuccessMessage = "L'article est marqué défectueux. Il ne peut plus être vendu et sera rendu au vendeur.";
            return RedirectToPage(new { onglet = ItemCorrectionsTab });
        }

        var message = result == DefectiveItemStatusChangeResult.NotFound
            ? "Article introuvable."
            : "Cet article ne peut pas être marqué défectueux dans son état actuel.";

        ModelState.AddModelError(string.Empty, message);
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRestoreDefectiveItemAsync(
        long itemId,
        CancellationToken cancellationToken)
    {
        ActiveTab = ItemCorrectionsTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await defectiveItemService.MarkInStockAsync(itemId, cancellationToken);
        if (result == DefectiveItemStatusChangeResult.Changed)
        {
            SuccessMessage = "L'article est remis en stock.";
            return RedirectToPage(new { onglet = ItemCorrectionsTab });
        }

        var message = result == DefectiveItemStatusChangeResult.NotFound
            ? "Article introuvable."
            : "Cet article défectueux ne peut pas être remis en stock.";

        ModelState.AddModelError(string.Empty, message);
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCancelDefectiveRefundAsync(
        long saleLineId,
        CancellationToken cancellationToken)
    {
        ActiveTab = ItemCorrectionsTab;
        if (!UnlockPageIfSessionAllows())
        {
            return Page();
        }

        var result = await saleCorrectionService.CancelDefectiveRefundAsync(saleLineId, cancellationToken);
        if (result == SaleCorrectionResult.RefundCanceled)
        {
            SuccessMessage = "Le remboursement est annulé. L'article est de nouveau marqué vendu.";
            return RedirectToPage(new { onglet = ItemCorrectionsTab });
        }

        var message = result == SaleCorrectionResult.NotFound
            ? "Ligne de vente introuvable."
            : "Ce remboursement ne peut pas être annulé dans son état actuel.";

        ModelState.AddModelError(string.Empty, message);
        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public bool IsActiveTab(string tab)
    {
        return string.Equals(ActiveTab, tab, StringComparison.Ordinal);
    }

    public string TabClass(string tab)
    {
        return IsActiveTab(tab)
            ? "data-list-tab data-list-tab-active"
            : "data-list-tab";
    }

    public string? TabAriaCurrent(string tab)
    {
        return IsActiveTab(tab) ? "page" : null;
    }

    private static string NormalizeTab(string? value)
    {
        return value switch
        {
            CashRegistersTab => CashRegistersTab,
            DesksTab => DesksTab,
            ItemCorrectionsTab => ItemCorrectionsTab,
            _ => SaleTab
        };
    }

    public static string FormatPrice(int priceCents)
    {
        return $"{priceCents / 100m:0.00} €";
    }

    public static string BuildCorrectionFilterText(ItemCorrectionCandidate item)
    {
        return $"{item.Reference} {item.Title} {item.SellerName} {item.Status} {item.SaleId}".ToLower(FrenchCulture);
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        CurrentPhase = await salePhaseService.GetCurrentPhaseAsync(cancellationToken);
        ItemCorrectionCandidates = await itemCorrectionService.ListCandidatesAsync(cancellationToken);
        RefundCashRegisters = await saleCorrectionService.ListRefundCashRegistersAsync(cancellationToken);
        CashRegisters = await cashRegisterService.ListCashRegistersAsync(cancellationToken);
        LabelsPerLetter = await depositDeskService.GetLabelsPerLetterAsync(cancellationToken);
        LabelsPerLetterInput ??= LabelsPerLetter.ToString(FrenchCulture);
        ReferenceLetters = await depositDeskService.ListLettersAsync(cancellationToken);
        OpenDesks = await depositDeskService.ListOpenDesksAsync(cancellationToken);
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

    public sealed class CashRegisterInputModel
    {
        public long? Id { get; set; }

        public string? Name { get; set; }

        public string? InitialCash { get; set; }

        public bool IsActive { get; set; } = true;

        public int? DisplayOrder { get; set; }
    }

    private static bool TryParseCashRegisterInitialCash(string? rawValue, out int cents)
    {
        if (!EuroPriceInput.TryParseToCents(rawValue, out cents))
        {
            return false;
        }

        return cents >= 0;
    }

    private static string ToCashRegisterValidationMessage(CashRegisterSaveResult result)
    {
        return result switch
        {
            CashRegisterSaveResult.NotFound => "Caisse introuvable.",
            CashRegisterSaveResult.NameRequired => "Le nom de la caisse est obligatoire.",
            CashRegisterSaveResult.DuplicateName => "Une caisse porte déjà ce nom.",
            CashRegisterSaveResult.NegativeInitialCash => "Le fond de caisse ne peut pas être négatif.",
            CashRegisterSaveResult.RenameNotAllowed => "Une caisse déjà utilisée ne peut pas être renommée.",
            _ => "La caisse n'a pas pu être enregistrée."
        };
    }
}
