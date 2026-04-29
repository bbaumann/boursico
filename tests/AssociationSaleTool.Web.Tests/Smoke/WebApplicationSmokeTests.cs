using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssociationSaleTool.Web.Tests.Smoke;

public class WebApplicationSmokeTests : IClassFixture<IntakeWebApplicationFactory>
{
    private readonly IntakeWebApplicationFactory _factory;

    public WebApplicationSmokeTests(IntakeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HomePageLoadsSuccessfully()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SellersIndexLoadsSuccessfully()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Vendeurs", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SellersPageShowsMainNavigationWithSellersActive()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive("/vendeurs", content);
    }

    [Fact]
    public async Task CheckoutPageShowsMainNavigationWithCheckoutActive()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/caisse");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive("/caisse", content);
    }

    [Fact]
    public async Task ReconciliationPageShowsMainNavigationWithReconciliationActive()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/rapprochement");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive("/rapprochement", content);
    }

    [Fact]
    public async Task ReconciliationSheetsPageShowsMainNavigationWithReconciliationActive()
    {
        var (client, _) = await UnlockReconciliationDashboardAsync();
        var response = await client.GetAsync("/rapprochement/feuilles");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive("/rapprochement", content);
    }

    [Fact]
    public async Task ReconciliationPageShowsAdminGateWhenLocked()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/rapprochement");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Rapprochement", content);
        Assert.Contains("Mot de passe administrateur", content);
        Assert.Contains("Déverrouiller", content);
        Assert.DoesNotContain("Correction des articles", content);
    }

    [Fact]
    public async Task ReconciliationUnlockRejectsInvalidAdminPasswordInFrench()
    {
        var client = _factory.CreateClient();
        var gate = await client.GetAsync("/rapprochement");
        var gateContent = await gate.Content.ReadAsStringAsync();

        gate.EnsureSuccessStatusCode();

        var response = await client.PostAsync(
            "/rapprochement?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "wrong-password")));

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Mot de passe administrateur", content);
        Assert.Contains("Mot de passe administrateur incorrect.", content);
        Assert.DoesNotContain("Correction des articles", content);
    }

    [Fact]
    public async Task ReconciliationUnlockShowsDashboardWithSellerSheetAndCorrectionSections()
    {
        var (client, content) = await UnlockReconciliationDashboardAsync();

        Assert.Contains("Feuilles vendeurs", content);
        Assert.Contains("Correction des articles", content);
        Assert.Contains("href=\"/rapprochement/feuilles\"", content);
        Assert.Contains("Imprimer toutes les feuilles", content);
        Assert.Contains("Code vendeur", content);
        Assert.Contains("Imprimer une feuille", content);
        Assert.Contains("Référence ou titre", content);
        Assert.Contains("Rechercher", content);

        await EnsureSoldItemsHaveSaleLinesAsync();

        var sheetsResponse = await client.GetAsync("/rapprochement/feuilles");
        sheetsResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReconciliationSheetsRedirectsToAdminGateWhenLocked()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/rapprochement/feuilles");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/rapprochement", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ReconciliationLockedPostHandlersShowAdminGateAndDoNotChangeItemStatus()
    {
        var client = _factory.CreateClient();
        long inStockItemId;
        long missingItemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RL-001",
                FirstName = "Lea",
                LastName = "Verrou",
                Email = "lea.verrou@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RL-001-001",
                ItemNumber = 1,
                Title = "Article verrouille",
                SellerPriceCents = 500,
                Status = "in_stock"
            };
            var missingItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RL-001-002",
                ItemNumber = 2,
                Title = "Article deja manquant",
                SellerPriceCents = 600,
                Status = "missing"
            };

            dbContext.Items.AddRange(inStockItem, missingItem);
            await dbContext.SaveChangesAsync();

            inStockItemId = inStockItem.Id;
            missingItemId = missingItem.Id;
        }

        var searchResponse = await client.PostAsync(
            "/rapprochement?handler=Search",
            BuildFormContent(await GetReconciliationGateAsync(client), ("SearchInput.Query", "RL-001")));
        var markMissingResponse = await client.PostAsync(
            $"/rapprochement?handler=MarkMissing&itemId={inStockItemId}&sellerCode=RL-001",
            BuildFormContent(await GetReconciliationGateAsync(client)));
        var markInStockResponse = await client.PostAsync(
            $"/rapprochement?handler=MarkInStock&itemId={missingItemId}&sellerCode=RL-001",
            BuildFormContent(await GetReconciliationGateAsync(client)));

        foreach (var response in new[] { searchResponse, markMissingResponse, markInStockResponse })
        {
            response.EnsureSuccessStatusCode();
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Mot de passe administrateur", content);
            Assert.Contains("Déverrouiller", content);
            Assert.DoesNotContain("Correction des articles", content);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("in_stock", (await dbContext.Items.SingleAsync(x => x.Id == inStockItemId)).Status);
            Assert.Equal("missing", (await dbContext.Items.SingleAsync(x => x.Id == missingItemId)).Status);
        }
    }

    [Fact]
    public async Task ReconciliationDashboardSearchShowsFrenchStatusesAndCorrectionActions()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RC-001",
                FirstName = "Renee",
                LastName = "Controle",
                Email = "renee.controle@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.AddRange(
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "RC-001-001",
                    ItemNumber = 1,
                    Title = "Puzzle rapprochement",
                    SellerPriceCents = 450,
                    Status = "in_stock"
                },
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "RC-001-002",
                    ItemNumber = 2,
                    Title = "Train rapprochement",
                    SellerPriceCents = 800,
                    Status = "missing"
                });

            await dbContext.SaveChangesAsync();
        }

        var dashboardContent = await UnlockReconciliationDashboardAsync(client);
        var response = await client.PostAsync(
            "/rapprochement?handler=Search",
            BuildFormContent(dashboardContent, ("SearchInput.Query", "RC-001")));

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("RC-001-001", content);
        Assert.Contains("Puzzle rapprochement", content);
        Assert.Contains("en stock", content);
        Assert.Contains("RC-001-002", content);
        Assert.Contains("Train rapprochement", content);
        Assert.Contains("manquant", content);
        Assert.Contains("Marquer manquant", content);
        Assert.Contains("Remettre en stock", content);
    }

    [Fact]
    public async Task ReconciliationDashboardCanMarkItemMissingWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RC-002",
                FirstName = "Marc",
                LastName = "Manquant",
                Email = "marc.manquant@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RC-002-001",
                ItemNumber = 1,
                Title = "Camion rapprochement",
                SellerPriceCents = 700,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var dashboardContent = await UnlockReconciliationDashboardAsync(client);
        var response = await client.PostAsync(
            "/rapprochement?handler=MarkMissing&sellerCode=RC-002",
            BuildFormContent(dashboardContent, ("itemId", itemId.ToString())));

        response.EnsureSuccessStatusCode();
        Assert.Equal("/rapprochement", response.RequestMessage?.RequestUri?.PathAndQuery);

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Article marqué manquant.", content);
        Assert.Contains("href=\"/rapprochement/feuilles?codeVendeur=RC-002\"", content);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = await dbContext.Items.SingleAsync(x => x.Id == itemId);
            Assert.Equal("missing", item.Status);
        }
    }

    [Fact]
    public async Task ReconciliationDashboardCanMarkItemInStockWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RC-003",
                FirstName = "Ines",
                LastName = "Stock",
                Email = "ines.stock@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RC-003-001",
                ItemNumber = 1,
                Title = "Jeu retrouve",
                SellerPriceCents = 900,
                Status = "missing"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var dashboardContent = await UnlockReconciliationDashboardAsync(client);
        var response = await client.PostAsync(
            "/rapprochement?handler=MarkInStock&sellerCode=RC-003",
            BuildFormContent(dashboardContent, ("itemId", itemId.ToString())));

        response.EnsureSuccessStatusCode();
        Assert.Equal("/rapprochement", response.RequestMessage?.RequestUri?.PathAndQuery);

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Article remis en stock.", content);
        Assert.Contains("href=\"/rapprochement/feuilles?codeVendeur=RC-003\"", content);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = await dbContext.Items.SingleAsync(x => x.Id == itemId);
            Assert.Equal("in_stock", item.Status);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageRendersAllActiveSellerSheetsWhenUnlocked()
    {
        var (client, _) = await UnlockReconciliationDashboardAsync();
        await SeedReconciliationSheetAsync(
            "RS-101",
            "Alice",
            "Feuille",
            "alice.feuille@example.test",
            "0600000001",
            ("RS-101-001", "Jeu manquant", "missing", 900),
            ("RS-101-002", "Puzzle a rendre", "in_stock", 450));
        await SeedReconciliationSheetAsync(
            "RS-102",
            "Bruno",
            "Feuille",
            null,
            null,
            ("RS-102-001", "Train a rendre", "in_stock", 800));
        await EnsureSoldItemsHaveSaleLinesAsync();

        var response = await client.GetAsync("/rapprochement/feuilles");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("seller-sheet-print-page", content);
        Assert.Contains("Feuille vendeur", content);
        Assert.Contains("RS-101", content);
        Assert.Contains("Alice Feuille", content);
        Assert.Contains("alice.feuille@example.test", content);
        Assert.Contains("Articles à payer", content);
        Assert.Contains("Jeu manquant", content);
        Assert.Contains("MANQUANT", content);
        Assert.Contains("Articles à rendre", content);
        Assert.Contains("Puzzle a rendre", content);
        Assert.Contains("Total à payer", content);
        Assert.Contains("RS-102", content);
        Assert.Contains("Bruno Feuille", content);
    }

    [Fact]
    public async Task ReconciliationSheetsPageFiltersBySellerCodeWhenUnlocked()
    {
        var (client, _) = await UnlockReconciliationDashboardAsync();
        await SeedReconciliationSheetAsync(
            "RS-201",
            "Claire",
            "Filtre",
            null,
            null,
            ("RS-201-001", "Article cible", "missing", 300));
        await SeedReconciliationSheetAsync(
            "RS-202",
            "Denis",
            "Filtre",
            null,
            null,
            ("RS-202-001", "Article autre vendeur", "missing", 400));

        var response = await client.GetAsync("/rapprochement/feuilles?codeVendeur=RS-201");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("RS-201", content);
        Assert.Contains("Article cible", content);
        Assert.DoesNotContain("RS-202", content);
        Assert.DoesNotContain("Article autre vendeur", content);
    }

    [Fact]
    public async Task ReconciliationSheetsPageShowsFrenchMessageForUnknownSellerCode()
    {
        var (client, _) = await UnlockReconciliationDashboardAsync();

        var response = await client.GetAsync("/rapprochement/feuilles?codeVendeur=RS-404");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Aucune feuille vendeur pour le code RS-404.", content);
    }

    [Fact]
    public async Task ReconciliationSheetsPageShowsFrenchErrorForSoldItemWithoutSaleLine()
    {
        var (client, _) = await UnlockReconciliationDashboardAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RS-500",
                FirstName = "Erreur",
                LastName = "Vente",
                Email = "erreur.vente@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RS-500-001",
                ItemNumber = 1,
                Title = "Article vendu incoherent",
                SellerPriceCents = 1200,
                Status = "sold"
            });

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/rapprochement/feuilles?codeVendeur=RS-500");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Données de vente incohérentes pour l'article RS-500-001.", content);
        Assert.Contains("La feuille vendeur ne peut pas être imprimée.", content);
        Assert.DoesNotContain("Total à payer", content);
    }

    [Fact]
    public async Task ReconciliationPrintCssContainsSellerSheetPrintRules()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(".no-print", content);
        Assert.Contains("body:has(.seller-sheets-print-root) .main-navigation", content);
        Assert.Contains("body:has(.seller-sheets-print-root) .page-frame", content);
        Assert.Contains("body:has(.seller-sheets-print-root) .page-shell", content);
        Assert.Contains(".seller-sheet-print-page", content);
        Assert.Contains("page-break-after: always;", content);
        Assert.Contains(".seller-sheet-print-page:last-child", content);
    }

    [Fact]
    public async Task SellerCreatePageLoadsSuccessfully()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs/creer");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Créer un vendeur", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SellerDetailsDisplaysFrenchItemStatusLabels()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-001",
                FirstName = "Claire",
                LastName = "Martin",
                Email = "claire@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-001-001",
                ItemNumber = 1,
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            });

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("enregistré", content);
        Assert.DoesNotContain(">registered<", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysFrenchIntakeModeLabels()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-002",
                FirstName = "Nina",
                LastName = "Robert",
                Email = "nina@example.test"
            };

            dbContext.Sellers.Add(seller);

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(">Dépot<", content);
        Assert.Contains(">Enregistrement<", content);
        Assert.DoesNotContain(">InPerson<", content);
        Assert.DoesNotContain(">EmailPreregistration<", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysIntakeModeAsRadioButtons()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-004",
                FirstName = "Marie",
                LastName = "Petit",
                Email = "marie@example.test"
            };

            dbContext.Sellers.Add(seller);

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("type=\"radio\"", content);
        Assert.Contains("name=\"Input.IntakeMode\"", content);
        Assert.Contains("value=\"InPerson\"", content);
        Assert.Contains("value=\"EmailPreregistration\"", content);
        Assert.DoesNotContain("<select", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysTenDefaultRegistrationRowsAndAddRowButton()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-006",
                FirstName = "Jeanne",
                LastName = "Moreau",
                Email = "jeanne@example.test"
            };

            dbContext.Sellers.Add(seller);

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Input.Rows[9].Title\"", content);
        Assert.DoesNotContain("name=\"Input.Rows[10].Title\"", content);
        Assert.Contains("data-item-registration-table", content);
        Assert.Contains("data-add-item-row", content);
        Assert.Contains("aria-label=\"Ajouter une ligne\"", content);
    }

    [Fact]
    public async Task SellerDetailsLoadsAddRowScript()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-007",
                FirstName = "Alice",
                LastName = "Lemoine",
                Email = "alice@example.test"
            };

            dbContext.Sellers.Add(seller);

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("/js/site.js", content);
    }

    [Fact]
    public async Task SiteCssPlacesAddRowButtonOnLeft()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(".item-registration-actions", content);
        Assert.Contains("justify-content: flex-start;", content);
        Assert.Contains(".button-link:not(:disabled)", content);
        Assert.Contains("cursor: pointer;", content);
        Assert.Contains(".button-link:disabled", content);
        Assert.Contains("cursor: not-allowed;", content);
        Assert.Contains(".main-navigation", content);
        Assert.Contains(".main-navigation-link", content);
        Assert.Contains(".main-navigation-link-active", content);
    }

    [Fact]
    public async Task SiteJsContainsCheckoutCatalogInteraction()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/js/site.js");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("initializeCheckout", content);
        Assert.Contains("data-checkout-root", content);
        Assert.Contains("Cet article est déjà dans le panier.", content);
        Assert.Contains("data-checkout-remove", content);
        Assert.Contains("/api/caisse/validation", content);
        Assert.Contains("/api/ventes", content);
        Assert.Contains("checkout-conflict-row", content);
        Assert.Contains("receiptDetails", content);
        Assert.Contains("data-checkout-save-print", content);
        Assert.Contains("data-checkout-receipt-lines", content);
        Assert.Contains("window.print()", content);
    }

    [Fact]
    public async Task SiteCssContainsCheckoutCatalogLayout()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(".checkout-grid", content);
        Assert.Contains("grid-template-columns: minmax(0, 1fr);", content);
        Assert.DoesNotContain("minmax(320px", content);
        Assert.Contains(".checkout-results-frame", content);
        Assert.Contains("max-height: min(52vh, 32rem);", content);
        Assert.Contains("overflow-y: auto;", content);
        Assert.Contains(".checkout-results-table", content);
        Assert.Contains(".checkout-cart-panel", content);
        Assert.Contains(".checkout-totals", content);
        Assert.Contains(".checkout-modal", content);
        Assert.Contains(".checkout-conflict-row", content);
        Assert.Contains(".checkout-print-receipt", content);
        Assert.Contains("@media print", content);
        Assert.Contains("body.checkout-printing", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysDeleteActionWithConfirmationForDeletableItemsOnly()
    {
        var client = _factory.CreateClient();
        long sellerId;
        long registeredItemId;
        long soldItemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-005",
                FirstName = "Sophie",
                LastName = "Durand",
                Email = "sophie@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var registeredItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-005-001",
                ItemNumber = 1,
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            };
            var soldItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-005-002",
                ItemNumber = 2,
                Title = "Camion",
                SellerPriceCents = 700,
                Status = "sold"
            };

            dbContext.Items.AddRange(registeredItem, soldItem);
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
            registeredItemId = registeredItem.Id;
            soldItemId = soldItem.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Supprimer", content);
        Assert.Contains("Voulez-vous vraiment supprimer cet article ?", content);
        Assert.Contains($"itemId={registeredItemId}", content);
        Assert.DoesNotContain($"itemId={soldItemId}", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysMarkInStockActionForRegisteredItemsOnly()
    {
        var client = _factory.CreateClient();
        long sellerId;
        long registeredItemId;
        long inStockItemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-008",
                FirstName = "Laura",
                LastName = "Girard",
                Email = "laura@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var registeredItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-008-001",
                ItemNumber = 1,
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            };
            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-008-002",
                ItemNumber = 2,
                Title = "Camion",
                SellerPriceCents = 700,
                Status = "in_stock"
            };

            dbContext.Items.AddRange(registeredItem, inStockItem);
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
            registeredItemId = registeredItem.Id;
            inStockItemId = inStockItem.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Marquer en stock", content);
        Assert.Contains("handler=MarkItemInStock", content);
        Assert.Contains($"itemId={registeredItemId}", content);
        Assert.DoesNotContain($"handler=MarkItemInStock&amp;itemId={inStockItemId}", content);
    }

    [Fact]
    public async Task SellerDetailsUsesNarrowPriceColumnsAndWideTitleColumns()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-003",
                FirstName = "Lucie",
                LastName = "Bernard",
                Email = "lucie@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-003-001",
                ItemNumber = 1,
                Title = "Grand coffret de construction",
                SellerPriceCents = 1200,
                Status = "registered"
            });

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("seller-items-title-column", content);
        Assert.Contains("seller-items-price-column", content);
        Assert.Contains("seller-intake-title-column", content);
        Assert.Contains("seller-intake-price-column", content);
    }

    [Fact]
    public async Task CheckoutPageLoadsCatalogShellInFrench()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "CK-001",
                FirstName = "Nora",
                LastName = "Bernier",
                Email = "nora@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CK-001-001",
                ItemNumber = 1,
                Title = "Jeu de cubes",
                SellerPriceCents = 900,
                Status = "in_stock"
            });

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/caisse");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Caisse", content);
        Assert.Contains("Rechercher un article", content);
        Assert.Contains("Panier", content);
        Assert.Contains("Sous-total", content);
        Assert.Contains("Frais association familiale", content);
        Assert.DoesNotContain("Frais de caisse", content);
        Assert.Contains("Total", content);
        Assert.Contains("checkout-catalog-data", content);
        Assert.Contains("data-checkout-root", content);
        Assert.Contains("data-checkout-search", content);
        Assert.Contains("data-checkout-results", content);
        Assert.Contains("data-checkout-cart", content);
        Assert.Contains("data-checkout-subtotal", content);
        Assert.Contains("data-checkout-finalize", content);
        Assert.Contains("Finaliser", content);
        Assert.Contains("data-checkout-modal", content);
        Assert.Contains("data-checkout-modal-count", content);
        Assert.Contains("data-checkout-save-sale", content);
        Assert.Contains("Enregistrer la vente", content);
        Assert.Contains("data-checkout-receipt-details", content);
        Assert.Contains("Détails du reçu", content);
        Assert.Contains("data-checkout-save-print", content);
        Assert.Contains("Enregistrer et imprimer", content);
        Assert.Contains("data-checkout-receipt", content);
        Assert.Contains("data-checkout-receipt-header", content);
        Assert.Contains("Association Toy Sale", content);
        Assert.Contains("data-checkout-receipt-lines", content);
        Assert.Contains("data-checkout-receipt-total", content);
        Assert.Contains("checkout-results-frame", content);
        Assert.Contains("<th>REF</th>", content);
        Assert.Contains("CK-001-001", content);
        Assert.Contains("Jeu de cubes", content);
    }

    [Fact]
    public async Task CheckoutPageHidesSellerInformationAndUnavailableActions()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "CK-002",
                FirstName = "Noelle",
                LastName = "Secret",
                Email = "noelle.secret@example.test",
                Phone = "0601020304"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CK-002-001",
                ItemNumber = 1,
                Title = "Toboggan miniature",
                SellerPriceCents = 1200,
                Status = "in_stock"
            });

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/caisse");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Noelle", content);
        Assert.DoesNotContain("Secret", content);
        Assert.DoesNotContain("noelle.secret@example.test", content);
        Assert.DoesNotContain("0601020304", content);
        Assert.DoesNotContain("Confirmer", content);
    }

    [Fact]
    public async Task CheckoutValidationApiReturnsTotalsForAvailableItems()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "CV-001",
                FirstName = "Claire",
                LastName = "Valide",
                Email = "claire.valide@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CV-001-001",
                ItemNumber = 1,
                Title = "Jeu valide",
                SellerPriceCents = 1200,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync("/api/caisse/validation", new { itemIds = new[] { itemId } });

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(1, document.RootElement.GetProperty("itemCount").GetInt32());
        Assert.Equal(1200, document.RootElement.GetProperty("totals").GetProperty("subtotalSellerPriceCents").GetInt32());
        Assert.Equal(120, document.RootElement.GetProperty("totals").GetProperty("checkoutFeeCents").GetInt32());
        Assert.Equal(1320, document.RootElement.GetProperty("totals").GetProperty("totalPaidCents").GetInt32());
    }

    [Fact]
    public async Task CheckoutValidationApiReturnsConflictForUnavailableItems()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "CV-002",
                FirstName = "Claire",
                LastName = "Conflit",
                Email = "claire.conflit@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CV-002-001",
                ItemNumber = 1,
                Title = "Jeu indisponible",
                SellerPriceCents = 800,
                Status = "sold"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync("/api/caisse/validation", new { itemIds = new[] { itemId } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(itemId, document.RootElement.GetProperty("conflictItemIds")[0].GetInt64());
    }

    [Fact]
    public async Task SalesApiPersistsSaleAndMarksItemSold()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-001",
                FirstName = "Sonia",
                LastName = "Vente",
                Email = "sonia.vente@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-001-001",
                ItemNumber = 1,
                Title = "Jeu vendu",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync("/api/ventes", new { itemIds = new[] { itemId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sale = await dbContext.Sales.Include(x => x.SaleLines).SingleAsync(x => x.SaleLines.Any(line => line.ItemId == itemId));
            Assert.Equal(1500, sale.SubtotalSellerPriceCents);
            Assert.Equal(150, sale.CheckoutFeeCents);
            Assert.Equal(1650, sale.TotalPaidCents);
            Assert.Equal("sold", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
        }
    }

    private async Task<(HttpClient Client, string Content)> UnlockReconciliationDashboardAsync()
    {
        var client = _factory.CreateClient();
        var content = await UnlockReconciliationDashboardAsync(client);

        return (client, content);
    }

    private static async Task<string> UnlockReconciliationDashboardAsync(HttpClient client)
    {
        var gateContent = await GetReconciliationGateAsync(client);

        var response = await client.PostAsync(
            "/rapprochement?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "test-password")));

        response.EnsureSuccessStatusCode();

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> GetReconciliationGateAsync(HttpClient client)
    {
        var gate = await client.GetAsync("/rapprochement");
        var gateContent = await gate.Content.ReadAsStringAsync();

        gate.EnsureSuccessStatusCode();

        return gateContent;
    }

    private static void AssertMainNavigationContainsExpectedLinks(string content)
    {
        Assert.Contains("main-navigation", content);
        Assert.Contains("href=\"/vendeurs\"", content);
        Assert.Contains("href=\"/caisse\"", content);
        Assert.Contains("href=\"/rapprochement\"", content);
        Assert.Contains("VENDEURS", content);
        Assert.Contains("CAISSE", content);
        Assert.True(
            content.Contains("Rapprochement", StringComparison.OrdinalIgnoreCase),
            "La navigation principale doit contenir le lien Rapprochement.");
    }

    private static void AssertMainNavigationActive(string href, string content)
    {
        Assert.Matches(
            $"class=\"[^\"]*main-navigation-link-active[^\"]*\"\\s+href=\"{Regex.Escape(href)}\"\\s+aria-current=\"page\"",
            content);
    }

    private async Task SeedReconciliationSheetAsync(
        string sellerCode,
        string firstName,
        string lastName,
        string? email,
        string? phone,
        params (string Reference, string Title, string Status, int PriceCents)[] items)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seller = new SellerEntity
        {
            SellerCode = sellerCode,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = phone
        };

        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync();

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = item.Reference,
                ItemNumber = index + 1,
                Title = item.Title,
                SellerPriceCents = item.PriceCents,
                Status = item.Status
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureSoldItemsHaveSaleLinesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var soldItemsWithoutSaleLines = await dbContext.Items
            .Include(x => x.SaleLine)
            .Where(x => x.Status == "sold" && x.SaleLine == null)
            .ToListAsync();

        foreach (var item in soldItemsWithoutSaleLines)
        {
            dbContext.Sales.Add(new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                SubtotalSellerPriceCents = item.SellerPriceCents,
                CheckoutFeeCents = 0,
                TotalPaidCents = item.SellerPriceCents,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = item.SellerPriceCents
                    }
                ]
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static FormUrlEncodedContent BuildFormContent(string html, params (string Name, string Value)[] fields)
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", ExtractRequestVerificationToken(html))
        };

        values.AddRange(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));

        return new FormUrlEncodedContent(values);
    }

    private static string ExtractRequestVerificationToken(string html)
    {
        var inputMatch = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        if (!inputMatch.Success)
        {
            throw new InvalidOperationException("The page did not render an antiforgery token.");
        }

        var valueMatch = Regex.Match(inputMatch.Value, "value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase);
        if (!valueMatch.Success)
        {
            throw new InvalidOperationException("The antiforgery token input did not include a value.");
        }

        return WebUtility.HtmlDecode(valueMatch.Groups["value"].Value);
    }

    [Fact]
    public async Task SalesApiPersistsReceiptDetails()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-003",
                FirstName = "Sonia",
                LastName = "Recu",
                Email = "sonia.recu@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-003-001",
                ItemNumber = 1,
                Title = "Jeu avec recu",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new { itemIds = new[] { itemId }, receiptDetails = "  Association sportive  " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sale = await dbContext.Sales.Include(x => x.SaleLines).SingleAsync(x => x.SaleLines.Any(line => line.ItemId == itemId));
            Assert.Equal("Association sportive", sale.ReceiptDetails);
        }
    }

    [Fact]
    public async Task SalesApiReturnsConflictWithoutPersistingUnavailableItems()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-002",
                FirstName = "Sonia",
                LastName = "Conflit",
                Email = "sonia.conflit@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-002-001",
                ItemNumber = 1,
                Title = "Jeu deja vendu",
                SellerPriceCents = 1500,
                Status = "sold"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync("/api/ventes", new { itemIds = new[] { itemId } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("saved").GetBoolean());
        Assert.Equal(itemId, document.RootElement.GetProperty("conflictItemIds")[0].GetInt64());
    }
}
