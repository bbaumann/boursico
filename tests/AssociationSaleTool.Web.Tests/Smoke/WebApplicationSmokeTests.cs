using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Domain.Items;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssociationSaleTool.Web.Tests.Smoke;

public class WebApplicationSmokeTests : IClassFixture<IntakeWebApplicationFactory>
{
    private const string AdminPath = "/administration";

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
    public async Task PagesDeclareFrenchDocumentLanguage()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.Contains("<meta charset=\"utf-8\" />", content);
        Assert.Contains("<html lang=\"fr\">", content);
    }

    [Fact]
    public void OperatorFacingUiSourceUsesEuroSymbolInsteadOfCurrencyCode()
    {
        var sourceDirectory = FindRepositoryDirectory("src", "AssociationSaleTool.Web");
        var checkedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs",
            ".cshtml",
            ".js"
        };

        var uiDirectories = new[]
        {
            Path.Combine(sourceDirectory, "Pages"),
            Path.Combine(sourceDirectory, "wwwroot", "js")
        };

        var sourceFiles = uiDirectories
            .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            .Append(Path.Combine(sourceDirectory, "Services", "DataListService.cs"))
            .Where(path => checkedExtensions.Contains(Path.GetExtension(path)));

        var filesStillUsingCurrencyCode = sourceFiles
            .Where(path => Regex.IsMatch(File.ReadAllText(path), @"(?<![A-Za-z])EUR(?![A-Za-z])"))
            .Select(path => Path.GetRelativePath(sourceDirectory, path))
            .Order()
            .ToList();

        Assert.Empty(filesStillUsingCurrencyCode);
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
    public async Task SellersIndexRendersLiveSearchHooksForName()
    {
        var client = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Sellers.AddRange(
                new SellerEntity
                {
                    SellerCode = "SL-101",
                    FirstName = "Ariane",
                    LastName = "Martin",
                    Phone = "0101010101",
                    PostalAddress = "1 rue du Filtre"
                },
                new SellerEntity
                {
                    SellerCode = "SL-202",
                    FirstName = "Bruno",
                    LastName = "Durand",
                    Phone = "0202020202",
                    PostalAddress = "2 rue du Filtre"
                });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("data-seller-search-root", content);
        Assert.Contains("data-seller-search", content);
        Assert.Contains("placeholder=\"Nom\"", content);
        Assert.Contains("Aucun vendeur ne correspond à cette recherche.", content);
        Assert.Contains("data-seller-row", content);
        Assert.Contains("data-seller-filter-text=\"ariane martin\"", content);
        Assert.Contains("data-seller-filter-text=\"bruno durand\"", content);
    }

    [Fact]
    public async Task SellersIndexDoesNotShowTheTechnicalSellerCode()
    {
        var client = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Sellers.Add(new SellerEntity
            {
                SellerCode = "DL4BCF74",
                FirstName = "Ariane",
                LastName = "Martin",
                Phone = "0101010101",
                PostalAddress = "1 rue du Filtre"
            });
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("seller-index-table", content);
        Assert.DoesNotContain("seller-index-code-column", content);
        Assert.DoesNotContain("DL4BCF74", content);
    }

    [Fact]
    public async Task SellersIndexAndDetailsShowTheThreeDepositStates()
    {
        var client = _factory.CreateClient();
        long notStartedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notStarted = new SellerEntity { SellerCode = "DS-001", FirstName = "Nadia", LastName = "Etatun" };
            var started = new SellerEntity
            {
                SellerCode = "DS-002", FirstName = "Paul", LastName = "Etatdeux", DepositStartedAt = DateTime.UtcNow
            };
            var validated = new SellerEntity
            {
                SellerCode = "DS-003",
                FirstName = "Vera",
                LastName = "Etattrois",
                DepositStartedAt = DateTime.UtcNow,
                DepositStatus = "deposit_validated"
            };
            dbContext.Sellers.AddRange(notStarted, started, validated);
            await dbContext.SaveChangesAsync();
            notStartedId = notStarted.Id;
        }

        var index = WebUtility.HtmlDecode(await client.GetStringAsync("/vendeurs"));

        Assert.Contains("<th>Dépôt</th>", index);
        Assert.Matches(@"Nadia Etatun</td>[\s\S]*?seller-deposit-status-not-started"">Dépôt non commencé</span>", index);
        Assert.Matches(@"Paul Etatdeux</td>[\s\S]*?seller-deposit-status-pending"">Dépôt à valider</span>", index);
        Assert.Matches(@"Vera Etattrois</td>[\s\S]*?seller-deposit-status-validated"">Dépôt validé</span>", index);

        var details = WebUtility.HtmlDecode(await client.GetStringAsync($"/vendeurs/{notStartedId}"));

        Assert.Contains("seller-deposit-status-not-started", details);
        Assert.Contains("Dépôt non commencé", details);
        Assert.Contains("Étiquetez les jouets quand le vendeur les apporte.", details);
    }

    [Fact]
    public async Task SellerPriceInputsUseFrenchWholeEuroBrowserValidation()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);
        long sellerId;
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "WE-001",
                FirstName = "Whole",
                LastName = "Euro"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "WE-001",
                Title = "Whole euro toy",
                SellerPriceCents = 1200,
                Status = "registered"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
            itemId = item.Id;
        }

        var sellerResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        sellerResponse.EnsureSuccessStatusCode();
        var sellerContent = WebUtility.HtmlDecode(await sellerResponse.Content.ReadAsStringAsync());
        AssertInputIncludesAttributes(
            sellerContent,
            "Input.Rows[0].SellerPrice",
            ("type", "text"),
            ("inputmode", "numeric"),
            ("pattern", "[0-9]+"),
            ("data-whole-euro-input", "true"),
            ("data-whole-euro-message", "Le prix vendeur doit être un montant entier en euros."));

        var editResponse = await client.GetAsync($"/articles/{itemId}/modifier");
        editResponse.EnsureSuccessStatusCode();
        var editContent = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        AssertInputIncludesAttributes(
            editContent,
            "Input.SellerPrice",
            ("type", "text"),
            ("inputmode", "numeric"),
            ("pattern", "[0-9]+"),
            ("data-whole-euro-input", "true"),
            ("data-whole-euro-message", "Le prix vendeur doit être un montant entier en euros."));
    }

    [Fact]
    public async Task SellerFormsRenderPostalCodeAndCitySuggestionFields()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Sellers.Add(new SellerEntity
            {
                SellerCode = "SF-001",
                FirstName = "Sophie",
                LastName = "Formulaire",
                Phone = "0102030405",
                PostalAddress = "1 rue des Tests"
            });
            await dbContext.SaveChangesAsync();
        }

        var createResponse = await client.GetAsync("/vendeurs/creer");
        createResponse.EnsureSuccessStatusCode();
        var createContent = WebUtility.HtmlDecode(await createResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-postal-city-root", createContent);
        Assert.Contains("name=\"Input.PostalCode\"", createContent);
        Assert.Contains("name=\"Input.City\"", createContent);
        Assert.Contains("data-city-select", createContent);
        Assert.DoesNotContain("seller-create-city-options", createContent);
        Assert.Contains("Code postal", createContent);
        Assert.Contains("Ville", createContent);
        Assert.Contains("Nature de la pièce d'identité", createContent);
        Assert.Contains("name=\"Input.IdentityDocumentType\"", createContent);
        Assert.Contains("Numéro de la pièce d'identité", createContent);
        Assert.Contains("name=\"Input.IdentityDocumentNumber\"", createContent);
        Assert.Contains("Date de délivrance", createContent);
        Assert.Contains("name=\"Input.IdentityDocumentIssuedOn\"", createContent);
        Assert.DoesNotContain("Autorité de délivrance", createContent);
        Assert.DoesNotContain("name=\"Input.IdentityDocumentIssuer\"", createContent);

        var sellerId = await GetSellerIdByCodeAsync("SF-001");
        var editResponse = await client.GetAsync($"/vendeurs/{sellerId}/modifier");
        editResponse.EnsureSuccessStatusCode();
        var editContent = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Input.PostalCode\"", editContent);
        Assert.Contains("name=\"Input.City\"", editContent);
        Assert.Contains("data-postal-city-root", editContent);
        Assert.Contains("data-city-select", editContent);
        Assert.DoesNotContain("seller-edit-city-options", editContent);
        Assert.Contains("Nature de la pièce d'identité", editContent);
        Assert.Contains("name=\"Input.IdentityDocumentType\"", editContent);
        Assert.Contains("Numéro de la pièce d'identité", editContent);
        Assert.Contains("name=\"Input.IdentityDocumentNumber\"", editContent);
        Assert.Contains("Date de délivrance", editContent);
        Assert.Contains("name=\"Input.IdentityDocumentIssuedOn\"", editContent);
        Assert.DoesNotContain("Autorité de délivrance", editContent);
        Assert.DoesNotContain("name=\"Input.IdentityDocumentIssuer\"", editContent);
    }

    [Fact]
    public async Task ItemRegistrationFormsExposeTheExistingItemCountToTheLiveCounter()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "IC-001",
                FirstName = "Iris",
                LastName = "Compteur",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38000",
                City = "Grenoble"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            dbContext.Items.AddRange(
                new ItemEntity { SellerId = seller.Id, Title = "Puzzle", SellerPriceCents = 500, Status = "registered" },
                new ItemEntity { SellerId = seller.Id, Title = "Livre", SellerPriceCents = 200, Status = "registered" });
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var detailsResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsContent = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains("data-item-registration-count", detailsContent);
        Assert.Contains("data-existing-item-count=\"2\"", detailsContent);

        var createResponse = await client.GetAsync("/vendeurs/creer");
        createResponse.EnsureSuccessStatusCode();
        var createContent = await createResponse.Content.ReadAsStringAsync();
        Assert.Contains("data-item-registration-count", createContent);
        Assert.Contains("data-existing-item-count=\"0\"", createContent);
    }

    [Fact]
    public async Task DataPageShowsTheStatisticsTab()
    {
        var client = _factory.CreateClient();

        var listContent = WebUtility.HtmlDecode(await client.GetStringAsync("/donnees"));
        Assert.Contains("href=\"/donnees?onglet=statistiques\"", listContent);

        // Other smoke tests leave sold items without a sale line in the shared database,
        // which the seller sheets behind these statistics rightly refuse.
        await EnsureSoldItemsHaveSaleLinesAsync();
        var response = await client.GetAsync("/donnees?onglet=statistiques");
        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Matches("href=\"/donnees\\?onglet=statistiques\"\\s+aria-current=\"page\"", content);
        foreach (var heading in new[]
        {
            "<h2>Recettes de l'association</h2>",
            "<h2>Argent des clients</h2>",
            "<h2>Ventes des vendeurs</h2>",
            "<h2>Articles</h2>",
            "<h2>Répartition des prix</h2>",
            "<h2>Villes des acheteurs</h2>"
        })
        {
            Assert.Contains(heading, content);
        }

        Assert.Contains("Frais d'inscription (2 € par tranche de 15 articles)", content);
        Assert.Contains("Commission vendeur (15 %)", content);
        Assert.Contains("Frais client (10 %)", content);
        Assert.Contains("Plus de 20 €", content);
    }

    [Fact]
    public async Task CheckoutLocksTheCashRegisterChoiceOnlyDuringTheSalePhase()
    {
        var client = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            await SeedCashRegisterAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), "Caisse verrou");
        }

        await SetSalePhaseAsync(SalePhase.Presale);
        try
        {
            var presaleContent = WebUtility.HtmlDecode(await client.GetStringAsync("/caisse"));
            Assert.Contains("data-cash-register-locked=\"false\"", presaleContent);
            Assert.Contains("Utiliser cette caisse", presaleContent);

            await SetSalePhaseAsync(SalePhase.Sale);
            var saleContent = WebUtility.HtmlDecode(await client.GetStringAsync("/caisse"));
            Assert.Contains("data-cash-register-locked=\"true\"", saleContent);
            Assert.Contains("data-checkout-cash-register-choice", saleContent);
            Assert.Contains("Pour changer de caisse, passez par l'administration sur cet ordinateur.", saleContent);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task AdministrationOffersThisComputerCashRegisterChoiceOnlyOnceUnlocked()
    {
        var client = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SeedCashRegisterAsync(dbContext, "Caisse poste active");
            if (!await dbContext.CashRegisters.AnyAsync(x => x.Name == "Caisse poste inactive"))
            {
                dbContext.CashRegisters.Add(new CashRegisterEntity { Name = "Caisse poste inactive", IsActive = false });
                await dbContext.SaveChangesAsync();
            }
        }

        var gateContent = WebUtility.HtmlDecode(await GetSaleStatusGateAsync(client));
        Assert.DoesNotContain("Caisse de cet ordinateur", gateContent);

        var content = await UnlockSaleStatusPageAsync(client, "caisses");
        var section = Regex.Match(content, "<section class=\"subsection\" data-device-cash-register-root>[\\s\\S]*?</section>");
        Assert.True(section.Success, "Missing the section for this computer's cash register");
        Assert.Contains("<h2>Caisse de cet ordinateur</h2>", section.Value);
        Assert.Contains("Changer la caisse de cet ordinateur", section.Value);
        Assert.Contains(">Caisse poste active</option>", section.Value);
        Assert.DoesNotContain("Caisse poste inactive", section.Value);
    }

    [Fact]
    public async Task RequiredFieldMarkersAreRenderedOnSellerForms()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Sellers.Add(new SellerEntity
            {
                SellerCode = "RF-001",
                FirstName = "Renee",
                LastName = "Obligatoire"
            });
            await dbContext.SaveChangesAsync();
        }

        var createResponse = await client.GetAsync("/vendeurs/creer");
        createResponse.EnsureSuccessStatusCode();
        var createContent = WebUtility.HtmlDecode(await createResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("form-required-note", createContent);
        AssertRequiredFieldMarker(createContent, "Prénom");
        AssertRequiredFieldMarker(createContent, "Nom");
        AssertRequiredFieldMarker(createContent, "Téléphone");
        AssertRequiredFieldMarker(createContent, "Adresse postale");
        AssertRequiredFieldMarker(createContent, "Code postal");
        AssertRequiredFieldMarker(createContent, "Ville");

        var sellerId = await GetSellerIdByCodeAsync("RF-001");
        var editResponse = await client.GetAsync($"/vendeurs/{sellerId}/modifier");
        editResponse.EnsureSuccessStatusCode();
        var editContent = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("form-required-note", editContent);
        AssertRequiredFieldMarker(editContent, "Prénom");
        AssertRequiredFieldMarker(editContent, "Nom");
        AssertRequiredFieldMarker(editContent, "Téléphone");
        AssertRequiredFieldMarker(editContent, "Adresse postale");
        AssertRequiredFieldMarker(editContent, "Code postal");
        AssertRequiredFieldMarker(editContent, "Ville");
    }

    [Fact]
    public async Task RequiredFieldMarkersAreRenderedOnItemEditForm()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RI-001",
                FirstName = "Renee",
                LastName = "Article"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RI-001",
                Title = "Puzzle requis",
                SellerPriceCents = 800,
                Status = "registered"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.GetAsync($"/articles/{itemId}/modifier");
        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("form-required-note", content);
        AssertRequiredFieldMarker(content, "Description");
        AssertRequiredFieldMarker(content, "Prix vendeur");
    }

    [Fact]
    public async Task RequiredFieldMarkersAreRenderedOnCheckoutBuyerFields()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/caisse");
        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.DoesNotContain("form-required-note", content);
        AssertRequiredFieldMarker(content, "Nom ou structure");
        AssertRequiredFieldMarker(content, "Code postal");
        AssertRequiredFieldMarker(content, "Ville");
        Assert.DoesNotContain(
            "<span class=\"field-label-text\">Adresse <span class=\"required-field-badge\">Obligatoire</span></span>",
            content);
    }

    [Fact]
    public async Task CreateSellerPostShowsDuplicateMatchesInFrench()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Sellers.Add(new SellerEntity
            {
                SellerCode = "DS-777",
                FirstName = "Ariane",
                LastName = "Doublon",
                Email = "ariane.doublon@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests"
            });
            await dbContext.SaveChangesAsync();
        }

        var form = await client.GetAsync("/vendeurs/creer");
        form.EnsureSuccessStatusCode();
        var formContent = await form.Content.ReadAsStringAsync();
        var response = await client.PostAsync(
            "/vendeurs/creer",
            BuildFormContent(
                formContent,
                ("Input.FirstName", "Ariane"),
                ("Input.LastName", "Doublon"),
                ("Input.Phone", "0123"),
                ("Input.PostalAddress", "2 rue des Tests"),
                ("Input.PostalCode", "38100"),
                ("Input.City", "Grenoble"),
                ("Input.IntakeMode", "EmailPreregistration"),
                ("Input.Rows[0].Title", "Puzzle doublon"),
                ("Input.Rows[0].SellerPrice", "5")));

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Ce vendeur existe déjà.", content);
        Assert.Contains("Vendeur déjà existant", content);
        Assert.Contains("data-existing-seller-alert", content);
        Assert.DoesNotContain("d\u00c3", content);
        Assert.Contains("Puzzle doublon", content);
        Assert.Contains("value=\"5\"", content);
        var navigationIndex = content.IndexOf("Navigation principale", StringComparison.Ordinal);
        var alertIndex = content.IndexOf("data-existing-seller-alert", StringComparison.Ordinal);
        var sellerCardIndex = content.IndexOf("seller-detail-card", StringComparison.Ordinal);
        Assert.True(navigationIndex >= 0, "The page should render the main navigation.");
        Assert.True(alertIndex > navigationIndex, "The existing-seller alert should appear below the navigation.");
        Assert.True(alertIndex < sellerCardIndex, "The existing-seller alert should appear before the seller detail card.");
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
    public async Task LayoutShowsCurrentSalePhaseNearEventName()
    {
        var client = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await new SalePhaseService(dbContext).SetPhaseAsync(SalePhase.Presale);
        }

        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Association Toy Sale Tests", content);
        Assert.Contains("Prévente", content);
        Assert.Contains("class=\"app-status-display\"", content);
        Assert.DoesNotContain("class=\"app-status-link\"", content);
    }

    [Fact]
    public async Task AdminPageShowsMainNavigationWithAdminActive()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(AdminPath);

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive(AdminPath, content);
    }

    [Fact]
    public async Task AdminPageShowsAdminGateWhenLocked()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(AdminPath);

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Administration", content);
        Assert.Contains("Mot de passe administrateur", content);
        Assert.Contains("Déverrouiller", content);
        Assert.DoesNotContain("Statut de la vente", content);
        Assert.DoesNotContain("Correction des articles", content);
    }

    [Fact]
    public async Task AdminPageDoesNotExposeLegacySaleStatusRoute()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vente/statut");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaleStatusUnlockRejectsInvalidAdminPasswordInFrench()
    {
        var client = _factory.CreateClient();
        var gateContent = await GetSaleStatusGateAsync(client);

        var response = await client.PostAsync(
            $"{AdminPath}?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "wrong-password")));

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Mot de passe administrateur", content);
        Assert.Contains("Mot de passe administrateur incorrect.", content);
        Assert.DoesNotContain("Correction des articles", content);
    }

    [Fact]
    public async Task SaleStatusUnlockShowsPhaseAndCorrectionControls()
    {
        var client = _factory.CreateClient();

        var content = await UnlockSaleStatusPageAsync(client);

        Assert.Contains("Statut de la vente", content);
        Assert.Contains("Correction des articles", content);
        Assert.Contains("Passer en prévente", content);
        Assert.Contains("Passer en vente", content);
        Assert.Contains("Passer en après-vente", content);
        Assert.DoesNotContain("name=\"Input.Password\"", content);
        Assert.DoesNotContain("name=\"CorrectionPassword\"", content);
        Assert.Contains($"href=\"{AdminPath}\"", content);
    }

    [Fact]
    public async Task AdminPageCashRegisterManagementDoesNotExposeDisplayOrder()
    {
        var client = _factory.CreateClient();

        var content = await UnlockSaleStatusPageAsync(client);

        Assert.Contains("Caisses", content);
        Assert.DoesNotContain("Ordre d'affichage", content);
        Assert.DoesNotContain("CashRegisterInput.DisplayOrder", content);
    }

    [Fact]
    public async Task AdminPageCreatesActiveCashRegisterWhenActiveCheckboxIsChecked()
    {
        var client = _factory.CreateClient();
        var cashRegisterName = $"CAISSE-ACTIVE-{Guid.NewGuid():N}";

        var dashboardContent = await UnlockSaleStatusPageAsync(client, "caisses");
        var response = await client.PostAsync(
            $"{AdminPath}?handler=SaveCashRegister",
            BuildCashRegisterCreateFormContent(dashboardContent, cashRegisterName));

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cashRegister = await dbContext.CashRegisters.SingleAsync(x => x.Name == cashRegisterName);
        Assert.True(cashRegister.IsActive);
    }

    [Theory]
    [InlineData(
        SalePhase.Presale,
        "Statut actuel : Prévente",
        "Dans ce statut, les informations des vendeurs et des jouets sont modifiables. Les ventes restent possibles.")]
    [InlineData(
        SalePhase.Sale,
        "Statut actuel : Vente",
        "Dans ce statut, les informations des vendeurs et des jouets ne sont pas modifiables. Les ventes restent possibles.")]
    [InlineData(
        SalePhase.PostSale,
        "Statut actuel : Après-vente",
        "Dans ce statut, les ventes sont fermées. Les informations des vendeurs et des jouets ne sont pas modifiables, sauf pour marquer un jouet manquant pendant le rapprochement.")]
    public async Task SaleStatusPageExplainsCurrentPhaseWhenUnlocked(
        SalePhase phase,
        string expectedCurrentStatus,
        string expectedDescription)
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(phase);

        try
        {
            var content = await UnlockSaleStatusPageAsync(client);

            Assert.Contains(expectedCurrentStatus, content);
            Assert.Contains(expectedDescription, content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Theory]
    [InlineData(SalePhase.Presale, "Passer en prévente", "Passer en vente", "Passer en après-vente")]
    [InlineData(SalePhase.Sale, "Passer en vente", "Passer en prévente", "Passer en après-vente")]
    [InlineData(SalePhase.PostSale, "Passer en après-vente", "Passer en prévente", "Passer en vente")]
    public async Task SaleStatusPageDisablesCurrentPhaseButtonWhenUnlocked(
        SalePhase phase,
        string disabledButtonLabel,
        string enabledButtonLabel1,
        string enabledButtonLabel2)
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(phase);

        try
        {
            var content = await UnlockSaleStatusPageAsync(client);

            AssertPhaseButtonDisabled(content, disabledButtonLabel);
            AssertPhaseButtonEnabled(content, enabledButtonLabel1);
            AssertPhaseButtonEnabled(content, enabledButtonLabel2);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task SaleStatusPageChangesPhaseWhenUnlocked()
    {
        var client = _factory.CreateClient();
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await new SalePhaseService(dbContext).SetPhaseAsync(SalePhase.Presale);
        }

        var dashboardContent = await UnlockSaleStatusPageAsync(client);
        var response = await client.PostAsync(
            $"{AdminPath}?handler=ChangePhase",
            BuildFormContent(
                dashboardContent,
                ("Input.Phase", "sale")));

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Administration", content);
        Assert.Contains("Vente", content);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(SalePhase.Sale, await new SalePhaseService(dbContext).GetCurrentPhaseAsync());
            await new SalePhaseService(dbContext).SetPhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task SaleStatusPageCorrectsMistakenSaleLineWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        long saleLineId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SC-001",
                FirstName = "Sabine",
                LastName = "Correction"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SC-001-001",
                Title = "Article mal vendu",
                SellerPriceCents = 900,
                Status = "sold"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SC-001");

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 900,
                CheckoutFeeCents = 90,
                TotalPaidCents = 990,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = 900
                    }
                ]
            };

            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
            saleLineId = sale.SaleLines.Single().Id;
        }

        var dashboardContent = await UnlockSaleStatusPageAsync(client);
        var response = await client.PostAsync(
            $"{AdminPath}?handler=CorrectSaleLine&saleLineId={saleLineId}",
            BuildFormContent(dashboardContent));

        response.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("in_stock", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
            Assert.NotNull((await dbContext.SaleLines.SingleAsync(x => x.Id == saleLineId)).CorrectedAt);
        }
    }

    [Fact]
    public async Task SaleStatusUnlockShowsItemCorrectionControls()
    {
        var client = _factory.CreateClient();
        long saleId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SD-001",
                FirstName = "Sonia",
                LastName = "Defaut"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var soldItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-001",
                Title = "Article remboursable",
                SellerPriceCents = 1000,
                Status = "sold"
            };
            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-002",
                Title = "Article \u00e0 contr\u00f4ler",
                SellerPriceCents = 700,
                Status = "in_stock"
            };
            var defectiveItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-003",
                Title = "Article déjà défectueux",
                SellerPriceCents = 500,
                Status = "defective"
            };
            var missingItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-004",
                Title = "Article manquant",
                SellerPriceCents = 300,
                Status = "missing"
            };
            var registeredItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-005",
                Title = "Article enregistré",
                SellerPriceCents = 200,
                Status = "registered"
            };

            dbContext.Items.AddRange(soldItem, inStockItem, defectiveItem, missingItem, registeredItem);
            await dbContext.SaveChangesAsync();
            var cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SD-001");
            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 1000,
                CheckoutFeeCents = 100,
                TotalPaidCents = 1100,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = soldItem.Id,
                        SellerPriceAtSaleCents = 1000
                    }
                ]
            };
            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            saleId = sale.Id;
        }

        var content = await UnlockSaleStatusPageAsync(client, "articles");

        Assert.Contains("Correction des articles", content);
        Assert.Contains("data-item-correction-root", content);
        Assert.DoesNotContain("data-defective-items-root", content);
        Assert.Contains("item-correction-reference-column", content);
        Assert.Contains("item-correction-title-column", content);
        Assert.Contains("item-correction-seller-column", content);
        Assert.Contains("Statut", content);
        Assert.Contains("Rembourser", content);
        Assert.DoesNotContain("Rembourser / marquer d\u00e9fectueux", content);
        Assert.DoesNotContain("<th>Montant \u00e0 rembourser</th>", content);
        Assert.Contains($">#{saleId}</a>", content);
        Assert.Contains($"href=\"/caisse/ventes/{saleId}\"", content);
        Assert.DoesNotContain($"#{saleId} -", content);
        Assert.Contains("10.00 €", content);
        Assert.Contains("1.00 €", content);
        Assert.Contains("11.00 €", content);
        Assert.Contains("Article remboursable", content);
        Assert.Contains("Article \u00e0 contr\u00f4ler", content);
        Assert.Contains("Article d\u00e9j\u00e0 d\u00e9fectueux", content);
        Assert.Contains("Article manquant", content);
        Assert.DoesNotContain("Article enregistr\u00e9", content);
        Assert.Contains("Marquer d\u00e9fectueux", content);
        Assert.Contains("Remettre en stock", content);
    }

    [Fact]
    public async Task SaleStatusItemCorrectionLoadsAllRowsForClientSideFiltering()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "AF-001",
                FirstName = "Aline",
                LastName = "Filtre"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            for (var index = 1; index <= 15; index++)
            {
                dbContext.Items.Add(new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = $"AF-001-{index:000}",
                    Title = $"Article vendu {index}",
                    SellerPriceCents = 500 + index,
                    Status = "sold"
                });
            }

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "ZZ-001-999",
                Title = "Article tardif à contrôler",
                SellerPriceCents = 900,
                Status = "in_stock"
            });
            await dbContext.SaveChangesAsync();
        }

        var content = await UnlockSaleStatusPageAsync(client, "articles");

        Assert.Contains("Article tardif à contrôler", content);
        Assert.Contains("Marquer défectueux", content);
        Assert.Contains("Saisissez une recherche pour filtrer les articles.", content);
        Assert.DoesNotContain("Les 15 premiers articles non enregistrés sont affichés.", content);
    }

    [Fact]
    public async Task SaleStatusPageRefundsDefectiveSaleLineWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        long saleLineId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SR-001",
                FirstName = "Solene",
                LastName = "Retour"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SR-001-001",
                Title = "Article retourn\u00e9",
                SellerPriceCents = 1500,
                Status = "sold"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-RET");

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                SubtotalSellerPriceCents = 1500,
                CheckoutFeeCents = 150,
                TotalPaidCents = 1650,
                CashRegisterId = cashRegisterId,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = 1500
                    }
                ]
            };
            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
            saleLineId = sale.SaleLines.Single().Id;
        }

        var dashboardContent = await UnlockSaleStatusPageAsync(client);
        var response = await client.PostAsync(
            $"{AdminPath}?handler=RefundDefectiveSaleLine&saleLineId={saleLineId}",
            BuildFormContent(dashboardContent, ("refundCashRegisterId", cashRegisterId.ToString(CultureInfo.InvariantCulture))));

        response.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("defective", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
            var saleLine = await dbContext.SaleLines.SingleAsync(x => x.Id == saleLineId);
            Assert.NotNull(saleLine.RefundedAt);
            Assert.Equal(1500, saleLine.RefundItemPriceCents);
            Assert.Equal(150, saleLine.RefundCheckoutFeeCents);
            Assert.Equal(1650, saleLine.RefundTotalCents);
            Assert.Equal("defective", saleLine.RefundReason);
            Assert.Equal(cashRegisterId, saleLine.RefundCashRegisterId);
        }
    }

    [Fact]
    public async Task AdminPageCancelsDefectiveRefundWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        long saleLineId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SU-001",
                FirstName = "Solene",
                LastName = "Undo"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SU-001",
                Title = "Article remboursement erreur",
                SellerPriceCents = 1500,
                Status = "defective"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SU-001");

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 1500,
                CheckoutFeeCents = 150,
                TotalPaidCents = 1650,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = 1500,
                        RefundedAt = DateTime.UtcNow,
                        RefundItemPriceCents = 1500,
                        RefundCheckoutFeeCents = 150,
                        RefundTotalCents = 1650,
                        RefundReason = "defective",
                        RefundCashRegisterId = cashRegisterId
                    }
                ]
            };
            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
            saleLineId = sale.SaleLines.Single().Id;
        }

        var dashboardContent = await UnlockSaleStatusPageAsync(client, "articles");
        Assert.Contains("Annuler le remboursement", dashboardContent);

        var response = await client.PostAsync(
            $"{AdminPath}?handler=CancelDefectiveRefund&saleLineId={saleLineId}",
            BuildFormContent(dashboardContent));

        response.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("sold", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
            var saleLine = await dbContext.SaleLines.SingleAsync(x => x.Id == saleLineId);
            Assert.Null(saleLine.RefundedAt);
            Assert.Null(saleLine.RefundItemPriceCents);
            Assert.Null(saleLine.RefundCheckoutFeeCents);
            Assert.Null(saleLine.RefundTotalCents);
            Assert.Null(saleLine.RefundReason);
        }
    }

    [Fact]
    public async Task SaleStatusPageMarksInStockItemDefectiveWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SM-001",
                FirstName = "Samia",
                LastName = "Marque"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SM-001-001",
                Title = "Article ab\u00eem\u00e9",
                SellerPriceCents = 800,
                Status = "in_stock"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var dashboardContent = await UnlockSaleStatusPageAsync(client);
        var response = await client.PostAsync(
            $"{AdminPath}?handler=MarkDefectiveItem&itemId={itemId}",
            BuildFormContent(dashboardContent));

        response.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal("defective", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
        }
    }

    [Fact]
    public async Task SaleStatusLockedPostHandlersShowAdminGateAndDoNotChangeData()
    {
        var client = _factory.CreateClient();
        long itemId;
        long saleLineId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await new SalePhaseService(dbContext).SetPhaseAsync(SalePhase.Presale);

            var seller = new SellerEntity
            {
                SellerCode = "SG-001",
                FirstName = "Sarah",
                LastName = "Garde"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SG-001-001",
                Title = "Article garde",
                SellerPriceCents = 700,
                Status = "sold"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            var cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SG-001");

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 700,
                CheckoutFeeCents = 70,
                TotalPaidCents = 770,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = 700
                    }
                ]
            };
            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
            saleLineId = sale.SaleLines.Single().Id;
        }

        var gateContent = await GetSaleStatusGateAsync(client);
        var phaseResponse = await client.PostAsync(
            $"{AdminPath}?handler=ChangePhase",
            BuildFormContent(gateContent, ("Input.Phase", "sale")));
        var correctionResponse = await client.PostAsync(
            $"{AdminPath}?handler=CorrectSaleLine&saleLineId={saleLineId}",
            BuildFormContent(gateContent));

        foreach (var response in new[] { phaseResponse, correctionResponse })
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
            Assert.Equal(SalePhase.Presale, await new SalePhaseService(dbContext).GetCurrentPhaseAsync());
            Assert.Equal("sold", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
            Assert.Null((await dbContext.SaleLines.SingleAsync(x => x.Id == saleLineId)).CorrectedAt);
        }
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
    public async Task SalesViewListPageLoadsWithoutAdminGate()
    {
        var client = _factory.CreateClient();
        var sale = await SeedSalesViewSaleAsync("Association lecture");

        var response = await client.GetAsync("/caisse/ventes");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Liste des ventes", content);
        Assert.Contains("Rechercher une vente", content);
        Assert.Contains($"#{sale.SaleId}", content);
        Assert.Contains(sale.FirstReference.ToLowerInvariant(), content);
        Assert.Contains("data-sales-view-search", content);
        Assert.DoesNotContain("Mot de passe administrateur", content);
    }

    [Fact]
    public async Task SalesViewDetailPageShowsReadonlyCorrectedSale()
    {
        var client = _factory.CreateClient();
        var sale = await SeedSalesViewSaleAsync("Client facture", new DateTime(2026, 5, 4, 11, 15, 0, DateTimeKind.Utc));

        var response = await client.GetAsync($"/caisse/ventes/{sale.SaleId}");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"Vente #{sale.SaleId}", content);
        Assert.Contains(sale.FirstReference, content);
        Assert.Contains("Jeu corrigé", content);
        Assert.Contains("Corrigée", content);
        Assert.Contains(sale.SecondReference, content);
        Assert.DoesNotContain("Vente active", content);
        Assert.Contains("Client facture", content);
        Assert.Contains("Retour à la liste", content);
        Assert.DoesNotContain("Remettre en stock", content);
        Assert.DoesNotContain("Enregistrer la vente", content);
    }

    [Fact]
    public async Task SalesViewDetailPageCreatesPrintableInvoiceOnReadonlySaleRouteWhenAddressWasCaptured()
    {
        var client = _factory.CreateClient();
        var sale = await SeedSalesViewInvoiceCandidateAsync();

        var response = await client.GetAsync($"/caisse/ventes/{sale.SaleId}");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Imprimer la facture", content);
        Assert.Contains("data-sales-view-print-invoice", content);
        Assert.Contains("12 rue des Lilas", content);
        Assert.DoesNotContain($"/vente/{sale.SaleId}", content);

        var printResponse = await client.PostAsync(
            $"/caisse/ventes/{sale.SaleId}?handler=PrintInvoice",
            BuildFormContent(content));

        printResponse.EnsureSuccessStatusCode();
        var printContent = WebUtility.HtmlDecode(await printResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-sales-view-print-on-load", printContent);
        Assert.Matches(@"Facture 2611AFBJ\d{4}", printContent);
        Assert.Contains("Association Familiale", printContent);
        Assert.Contains("MJC Centre", printContent);
        Assert.Contains("12 rue des Lilas", printContent);
        Assert.Contains("38100 Grenoble", printContent);
        Assert.Contains(sale.FirstReference, printContent);
        Assert.Contains("P.U. HT", printContent);
        Assert.Contains("TVA non applicable en vertu de l'article 293 B du CGI.", printContent);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedSale = await dbContext.Sales.SingleAsync(x => x.Id == sale.SaleId);
        Assert.NotNull(storedSale.InvoiceNumber);
        Assert.StartsWith("2611AFBJ", storedSale.InvoiceNumber);
        Assert.NotNull(storedSale.InvoiceIssuedAt);

        var aliasResponse = await client.GetAsync($"/vente/{sale.SaleId}");
        Assert.Equal(HttpStatusCode.NotFound, aliasResponse.StatusCode);
    }

    [Fact]
    public async Task SalesViewDetailPageAddsBuyerAddressBeforePrintingInvoice()
    {
        var client = _factory.CreateClient();
        var sale = await SeedSalesViewInvoiceCandidateAsync(buyerAddress: null);

        var response = await client.GetAsync($"/caisse/ventes/{sale.SaleId}");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Adresse de facturation", content);
        Assert.Contains("Enregistrer l'adresse", content);
        Assert.DoesNotContain("data-sales-view-print-invoice", content);

        var addressResponse = await client.PostAsync(
            $"/caisse/ventes/{sale.SaleId}?handler=EnregistrerAdresse",
            BuildFormContent(content, ("AddressInput.BuyerAddress", "  34 rue de la Facture  ")));

        addressResponse.EnsureSuccessStatusCode();
        var addressContent = WebUtility.HtmlDecode(await addressResponse.Content.ReadAsStringAsync());
        Assert.Contains("Adresse de facturation enregistrée.", addressContent);
        Assert.Contains("34 rue de la Facture", addressContent);
        Assert.Contains("data-sales-view-print-invoice", addressContent);

        var printResponse = await client.PostAsync(
            $"/caisse/ventes/{sale.SaleId}?handler=PrintInvoice",
            BuildFormContent(addressContent));

        printResponse.EnsureSuccessStatusCode();
        var printContent = WebUtility.HtmlDecode(await printResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-sales-view-print-on-load", printContent);
        Assert.Contains("34 rue de la Facture", printContent);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedSale = await dbContext.Sales.SingleAsync(x => x.Id == sale.SaleId);
        Assert.Equal("34 rue de la Facture", storedSale.BuyerAddress);
        Assert.NotNull(storedSale.InvoiceNumber);
    }

    [Fact]
    public async Task DataListsPageLoadsWithoutAdminGateAndShowsArticlesByDefault()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Donn\u00e9es", content);
        Assert.Contains("Consultation en lecture seule des donn\u00e9es de l'\u00e9v\u00e9nement.", content);
        Assert.Contains("data-data-list-root", content);
        Assert.Contains("data-data-list-table", content);
        Assert.Contains("data-list-filter-bar", content);
        Assert.Contains("data-list-filter-field-search", content);
        Assert.Contains("data-list-filter-field-status", content);
        Assert.Contains("data-data-list-search", content);
        Assert.Contains("data-data-list-status-filter", content);
        Assert.Equal(5, CountSortableDataListHeaders(content));
        Assert.Contains("data-data-list-sort-value", content);
        Assert.Contains(scenario.RegisteredReference, content);
        Assert.Contains(scenario.InStockReference, content);
        Assert.Contains(scenario.SoldReference, content);
        Assert.Contains(scenario.MissingReference, content);
        Assert.Contains($"href=\"/vendeurs/{scenario.SellerId}\">{scenario.RegisteredReference}</a>", content);
        Assert.Contains("data-data-list-status=\"registered\"", content);
        Assert.Contains("data-data-list-status=\"in_stock\"", content);
        Assert.Contains("data-data-list-status=\"sold\"", content);
        Assert.Contains("data-data-list-status=\"missing\"", content);
        Assert.DoesNotContain("Mot de passe administrateur", content);
        Assert.DoesNotContain("Cr\u00e9er", content);
        Assert.DoesNotContain("Modifier", content);
        Assert.DoesNotContain("Supprimer", content);
    }

    [Fact]
    public async Task DataListsPageShowsMainNavigationWithDataActive()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/donnees");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive("/donnees", content);
    }

    [Fact]
    public async Task DataListsSellersTabSearchesByNameOnly()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees?onglet=vendeurs");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Vendeurs", content);
        Assert.DoesNotContain(scenario.SellerCode, content);
        Assert.Contains("Diane Liste", content);
        Assert.Contains("diane.liste@example.test", content);
        Assert.Contains("0606060606", content);
        Assert.Contains("data-list-seller-table", content);
        Assert.Equal(10, CountSortableDataListHeaders(content));
        Assert.DoesNotContain("data-list-code-column", content);
        Assert.Contains("data-list-name-column", content);
        Assert.Contains("data-list-email-column", content);
        Assert.Contains("data-list-phone-column", content);
        Assert.Contains("title=\"Total des articles\">Tot.</abbr>", content);
        Assert.Contains("title=\"Enregistr\u00e9s\">Enr.</abbr>", content);
        Assert.Contains("title=\"En stock\">Stock</abbr>", content);
        Assert.Contains("title=\"Vendus\">Vend.</abbr>", content);
        Assert.Contains("title=\"Manquants\">Manq.</abbr>", content);
        Assert.Contains("title=\"D\u00e9fectueux\">D\u00e9f.</abbr>", content);
        Assert.Contains("data-list-fee-column", content);
        Assert.Contains($"href=\"/vendeurs/{scenario.SellerId}\">Diane Liste</a>", content);
        Assert.Contains("0/2\u20ac", content);
        Assert.DoesNotContain("0.00 € / 2.00 €", content);
        Assert.Contains($"data-data-list-filter-text=\"diane liste\"", content);
        Assert.DoesNotContain($"data-data-list-filter-text=\"diane liste diane.liste@example.test", content);
        Assert.DoesNotContain($"data-data-list-filter-text=\"diane liste 0606060606", content);
        Assert.DoesNotContain($"data-data-list-filter-text=\"diane liste rue lecture", content);
    }

    [Fact]
    public async Task DataListsSalesTabSearchesByApprovedSaleCriteriaOnly()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees?onglet=ventes");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Ventes", content);
        Assert.Contains("data-list-sale-table", content);
        Assert.Equal(7, CountSortableDataListHeaders(content));
        Assert.DoesNotContain("sales-view-table", content);
        Assert.Contains("data-list-sale-id-column", content);
        Assert.DoesNotContain("<th>Articles</th>", content);
        Assert.DoesNotContain("<th>Frais</th>", content);
        Assert.Contains($"#{scenario.SaleId}", content);
        Assert.Contains("MJC Centre - 38100 Grenoble", content);
        Assert.Contains("Ch\u00e8que", content);
        Assert.Contains("jeu vendu", content);
        Assert.Contains("data-data-list-filter-text", content);
        Assert.Contains("mjc centre", content);
        Assert.Contains("38100", content);
        Assert.Contains("grenoble", content);
        Assert.Contains("ch\u00e8que", content);
        Assert.Contains(scenario.SoldReference.ToLowerInvariant(), content);
        Assert.DoesNotContain("texte re\u00e7u confidentiel", content);
        Assert.DoesNotContain("CH-123", content);
        Assert.DoesNotContain("banque cach\u00e9e", content);
    }

    [Fact]
    public async Task DataListsChequesTabShowsRegisteredChequesOnly()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees?onglet=cheques");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Ch\u00e8ques", content);
        Assert.Contains("data-list-cheque-table", content);
        Assert.Equal(4, CountSortableDataListHeaders(content));
        Assert.Contains("data-list-cheque-sale-column", content);
        Assert.Contains("N\u00b0 ch\u00e8que", content);
        Assert.Contains("Banque", content);
        Assert.Contains("Montant", content);
        Assert.Contains($"href=\"/caisse/ventes/{scenario.SaleId}\">#{scenario.SaleId}</a>", content);
        Assert.Contains("CH-123", content);
        Assert.Contains("banque cach\u00e9e", content);
        Assert.Contains("9.90 €", content);
    }

    [Fact]
    public async Task DataListsClosingTabShowsGlobalAndCheckoutTotals()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees?onglet=cloture");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Cl\u00f4ture", content);
        Assert.Contains("data-list-closing-table", content);
        Assert.Contains("Toutes les caisses", content);
        Assert.Contains(scenario.CashRegisterName, content);
        Assert.Contains("9.90\u20ac", content);
        Assert.Contains("0.00\u20ac", content);
        Assert.Contains("Ch\u00e8ques attendus", content);
        Assert.Contains("CB attendue", content);
        Assert.DoesNotContain("Sous-total", content);
        Assert.DoesNotContain("Encaiss\u00e9", content);
        Assert.True(
            content.IndexOf(scenario.CashRegisterName, StringComparison.Ordinal)
                < content.IndexOf("Toutes les caisses", StringComparison.Ordinal),
            "Each cash register should be listed before the total line.");
    }

    [Fact]
    public async Task DataListsChequesByCashRegisterTabShowsEachChequeUnderItsCashRegister()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees?onglet=cheques-caisse");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Chèques par caisse", content);
        Assert.Contains("data-list-register-cheque-table", content);
        Assert.Contains(scenario.CashRegisterName, content);
        Assert.Contains("CH-123", content);
        Assert.Contains("banque cachée", content);
        Assert.Contains("MJC Centre", content);
        Assert.Contains("9.90€", content);
        var registerIndex = content.IndexOf(scenario.CashRegisterName, StringComparison.Ordinal);
        Assert.True(registerIndex >= 0, "The cash register should be listed.");
        Assert.True(
            content.IndexOf("CH-123", registerIndex, StringComparison.Ordinal) > registerIndex,
            "The cheque should be listed under its cash register.");
    }

    [Fact]
    public async Task SalesViewDetailPageShowsReadonlyRefundedDefectiveSale()
    {
        var client = _factory.CreateClient();
        long saleId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-REF",
                FirstName = "Renee",
                LastName = "Refund"
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-REF-001",
                Title = "Jeu defectueux",
                SellerPriceCents = 1000,
                Status = "defective"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            var cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-REF");

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = 1000,
                CheckoutFeeCents = 100,
                TotalPaidCents = 1100,
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = 1000,
                        RefundedAt = DateTime.UtcNow,
                        RefundItemPriceCents = 1000,
                        RefundCheckoutFeeCents = 100,
                        RefundTotalCents = 1100,
                        RefundReason = "defective",
                        RefundCashRegisterId = cashRegisterId
                    }
                ]
            };
            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            saleId = sale.Id;
        }

        var response = await client.GetAsync($"/caisse/ventes/{saleId}");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Rembours\u00e9", content);
        Assert.Contains("Remboursements", content);
        Assert.Contains("Total corrig\u00e9", content);
        Assert.DoesNotContain("Rembours\u00e9e - d\u00e9fectueux", content);
        Assert.DoesNotContain("Prix rembours\u00e9", content);
        Assert.DoesNotContain("Frais rembours\u00e9s", content);
        Assert.DoesNotContain("Total rembours\u00e9", content);
        Assert.Contains("10.00 €", content);
        Assert.Contains("1.00 €", content);
        Assert.Contains("11.00 €", content);
        Assert.Contains("-11.00 €", content);
        Assert.Contains("0.00 €", content);
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
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
        {
            var (client, _) = await UnlockReconciliationDashboardAsync();
            var response = await client.GetAsync("/rapprochement/feuilles");

            response.EnsureSuccessStatusCode();

            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            AssertMainNavigationContainsExpectedLinks(content);
            AssertMainNavigationActive("/rapprochement", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
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
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
        {
            var (client, content) = await UnlockReconciliationDashboardAsync();

            Assert.Contains("Feuilles vendeurs", content);
            Assert.Contains("Correction des articles", content);
            Assert.Contains("href=\"/rapprochement/feuilles\"", content);
            Assert.Contains("Imprimer toutes les feuilles", content);
            Assert.Contains(">Vendeur</label>", content);
            Assert.Contains("<option value=\"\">Choisir un vendeur</option>", content);
            Assert.Contains("Imprimer une feuille", content);
            Assert.Contains("data-reconciliation-root", content);
            Assert.Contains("data-reconciliation-search", content);
            Assert.Contains("data-reconciliation-results", content);
            Assert.Contains("Filtrer les articles", content);

            await EnsureSoldItemsHaveSaleLinesAsync();

            var sheetsResponse = await client.GetAsync("/rapprochement/feuilles");
            sheetsResponse.EnsureSuccessStatusCode();
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationDashboardIsReadOnlyWhenEventIsNotPostSale()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Sale);

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var seller = new SellerEntity
                {
                    SellerCode = "RO-001",
                    FirstName = "Remi",
                    LastName = "Lecture",
                    Email = "remi.lecture@example.test"
                };

                dbContext.Sellers.Add(seller);
                await dbContext.SaveChangesAsync();

                dbContext.Items.Add(new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "RO-001-001",
                    Title = "Puzzle lecture seule",
                    SellerPriceCents = 500,
                    Status = "in_stock"
                });

                await dbContext.SaveChangesAsync();
            }

            var content = await UnlockReconciliationDashboardAsync(client);

            Assert.Contains("Le rapprochement est en lecture seule tant que la vente n'est pas en après-vente.", content);
            Assert.Contains("RO-001-001", content);
            Assert.Contains("Puzzle lecture seule", content);
            Assert.DoesNotContain("Imprimer toutes les feuilles", content);
            Assert.DoesNotContain("Imprimer une feuille", content);
            Assert.DoesNotContain("Marquer manquant", content);
            Assert.DoesNotContain("Remettre en stock", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationReadOnlyPostHandlersDoNotChangeItemStatusWhenEventIsNotPostSale()
    {
        var client = _factory.CreateClient();
        long inStockItemId;
        long missingItemId;
        await SetSalePhaseAsync(SalePhase.Sale);

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var seller = new SellerEntity
                {
                    SellerCode = "RO-002",
                    FirstName = "Lea",
                    LastName = "Operation"
                };

                dbContext.Sellers.Add(seller);
                await dbContext.SaveChangesAsync();

                var inStockItem = new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "RO-002-001",
                    Title = "Article en stock",
                    SellerPriceCents = 500,
                    Status = "in_stock"
                };
                var missingItem = new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "RO-002-002",
                    Title = "Article manquant",
                    SellerPriceCents = 600,
                    Status = "missing"
                };

                dbContext.Items.AddRange(inStockItem, missingItem);
                await dbContext.SaveChangesAsync();

                inStockItemId = inStockItem.Id;
                missingItemId = missingItem.Id;
            }

            var gateContent = await GetReconciliationGateAsync(client);
            _ = await UnlockReconciliationDashboardAsync(client);
            var markMissingResponse = await client.PostAsync(
                $"/rapprochement?handler=MarkMissing&itemId={inStockItemId}",
                BuildFormContent(gateContent));
            var markInStockResponse = await client.PostAsync(
                $"/rapprochement?handler=MarkInStock&itemId={missingItemId}",
                BuildFormContent(gateContent));

            foreach (var response in new[] { markMissingResponse, markInStockResponse })
            {
                response.EnsureSuccessStatusCode();
                var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.Contains("Le rapprochement est en lecture seule tant que la vente n'est pas en après-vente.", content);
            }

            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal("in_stock", (await dbContext.Items.SingleAsync(x => x.Id == inStockItemId)).Status);
                Assert.Equal("missing", (await dbContext.Items.SingleAsync(x => x.Id == missingItemId)).Status);
            }
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationDashboardRendersSellerAutocompleteAndLiveCorrectionRows()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.PostSale);

        try
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var seller = new SellerEntity
                {
                    SellerCode = "RA-010",
                    FirstName = "Alice",
                    LastName = "Autocomplete",
                    Email = "alice.autocomplete@example.test"
                };

                dbContext.Sellers.Add(seller);
                await dbContext.SaveChangesAsync();

                dbContext.Items.AddRange(
                    new ItemEntity
                    {
                        SellerId = seller.Id,
                        Reference = "RA-010-001",
                        Title = "Camion filtrable",
                        SellerPriceCents = 700,
                        Status = "in_stock"
                    },
                    new ItemEntity
                    {
                        SellerId = seller.Id,
                        Reference = "RA-010-002",
                        Title = "Puzzle deja manquant",
                        SellerPriceCents = 800,
                        Status = "missing"
                    });

                await dbContext.SaveChangesAsync();
            }

            var content = await UnlockReconciliationDashboardAsync(client);

            Assert.Contains("<select name=\"vendeur\" id=\"SheetInput_SellerId\" required>", content);
            Assert.Contains($"<option value=\"{await SellerIdByCodeAsync("RA-010")}\">Alice Autocomplete</option>", content);
            Assert.DoesNotContain("RA-010 - ", content);
            Assert.Contains("data-reconciliation-filter-text=\"ra-010-001 camion filtrable alice autocomplete\"", content);
            Assert.Contains("data-reconciliation-filter-text=\"ra-010-002 puzzle deja manquant alice autocomplete\"", content);
            Assert.Contains("Marquer manquant", content);
            Assert.Contains("Remettre en stock", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
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
    public async Task ReconciliationSheetsRedirectsToDashboardWhenEventIsNotPostSale()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await SetSalePhaseAsync(SalePhase.Sale);

        try
        {
            var gateContent = await GetReconciliationGateAsync(client);
            var unlockResponse = await client.PostAsync(
                "/rapprochement?handler=Unlock",
                BuildFormContent(gateContent, ("PasswordInput.Password", "test-password")));

            Assert.Equal(HttpStatusCode.Redirect, unlockResponse.StatusCode);

            var response = await client.GetAsync("/rapprochement/feuilles");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/rapprochement", response.Headers.Location?.OriginalString);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
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
                Title = "Article verrouille",
                SellerPriceCents = 500,
                Status = "in_stock"
            };
            var missingItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RL-001-002",
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
            $"/rapprochement?handler=MarkMissing&itemId={inStockItemId}",
            BuildFormContent(await GetReconciliationGateAsync(client)));
        var markInStockResponse = await client.PostAsync(
            $"/rapprochement?handler=MarkInStock&itemId={missingItemId}",
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
        await SetSalePhaseAsync(SalePhase.PostSale);

        try
        {
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
                        Title = "Puzzle rapprochement",
                        SellerPriceCents = 450,
                        Status = "in_stock"
                    },
                    new ItemEntity
                    {
                        SellerId = seller.Id,
                        Reference = "RC-001-002",
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
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationDashboardCanMarkItemMissingWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        await SetSalePhaseAsync(SalePhase.PostSale);

        try
        {
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
                $"/rapprochement?handler=MarkMissing&sellerId={await SellerIdByCodeAsync("RC-002")}",
                BuildFormContent(dashboardContent, ("itemId", itemId.ToString())));

            response.EnsureSuccessStatusCode();
            Assert.Equal("/rapprochement", response.RequestMessage?.RequestUri?.PathAndQuery);

            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Article marqué manquant.", content);
            Assert.Contains($"href=\"/rapprochement/feuilles?vendeur={await SellerIdByCodeAsync("RC-002")}\"", content);

            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var item = await dbContext.Items.SingleAsync(x => x.Id == itemId);
                Assert.Equal("missing", item.Status);
            }
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationDashboardCanMarkItemInStockWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        await SetSalePhaseAsync(SalePhase.PostSale);

        try
        {
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
                $"/rapprochement?handler=MarkInStock&sellerId={await SellerIdByCodeAsync("RC-003")}",
                BuildFormContent(dashboardContent, ("itemId", itemId.ToString())));

            response.EnsureSuccessStatusCode();
            Assert.Equal("/rapprochement", response.RequestMessage?.RequestUri?.PathAndQuery);

            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Article remis en stock.", content);
            Assert.Contains($"href=\"/rapprochement/feuilles?vendeur={await SellerIdByCodeAsync("RC-003")}\"", content);

            using (var scope = _factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var item = await dbContext.Items.SingleAsync(x => x.Id == itemId);
                Assert.Equal("in_stock", item.Status);
            }
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageRendersAllActiveSellerSheetsWhenUnlocked()
    {
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
        {
        var (client, _) = await UnlockReconciliationDashboardAsync();
        await SeedReconciliationSheetAsync(
            "RS-101",
            "Alice",
            "Feuille",
            "alice.feuille@example.test",
            "0600000001",
            ("RS-101-001", "Jeu manquant", "missing", 900),
            ("RS-101-002", "Puzzle a rendre", "in_stock", 450),
            ("RS-101-003", "Livre defectueux", "defective", 300));
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
        Assert.Contains("Livre defectueux", content);
        Assert.Contains("D\u00c9FECTUEUX", content);
        Assert.True(
            content.IndexOf("Articles à rendre", StringComparison.Ordinal) < content.IndexOf("Articles à payer", StringComparison.Ordinal),
            "Les articles à rendre doivent être imprimés avant les articles à payer.");
        Assert.Contains("seller-sheet-section-return", content);
        Assert.Contains("seller-sheet-section-payable", content);
        Assert.Contains("<th>Trouvé</th>", content);
        Assert.Contains("seller-sheet-checkbox", content);
        Assert.Contains("Total à payer", content);
        Assert.Contains("RS-102", content);
        Assert.Contains("Bruno Feuille", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageRendersSellerFeeDeductionRows()
    {
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
        {
        var (client, _) = await UnlockReconciliationDashboardAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "RS-FEE",
                FirstName = "Fee",
                LastName = "Vendeur",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                FeePaidCents = 200
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "RS-FEE-001",
                Title = "Article manquant",
                SellerPriceCents = 1000,
                Status = "missing"
            });

            for (var itemNumber = 2; itemNumber <= 16; itemNumber++)
            {
                dbContext.Items.Add(new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = $"RS-FEE-{itemNumber:000}",
                    Title = $"Article enregistre {itemNumber}",
                    SellerPriceCents = 100,
                    Status = "registered"
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/rapprochement/feuilles?vendeur={await SellerIdByCodeAsync("RS-FEE")}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Sous-total articles", content);
        Assert.Contains("Frais de vente (15%)", content);
        Assert.Contains("Frais attendus", content);
        Assert.Contains("Frais payés", content);
        Assert.Contains("Frais restant dus", content);
        Assert.Contains("Total à payer", content);
        Assert.Contains("1.50 €", content);
        Assert.Contains("6.50 €", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageFiltersBySellerWhenUnlocked()
    {
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
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

        var response = await client.GetAsync($"/rapprochement/feuilles?vendeur={await SellerIdByCodeAsync("RS-201")}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Claire Filtre", content);
        Assert.Contains("Article cible", content);
        Assert.DoesNotContain("Denis Filtre", content);
        Assert.DoesNotContain("Article autre vendeur", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageShowsFrenchMessageForUnknownSeller()
    {
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
        {
        var (client, _) = await UnlockReconciliationDashboardAsync();

        var response = await client.GetAsync("/rapprochement/feuilles?vendeur=999999");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Aucune feuille à imprimer pour ce vendeur.", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageShowsFrenchErrorForSoldItemWithoutSaleLine()
    {
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
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
                Title = "Article vendu incoherent",
                SellerPriceCents = 1200,
                Status = "sold"
            });

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/rapprochement/feuilles?vendeur={await SellerIdByCodeAsync("RS-500")}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Données de vente incohérentes pour l'article RS-500-001.", content);
        Assert.Contains("La feuille vendeur ne peut pas être imprimée.", content);
        Assert.DoesNotContain("Total à payer", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
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
        Assert.Contains("body:has(.seller-sheets-print-root) .app-status-header", content);
        Assert.Contains("body:has(.seller-sheets-print-root) .page-frame", content);
        Assert.Contains("body:has(.seller-sheets-print-root) .page-shell", content);
        Assert.Contains(".seller-sheet-print-page", content);
        Assert.Contains("page-break-after: always;", content);
        Assert.Contains(".seller-sheet-print-page:last-child", content);
        Assert.Contains("size: A4;", content);
        Assert.Contains("margin: 8mm;", content);
        Assert.Contains(".seller-sheet-print-page .data-table th", content);
        Assert.Contains(".seller-sheet-section-return", content);
        Assert.Contains(".seller-sheet-section-payable", content);
        Assert.Contains(".seller-sheet-checkbox", content);
        Assert.Contains("background: #f2f2f2;", content);
        Assert.Contains("padding: 0.6mm 1.2mm;", content);
        Assert.Contains("font-size: 8pt;", content);
        Assert.Contains("font-size: 7.5pt;", content);
        Assert.Contains(".seller-sheet-total-row", content);
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
    public async Task SellerCreatePageRendersCombinedSellerItemFormWithoutExcelUpload()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs/creer");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("excel-import-panel", content);
        Assert.DoesNotContain("excel-import-form", content);
        Assert.DoesNotContain("Préremplissage Excel", content);
        Assert.DoesNotContain("Fichier Excel vendeur", content);
        Assert.DoesNotContain("Pr\u00c3", content);
        Assert.DoesNotContain("<h2>Importer depuis Excel</h2>", content);
        Assert.Contains("Enregistrer le vendeur et les articles", content);
        Assert.DoesNotContain("Mode de saisie", content);
        Assert.DoesNotContain("value=\"InPerson\"", content);
        Assert.DoesNotContain("name=\"Input.IntakeMode\"", content);
        Assert.Contains("name=\"Input.Rows[4].Title\"", content);
        Assert.DoesNotContain("name=\"Input.Rows[5].Title\"", content);
        Assert.Contains("data-item-registration-table", content);
        Assert.Contains("data-add-item-row", content);
    }

    [Fact]
    public async Task SellerFormImportPageImportsFramaformsCsv()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);

        var form = await client.GetAsync("/vendeurs/importer");
        form.EnsureSuccessStatusCode();
        var formContent = await form.Content.ReadAsStringAsync();
        var decodedForm = WebUtility.HtmlDecode(formContent);
        Assert.Contains("Importer des soumissions", decodedForm);
        Assert.Contains("Fichier CSV ou TSV", decodedForm);
        Assert.Contains("Choisir un fichier CSV ou TSV", decodedForm);
        Assert.Contains("Aucun fichier sélectionné", decodedForm);
        Assert.Contains("data-auto-submit-file-input", decodedForm);
        Assert.Contains("data-auto-submit-file-form", decodedForm);
        Assert.Contains("screen-reader-label", decodedForm);
        Assert.DoesNotContain("Importer les nouvelles soumissions", decodedForm);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(ExtractRequestVerificationToken(formContent)), "__RequestVerificationToken");
        multipart.Add(
            new ByteArrayContent(Encoding.UTF8.GetBytes(SellerFormImportCsv)),
            "FormFile",
            "soumissions.csv");

        var response = await client.PostAsync("/vendeurs/importer?handler=Import", multipart);

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("2 dossiers importés", content);
        Assert.Contains("Import à vérifier", content);
        Assert.Contains("Rueil Malmaison", content);
        Assert.Contains("Saint-Égrève", content);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sellers = await dbContext.Sellers.Include(x => x.Items).Where(x => x.LastName == "Baumann" || x.LastName == "Bulteau").ToListAsync();
        Assert.Equal(2, sellers.Count);
        Assert.Contains(sellers, seller => seller.LastName == "Baumann" && seller.PostalCode == "92500" && seller.City == string.Empty && seller.ImportNotes!.Contains("Rueil Malmaison"));
        Assert.Contains(sellers, seller => seller.LastName == "Bulteau" && seller.City == "Saint-Égrève" && seller.Items.Count == 2);
    }

    private const string SellerFormImportCsv =
        """
        "Bourse aux jouets Test","","","","","","","","","","","","","","","",""
        "Détails de la soumission","","","","","","","","","","","","","","","",""
        "Séquentiel","SID","Heure de soumission","Heure de complétion","Heure de modification","Brouillon","Adresse IP","UID","Nom d'utilisateur","Nom","Prénom","Téléphone","Email","Adresse","Code Postal","Ville","Jouet,Prix"
        "1","33792452","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Baumann","Benjamin","0609710985","benjamin@example.test","29 avenue du mont Valérien","92500","Rueil Malmaison","Lego Pirate, 5"
        "2","33792475","11/05/2026 - 18:20","11/05/2026 - 18:20","11/05/2026 - 19:33","0","91.167.159.160","2780872","Zentiltoutou","Bulteau","Sarah","0600000000","sarah.bulteau@example.test","12 Rue Adolphe Muguet","38120","St Egreve","Lot 3 livres Monsieur Madame, 2
        Chateau Playmobil, 10"
        """;

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
    public async Task SellerDetailsDisplaysSellerFeeStatus()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-FEE",
                FirstName = "Claire",
                LastName = "Martin",
                Email = "claire@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                FeePaidCents = 200
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            for (var itemNumber = 1; itemNumber <= 16; itemNumber++)
            {
                dbContext.Items.Add(new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = $"BK-FEE-{itemNumber:000}",
                    Title = $"Jouet {itemNumber}",
                    SellerPriceCents = 100,
                    Status = "registered"
                });
            }

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Frais", content);
        Assert.Contains("4.00 €", content);
        Assert.Contains("payés 2.00 €", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysGuidedDepositFinalizationFlow()
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
                Email = "nina@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38000",
                City = "Grenoble",
                IdentityDocumentType = "CNI",
                IdentityDocumentNumber = "123456789",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 15),
                // Its item already carries a reference: the deposit was started.
                DepositStartedAt = DateTime.UtcNow
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-002-001",
                Title = "Puzzle",
                SellerPriceCents = 500,
                Status = "registered"
            });
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        await using var desk = await OpenClientDeskAsync(client, sellerId);
        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Nature de la pièce d'identité", content);
        Assert.Contains("CNI", content);
        Assert.Contains("Numéro de la pièce d'identité", content);
        Assert.Contains("123456789", content);
        Assert.Contains("Date de délivrance", content);
        Assert.Contains("15/01/2020", content);
        Assert.DoesNotContain("Autorité de délivrance", content);
        Assert.Contains("Dépôt à valider", content);
        Assert.Contains("data-seller-deposit-flow", content);
        Assert.Contains("handler=BeginDepositFinalization", content);
        Assert.Contains("data-seller-deposit-begin", content);
        Assert.Contains("Finaliser le dépôt", content);
        Assert.Contains("data-seller-deposit-dialog", content);
        Assert.Contains("<h4>Frais</h4>", content);
        Assert.Contains("Attendus : 2.00 €", content);
        Assert.Contains("Payés : 0.00 €", content);
        Assert.Contains("Reste : 2.00 €", content);
        Assert.Contains("Déduit au rapprochement", content);
        Assert.Contains("name=\"DepositFeeInput.FeePaid\"", content);
        Assert.Contains("handler=UpdateDepositFee", content);
        Assert.Contains("data-seller-deposit-fee-step", content);
        Assert.Contains("<h4>Contrat</h4>", content);
        Assert.Contains("Imprimer le contrat", content);
        Assert.Contains($"data-seller-deposit-print-url=\"/vendeurs/{sellerId}/contrat-depot?impression=auto\"", content);
        Assert.DoesNotContain("target=\"_blank\"", content);
        Assert.Contains("<h4>Signature</h4>", content);
        Assert.Contains("Le contrat papier est signé par le vendeur.", content);
        Assert.Contains("Valider le dépôt", content);
        Assert.Contains("data-seller-deposit-initial-step=\"fees\"", content);
        Assert.Contains("data-seller-deposit-print-step", content);
        Assert.Contains("data-seller-deposit-validate-step", content);
        Assert.Contains("data-seller-deposit-signature-confirmation", content);
        Assert.Contains("data-seller-deposit-submit", content);
        Assert.Contains("Retour", content);
        Assert.DoesNotContain($"<a class=\"button-link\" href=\"/vendeurs/{sellerId}/contrat-depot\">Imprimer le contrat</a>", content);
        Assert.DoesNotContain("Marquer en stock", content);
        Assert.DoesNotContain("handler=MarkItemInStock", content);

        var finalizationResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=BeginDepositFinalization",
            BuildFormContent(content));

        finalizationResponse.EnsureSuccessStatusCode();

        var finalizationContent = WebUtility.HtmlDecode(await finalizationResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-seller-deposit-auto-open", finalizationContent);
        Assert.Contains("data-seller-deposit-initial-step=\"fees\"", finalizationContent);
        Assert.Contains("Le contrat papier est signé par le vendeur.", finalizationContent);

        var feeUpdateResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=UpdateDepositFee",
            BuildFormContent(finalizationContent, ("DepositFeeInput.FeePaid", "2")));

        feeUpdateResponse.EnsureSuccessStatusCode();

        var feeUpdateContent = WebUtility.HtmlDecode(await feeUpdateResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-seller-deposit-auto-open", feeUpdateContent);
        Assert.Contains("data-seller-deposit-initial-step=\"print\"", feeUpdateContent);
        Assert.Contains("Payés : 2.00 €", feeUpdateContent);
        Assert.Contains("Reste : 0.00 €", feeUpdateContent);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifiedSeller = await verificationContext.Sellers.SingleAsync(x => x.Id == sellerId);
        Assert.Equal("deposit_pending", verifiedSeller.DepositStatus);
        Assert.Equal(200, verifiedSeller.FeePaidCents);
    }

    [Fact]
    public async Task SellerDetailsHidesSellerEditingWhenCatalogIsLocked()
    {
        var client = _factory.CreateClient();
        long sellerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await new SalePhaseService(dbContext).SetPhaseAsync(SalePhase.Sale);
            var seller = new SellerEntity
            {
                SellerCode = "BK-030",
                FirstName = "Claire",
                LastName = "Verrou",
                Email = "claire.verrou@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        try
        {
            var response = await client.GetAsync($"/vendeurs/{sellerId}");

            response.EnsureSuccessStatusCode();
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Les informations du vendeur et les articles sont en lecture seule dans le statut actuel de la vente.", content);
            Assert.DoesNotContain($"/vendeurs/{sellerId}/modifier", content);
            Assert.DoesNotContain("Ajouter des articles", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task SellerDetailsExplainsReadOnlyStateWhenContractIsSigned()
    {
        var client = _factory.CreateClient();
        long sellerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-LOCK",
                FirstName = "Claire",
                LastName = "Contrat",
                Email = "claire.contrat@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38000",
                City = "Grenoble",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-LOCK-001",
                Title = "Puzzle verrouillé",
                SellerPriceCents = 500,
                Status = "in_stock"
            });
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Le contrat de dépôt signé rend le vendeur et ses articles en lecture seule.", content);
        Assert.DoesNotContain("Les informations du vendeur et les articles sont en lecture seule dans le statut actuel de la vente.", content);
        Assert.DoesNotContain($"/vendeurs/{sellerId}/modifier", content);
        Assert.DoesNotContain("Ajouter des articles", content);
    }

    [Fact]
    public async Task SellerDetailsDoesNotDisplayIntakeModeControls()
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
                Email = "marie@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests"
            };

            dbContext.Sellers.Add(seller);

            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Mode de saisie", content);
        Assert.DoesNotContain("name=\"Input.IntakeMode\"", content);
        Assert.DoesNotContain("value=\"InPerson\"", content);
        Assert.DoesNotContain("value=\"EmailPreregistration\"", content);
        Assert.DoesNotContain("<select", content);
    }

    [Fact]
    public async Task SellerDetailsDisplaysFiveDefaultRegistrationRowsAndAddRowButton()
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
        Assert.Contains("name=\"Input.Rows[4].Title\"", content);
        Assert.DoesNotContain("name=\"Input.Rows[5].Title\"", content);
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
    public async Task SiteCssLeftAlignsTableActions()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Matches(
            new Regex(
                @"\.table-action-button\s*\{[^}]*text-align:\s*left;",
                RegexOptions.Singleline),
            content);
        Assert.Matches(
            new Regex(
                @"\.table-actions-cell\s*\{[^}]*justify-content:\s*flex-start;",
                RegexOptions.Singleline),
            content);
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
        Assert.Contains("Déjà dans le panier", content);
        Assert.Contains("checkout-result-row-disabled", content);
        Assert.Contains("data-checkout-remove", content);
        Assert.Contains("/api/caisse/validation", content);
        Assert.Contains("/api/ventes", content);
        Assert.Contains("checkout-conflict-row", content);
        Assert.Contains("buyerAddress", content);
        Assert.Contains("createInvoice", content);
        Assert.Contains("paymentMethod", content);
        Assert.Contains("chequeNumber", content);
        Assert.Contains("chequeBankName", content);
        Assert.Contains("data-checkout-payment-method", content);
        Assert.Contains("buyerName", content);
        Assert.Contains("buyerPostalCode", content);
        Assert.Contains("buyerCity", content);
        Assert.Contains("checkoutCitiesByPostalCode", content);
        Assert.Contains("data-checkout-save-print", content);
        Assert.Contains("data-checkout-invoice-lines", content);
        Assert.Contains("data-checkout-invoice-customer-text", content);
        Assert.Contains("join(\"\\n\")", content);
        Assert.Contains("window.print()", content);
    }

    [Fact]
    public async Task SiteJsContainsDataListFiltering()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/js/site.js");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("initializeDataList", content);
        Assert.Contains("data-data-list-root", content);
        Assert.Contains("data-data-list-search", content);
        Assert.Contains("data-data-list-status-filter", content);
        Assert.Contains("data-data-list-row", content);
        Assert.Contains("dataListFilterText", content);
        Assert.Contains("initializeDataListSorting", content);
        Assert.Contains("data-data-list-sort", content);
        Assert.Contains("dataListSortValue", content);
        Assert.Contains("aria-sort", content);
        Assert.Contains("appendChild", content);
    }

    [Fact]
    public async Task SiteCssContainsDataListFilterAlignment()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(".data-list-filter-bar", content);
        Assert.Contains(".data-list-filter-field", content);
        Assert.Contains(".data-list-sort-button", content);
        Assert.Contains("cursor: pointer;", content);
        Assert.Contains("th[aria-sort=\"ascending\"]", content);
        Assert.Contains("th[aria-sort=\"descending\"]", content);
        Assert.Contains("grid-template-columns: max-content minmax(0, 1fr);", content);
        Assert.Contains("align-items: center;", content);
        Assert.Contains("column-gap: 0.5rem;", content);
        Assert.Contains(".data-list-seller-table", content);
        Assert.Contains("table-layout: fixed;", content);
        Assert.Contains(".data-list-count-column", content);
        Assert.Contains(".data-list-size-xxs", content);
        Assert.Contains("width: 2.3rem;", content);
        Assert.Contains(".data-list-size-xs", content);
        Assert.Contains("width: 2.6rem;", content);
        Assert.Contains(".data-list-seller-table .data-list-count-column", content);
        Assert.Contains("padding-left: 0.2rem;", content);
        Assert.Contains("padding-right: 0.2rem;", content);
        Assert.Contains(".data-list-size-m", content);
        Assert.Contains("width: 5.75rem;", content);
        Assert.Contains(".data-list-size-l", content);
        Assert.Contains("width: 10.25rem;", content);
        Assert.Contains(".data-list-seller-table th.data-list-count-column", content);
        Assert.Contains("white-space: normal;", content);
        Assert.Contains("letter-spacing: 0;", content);
        Assert.Contains(".data-list-sale-table", content);
        Assert.Contains(".data-list-sale-table th,", content);
        Assert.Contains(".data-list-sale-table td", content);
        Assert.Contains(".data-list-sale-id-column", content);
    }

    [Fact]
    public async Task SiteCssContainsSellerIndexTableLayout()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/css/site.css");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(".seller-index-table", content);
        Assert.Contains("overflow-wrap: anywhere;", content);
        Assert.Contains("table-layout: fixed;", content);
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
        Assert.Contains(".checkout-buyer-fields", content);
        Assert.Contains(".checkout-totals", content);
        Assert.Contains(".checkout-modal", content);
        Assert.Contains(".checkout-conflict-row", content);
        Assert.Contains(".checkout-print-invoice", content);
        Assert.Contains(".checkout-invoice-parties", content);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);", content);
        Assert.Contains(".checkout-invoice-designation-column", content);
        Assert.Contains("width: 56%;", content);
        Assert.Contains("@media print", content);
        Assert.Contains("body.checkout-printing", content);
        Assert.Contains("body.checkout-printing .main-navigation", content);
        Assert.Contains("body.checkout-printing .app-status-header", content);
    }

    [Fact]
    public async Task IsereCitySuggestionDataIsServedLocally()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/data/isere-postal-cities.json");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Contains(root.GetProperty("38000").EnumerateArray(), city => city.GetString() == "Grenoble");
        Assert.Contains(root.GetProperty("38100").EnumerateArray(), city => city.GetString() == "Grenoble");
        Assert.True(root.TryGetProperty("38300", out var bourgoinPostalCode));
        Assert.Contains(bourgoinPostalCode.EnumerateArray(), city => city.GetString() == "Bourgoin-Jallieu");
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
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            };
            var soldItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-005-002",
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
    public async Task SellerDetailsValidatesSignedDepositAndMovesItemsToStock()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-008",
                FirstName = "Laura",
                LastName = "Girard",
                Email = "laura@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38000",
                City = "Grenoble",
                IdentityDocumentType = "CNI",
                IdentityDocumentNumber = "123456789",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 15),
                // Its item already carries a reference: the deposit was started.
                DepositStartedAt = DateTime.UtcNow
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-008-001",
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            });
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
        }

        await using var desk = await OpenClientDeskAsync(client, sellerId);
        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Finaliser le dépôt", content);
        Assert.Contains("Valider le dépôt", content);
        Assert.Contains("Le contrat papier est signé par le vendeur.", content);
        Assert.DoesNotContain("handler=MarkItemInStock", content);

        var finalizationResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=BeginDepositFinalization",
            BuildFormContent(content));

        finalizationResponse.EnsureSuccessStatusCode();

        var finalizationContent = WebUtility.HtmlDecode(await finalizationResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-seller-deposit-auto-open", finalizationContent);

        var postResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=ValidateDeposit",
            BuildFormContent(finalizationContent));

        postResponse.EnsureSuccessStatusCode();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifiedSeller = await verificationContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);
        Assert.Equal("deposit_validated", verifiedSeller.DepositStatus);
        Assert.Equal(0, verifiedSeller.FeePaidCents);
        Assert.Equal("in_stock", Assert.Single(verifiedSeller.Items).Status);
    }

    [Fact]
    public async Task SellerDetailsShowsMissingFieldsWhenDepositValidationIsRejected()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-MISS",
                FirstName = "Marie",
                LastName = "Incomplete",
                Email = "marie@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-MISS-001",
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            });
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
        }

        await using var desk = await OpenClientDeskAsync(client, sellerId);
        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Finaliser le dépôt", content);
        Assert.Contains("Le contrat papier est signé par le vendeur.", content);

        var postResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=BeginDepositFinalization",
            BuildFormContent(content));

        postResponse.EnsureSuccessStatusCode();

        var rejectedContent = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Contains("Corrigez les points ci-dessous avant de finaliser le dépôt.", rejectedContent);
        Assert.Contains("Fiche vendeur", rejectedContent);
        Assert.Contains("Le code postal est obligatoire.", rejectedContent);
        Assert.Contains("La ville est obligatoire.", rejectedContent);
        Assert.Contains("La nature de la pièce d'identité est obligatoire.", rejectedContent);
        Assert.Contains("Le numéro de la pièce d'identité est obligatoire.", rejectedContent);
        Assert.Contains("La date de délivrance est obligatoire.", rejectedContent);
        Assert.DoesNotContain("L'autorité de délivrance est obligatoire.", rejectedContent);
        Assert.DoesNotContain("data-seller-deposit-auto-open", rejectedContent);
        Assert.Contains("Corriger la fiche vendeur", rejectedContent);
        Assert.Contains($"href=\"/vendeurs/{sellerId}/modifier\"", rejectedContent);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifiedSeller = await verificationContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);
        Assert.Equal("deposit_pending", verifiedSeller.DepositStatus);
        Assert.Equal("registered", Assert.Single(verifiedSeller.Items).Status);
    }

    [Fact]
    public async Task SellerDetailsShowsArticleCorrectionActionWhenDepositValidationRejectsMissingItemPrice()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-ITEM",
                FirstName = "Alice",
                LastName = "Article",
                Email = "alice.article@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38000",
                City = "Grenoble",
                IdentityDocumentType = "CNI",
                IdentityDocumentNumber = "123456789",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 15)
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-ITEM-001",
                Title = "Jouet sans prix",
                SellerPriceCents = null,
                Status = "registered"
            });
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
        }

        await using var desk = await OpenClientDeskAsync(client, sellerId);
        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=BeginDepositFinalization",
            BuildFormContent(content));

        postResponse.EnsureSuccessStatusCode();

        var rejectedContent = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Contains("Tous les articles doivent avoir un prix vendeur avant de valider le dépôt.", rejectedContent);
        Assert.DoesNotContain("data-seller-deposit-auto-open", rejectedContent);
        Assert.Contains("Corriger les articles", rejectedContent);
        Assert.Contains("href=\"#articles\"", rejectedContent);
        Assert.DoesNotContain("Corriger le vendeur", rejectedContent);
    }

    [Fact]
    public async Task SellerDepositContractPagePrintsSellerAndItemRecap()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-CON",
                FirstName = "Alice",
                LastName = "Contrat",
                Email = "alice@example.test",
                Phone = "0123",
                PostalAddress = "1 rue du Contrat",
                PostalCode = "38000",
                City = "Grenoble",
                IdentityDocumentType = "Passeport",
                IdentityDocumentNumber = "P123456789",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 15)
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            dbContext.Items.AddRange(
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "BK-CON-001",
                    Title = "Puzzle contrat",
                    SellerPriceCents = 500,
                    Status = "registered"
                },
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "BK-CON-002",
                    Title = "Camion contrat",
                    SellerPriceCents = 700,
                    Status = "registered"
                });
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}/contrat-depot");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Contrat de dépôt", content);
        Assert.Contains("Imprimer", content);
        Assert.DoesNotContain("data-deposit-contract-auto-print", content);
        Assert.Contains("Association Familiale", content);
        Assert.Contains("33 Av. de l'Europe, 38120 Saint-Égrève", content);
        Assert.Contains("SIRET : 306 041 997 00058", content);
        Assert.DoesNotContain("Nature de la pièce d'identité", content);
        Assert.DoesNotContain("Passeport", content);
        Assert.DoesNotContain("Numéro de la pièce d'identité", content);
        Assert.DoesNotContain("P123456789", content);
        Assert.DoesNotContain("Date de délivrance", content);
        Assert.DoesNotContain("15/01/2020", content);
        Assert.DoesNotContain("Autorité de délivrance", content);
        Assert.Contains("Ce document papier signé constitue la preuve du dépôt.", content);
        Assert.Contains("Frais vendeur : 2.00 € par tranche commencée de 15 articles", content);
        Assert.Contains("Frais attendus : 2.00 €", content);
        Assert.Contains("Frais payés : 0.00 €", content);
        Assert.Contains("Paiement : à régler", content);
        Assert.Contains("BK-CON-001", content);
        Assert.Contains("Puzzle contrat", content);
        Assert.Contains("5.00 €", content);
        Assert.Contains("J'accepte le règlement de la bourse aux jouets", content);
        Assert.Contains("Signature vendeur", content);
        Assert.DoesNotContain("Signature association", content);

        var autoPrintResponse = await client.GetAsync($"/vendeurs/{sellerId}/contrat-depot?impression=auto");
        autoPrintResponse.EnsureSuccessStatusCode();

        var autoPrintContent = WebUtility.HtmlDecode(await autoPrintResponse.Content.ReadAsStringAsync());
        Assert.Contains("data-deposit-contract-auto-print", autoPrintContent);
        Assert.Contains("Fermer cet onglet", autoPrintContent);
        Assert.Contains("Contrat de dépôt", autoPrintContent);
        Assert.Contains("Puzzle contrat", autoPrintContent);
    }

    [Fact]
    public async Task SellerDetailsCancelsDepositValidationWithLocalAdminPassword()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-CAN",
                FirstName = "Aline",
                LastName = "Annule",
                Email = "aline@example.test",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38000",
                City = "Grenoble",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            dbContext.Items.AddRange(
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "BK-CAN-001",
                    Title = "Puzzle",
                    SellerPriceCents = 500,
                    Status = "in_stock"
                },
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "BK-CAN-002",
                    Title = "Train vendu",
                    SellerPriceCents = 700,
                    Status = "sold"
                },
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "BK-CAN-003",
                    Title = "Jeu manquant",
                    SellerPriceCents = 600,
                    Status = "missing"
                },
                new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "BK-CAN-004",
                    Title = "Camion défectueux",
                    SellerPriceCents = 800,
                    Status = "defective"
                });
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        var pageResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        pageResponse.EnsureSuccessStatusCode();
        var pageContent = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());
        Assert.Contains("Annuler la validation du dépôt", pageContent);
        Assert.Contains("Mot de passe administrateur", pageContent);

        var rejectedResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=CancelDepositValidation",
            BuildFormContent(pageContent, ("CancellationInput.AdminPassword", "wrong-password")));

        rejectedResponse.EnsureSuccessStatusCode();
        var rejectedContent = WebUtility.HtmlDecode(await rejectedResponse.Content.ReadAsStringAsync());
        Assert.Contains("Mot de passe administrateur incorrect.", rejectedContent);

        using (var rejectedScope = _factory.Services.CreateScope())
        {
            var rejectedContext = rejectedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rejectedSeller = await rejectedContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);
            Assert.Equal("deposit_validated", rejectedSeller.DepositStatus);
            Assert.Contains(rejectedSeller.Items, item => item.Reference == "BK-CAN-001" && item.Status == "in_stock");
        }

        var postResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=CancelDepositValidation",
            BuildFormContent(pageContent, ("CancellationInput.AdminPassword", "test-password")));

        postResponse.EnsureSuccessStatusCode();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifiedSeller = await verificationContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);
        Assert.Equal("deposit_pending", verifiedSeller.DepositStatus);
        // Every item seeded above has an explicit Reference, so the key is
        // never null here.
        var statusesByReference = verifiedSeller.Items.ToDictionary(x => x.Reference!, x => x.Status);
        Assert.Equal("registered", statusesByReference["BK-CAN-001"]);
        Assert.Equal("sold", statusesByReference["BK-CAN-002"]);
        Assert.Equal("missing", statusesByReference["BK-CAN-003"]);
        Assert.Equal("defective", statusesByReference["BK-CAN-004"]);
    }

    [Fact]
    public async Task DepositFinalizationAllocatesConsecutiveReferencesAndItemsReachCheckoutCatalog()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);
        long sellerId;
        long itemOneId;
        long itemTwoId;
        long itemThreeId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "K-701",
                FirstName = "Karine",
                LastName = "Depot",
                Phone = "0123456789",
                PostalAddress = "1 rue du Dépôt",
                PostalCode = "38000",
                City = "Grenoble",
                IdentityDocumentType = "CNI",
                IdentityDocumentNumber = "K701K701",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 15)
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var itemOne = new ItemEntity { SellerId = seller.Id, Title = "Puzzle un", SellerPriceCents = 500, Status = "registered" };
            var itemTwo = new ItemEntity { SellerId = seller.Id, Title = "Puzzle deux", SellerPriceCents = 600, Status = "registered" };
            var itemThree = new ItemEntity { SellerId = seller.Id, Title = "Puzzle trois", SellerPriceCents = 700, Status = "registered" };
            dbContext.Items.AddRange(itemOne, itemTwo, itemThree);
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
            itemOneId = itemOne.Id;
            itemTwoId = itemTwo.Id;
            itemThreeId = itemThree.Id;
        }

        // Items must show no reference at all before the deposit starts - both
        // in the database and rendered as an em dash on the seller page.
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seededItems = await dbContext.Items.Where(x => x.SellerId == sellerId).ToListAsync();
            Assert.All(seededItems, item => Assert.Null(item.Reference));
        }

        var detailsResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsContent = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(3, Regex.Matches(detailsContent, "<td>—</td>").Count);

        // Starting the deposit at a desk is the allocation trigger under test.
        long deskId;
        IReadOnlyList<char> deskLetters;
        using (var scope = _factory.Services.CreateScope())
        {
            var desk = await scope.ServiceProvider.GetRequiredService<DepositDeskService>().OpenDeskAsync();
            deskId = desk.DeskId;
            deskLetters = desk.Letters;
            var start = await scope.ServiceProvider.GetRequiredService<IntakeService>().StartDepositAsync(sellerId, deskId);
            Assert.Equal(DepositAllocationStatus.Done, start.Status);
        }

        string referenceOne;
        string referenceTwo;
        string referenceThree;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var itemOne = await dbContext.Items.SingleAsync(x => x.Id == itemOneId);
            var itemTwo = await dbContext.Items.SingleAsync(x => x.Id == itemTwoId);
            var itemThree = await dbContext.Items.SingleAsync(x => x.Id == itemThreeId);

            Assert.NotNull(itemOne.Reference);
            Assert.NotNull(itemTwo.Reference);
            Assert.NotNull(itemThree.Reference);
            referenceOne = itemOne.Reference!;
            referenceTwo = itemTwo.Reference!;
            referenceThree = itemThree.Reference!;
        }

        // One of the desk's letters, numbered consecutively in item-creation
        // order. The class shares one database, so the run's start is not fixed.
        Assert.Contains(referenceOne[0], deskLetters);
        var firstNumber = int.Parse(referenceOne[1..], CultureInfo.InvariantCulture);
        Assert.Equal(ItemReference.Format(referenceOne[0], firstNumber + 1), referenceTwo);
        Assert.Equal(ItemReference.Format(referenceOne[0], firstNumber + 2), referenceThree);

        var finalizationResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=BeginDepositFinalization",
            BuildFormContent(detailsContent));
        finalizationResponse.EnsureSuccessStatusCode();
        var finalizationContent = WebUtility.HtmlDecode(await finalizationResponse.Content.ReadAsStringAsync());

        var feeUpdateResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=UpdateDepositFee",
            BuildFormContent(finalizationContent, ("DepositFeeInput.FeePaid", "2")));
        feeUpdateResponse.EnsureSuccessStatusCode();
        var feeUpdateContent = WebUtility.HtmlDecode(await feeUpdateResponse.Content.ReadAsStringAsync());

        var contractResponse = await client.GetAsync($"/vendeurs/{sellerId}/contrat-depot");
        contractResponse.EnsureSuccessStatusCode();
        var contractContent = WebUtility.HtmlDecode(await contractResponse.Content.ReadAsStringAsync());
        Assert.Contains(referenceOne, contractContent);
        Assert.Contains(referenceTwo, contractContent);
        Assert.Contains(referenceThree, contractContent);

        var validateResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=ValidateDeposit",
            BuildFormContent(feeUpdateContent));
        validateResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = await dbContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);
            Assert.Equal("deposit_validated", seller.DepositStatus);
            Assert.All(seller.Items, item => Assert.Equal("in_stock", item.Status));
            Assert.Equal([referenceOne, referenceTwo, referenceThree], seller.Items.OrderBy(x => x.Id).Select(x => x.Reference));

            // Free the desk's letters for the other tests sharing this database.
            await scope.ServiceProvider.GetRequiredService<DepositDeskService>().CloseDeskAsync(deskId);
        }

        var checkoutResponse = await client.GetAsync("/caisse");
        checkoutResponse.EnsureSuccessStatusCode();
        var checkoutContent = await checkoutResponse.Content.ReadAsStringAsync();
        Assert.Contains($"\"reference\":\"{referenceOne}\"", checkoutContent);
        Assert.Contains($"\"reference\":\"{referenceTwo}\"", checkoutContent);
        Assert.Contains($"\"reference\":\"{referenceThree}\"", checkoutContent);
    }

    [Fact]
    public async Task DepositCancellationPreservesReferencesAndTopUpContinuesNumbering()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);
        long sellerId;
        long itemOneId;
        long itemTwoId;
        long itemThreeId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "T-702",
                FirstName = "Theo",
                LastName = "Topup",
                Phone = "0123456789",
                PostalAddress = "2 rue du Dépôt",
                PostalCode = "38000",
                City = "Grenoble",
                IdentityDocumentType = "CNI",
                IdentityDocumentNumber = "T702T702",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 15),
                DepositStatus = "deposit_validated",
                DepositStartedAt = DateTime.UtcNow
            };
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var itemOne = new ItemEntity { SellerId = seller.Id, Reference = "T001", Title = "Camion un", SellerPriceCents = 500, Status = "in_stock" };
            var itemTwo = new ItemEntity { SellerId = seller.Id, Reference = "T002", Title = "Camion deux", SellerPriceCents = 600, Status = "in_stock" };
            var itemThree = new ItemEntity { SellerId = seller.Id, Reference = "T003", Title = "Camion trois", SellerPriceCents = 700, Status = "in_stock" };
            dbContext.Items.AddRange(itemOne, itemTwo, itemThree);

            // These numbers came from the T counter, so it has moved past them.
            var letterT = await dbContext.ReferenceLetters.SingleAsync(x => x.Letter == "T");
            letterT.LastNumber = Math.Max(letterT.LastNumber, 3);
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
            itemOneId = itemOne.Id;
            itemTwoId = itemTwo.Id;
            itemThreeId = itemThree.Id;
        }

        var pageResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        pageResponse.EnsureSuccessStatusCode();
        var pageContent = WebUtility.HtmlDecode(await pageResponse.Content.ReadAsStringAsync());

        var cancelResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=CancelDepositValidation",
            BuildFormContent(pageContent, ("CancellationInput.AdminPassword", "test-password")));
        cancelResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = await dbContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);
            Assert.Equal("deposit_pending", seller.DepositStatus);
            Assert.NotNull(seller.DepositStartedAt);

            // Load-bearing: the physical labels are already stuck on the toys,
            // so cancelling the deposit validation must never touch an
            // already-allocated reference.
            Assert.Equal("T001", seller.Items.Single(x => x.Id == itemOneId).Reference);
            Assert.Equal("T002", seller.Items.Single(x => x.Id == itemTwoId).Reference);
            Assert.Equal("T003", seller.Items.Single(x => x.Id == itemThreeId).Reference);
            Assert.All(seller.Items, item => Assert.Equal("registered", item.Status));
        }

        // A row added after the start gets its reference as it is saved: the
        // next number of a letter the desk holds.
        long deskId;
        string referenceFour;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var desk = await scope.ServiceProvider.GetRequiredService<DepositDeskService>().OpenDeskAsync();
            deskId = desk.DeskId;
            var lastNumbersBefore = (await dbContext.ReferenceLetters.AsNoTracking().ToListAsync())
                .ToDictionary(x => x.Letter[0], x => x.LastNumber);

            var added = await scope.ServiceProvider.GetRequiredService<IntakeService>().CreateItemBatchAsync(
                new CreateItemBatchRequest(sellerId, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Camion quatre", 800)], deskId));

            Assert.Equal(DepositAllocationStatus.Done, added.Status);
            referenceFour = Assert.Single(added.AssignedReferences);
            Assert.Contains(referenceFour[0], desk.Letters);
            Assert.Equal(ItemReference.Format(referenceFour[0], lastNumbersBefore[referenceFour[0]] + 1), referenceFour);
        }

        var refreshedResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        refreshedResponse.EnsureSuccessStatusCode();
        var refreshedContent = WebUtility.HtmlDecode(await refreshedResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain("<td>—</td>", refreshedContent);

        var finalizationResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=BeginDepositFinalization",
            BuildFormContent(refreshedContent));
        finalizationResponse.EnsureSuccessStatusCode();
        var finalizationContent = WebUtility.HtmlDecode(await finalizationResponse.Content.ReadAsStringAsync());

        // The fee step no longer allocates: it leaves every reference as it is.
        var feeUpdateResponse = await client.PostAsync(
            $"/vendeurs/{sellerId}?handler=UpdateDepositFee",
            BuildFormContent(finalizationContent, ("DepositFeeInput.FeePaid", "2")));
        feeUpdateResponse.EnsureSuccessStatusCode();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var verifiedSeller = await verificationContext.Sellers.Include(x => x.Items).SingleAsync(x => x.Id == sellerId);

        Assert.Equal("T001", verifiedSeller.Items.Single(x => x.Id == itemOneId).Reference);
        Assert.Equal("T002", verifiedSeller.Items.Single(x => x.Id == itemTwoId).Reference);
        Assert.Equal("T003", verifiedSeller.Items.Single(x => x.Id == itemThreeId).Reference);
        Assert.Equal(referenceFour, verifiedSeller.Items.Single(x => x.Title == "Camion quatre").Reference);

        // Free the desk's letters for the other tests sharing this database.
        await verificationScope.ServiceProvider.GetRequiredService<DepositDeskService>().CloseDeskAsync(deskId);
    }

    [Fact]
    public async Task SellerDetailsDoesNotDisplayAdminDefectAction()
    {
        var client = _factory.CreateClient();
        long sellerId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "BK-009",
                FirstName = "Marion",
                LastName = "Defaut",
                Email = "marion@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var registeredItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-009-001",
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            };
            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-009-002",
                Title = "Camion",
                SellerPriceCents = 700,
                Status = "in_stock"
            };

            dbContext.Items.AddRange(registeredItem, inStockItem);
            await dbContext.SaveChangesAsync();

            sellerId = seller.Id;
        }

        var response = await client.GetAsync($"/vendeurs/{sellerId}");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Marquer défectueux", content);
        Assert.DoesNotContain("handler=MarkItemDefective", content);
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
                Email = "nora@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CK-001-001",
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
        Assert.Contains("Étape 1", content);
        Assert.Contains("Acheteur", content);
        Assert.Contains("Étape 2", content);
        Assert.Contains("Articles", content);
        Assert.Contains("Étape 3", content);
        Assert.Contains("Paiement", content);
        Assert.Contains("Sous-total", content);
        Assert.Contains("Frais association familiale", content);
        Assert.DoesNotContain("Frais de caisse", content);
        Assert.Contains("Total", content);
        Assert.Contains("checkout-catalog-data", content);
        Assert.Contains("data-checkout-root", content);
        Assert.Contains("data-checkout-buyer-name", content);
        Assert.Contains("data-checkout-buyer-postal-code", content);
        Assert.Contains("data-checkout-buyer-city", content);
        Assert.Contains("data-city-select", content);
        Assert.DoesNotContain("data-checkout-buyer-city-options", content);
        Assert.DoesNotContain("checkout-buyer-city-options", content);
        Assert.Contains("data-checkout-buyer-message", content);
        Assert.Contains("data-postal-city-root", content);
        Assert.Contains("data-postal-code-input", content);
        Assert.Contains("data-city-input", content);
        Assert.Contains("Nom ou structure", content);
        Assert.Contains("Code postal", content);
        Assert.Contains("Ville", content);
        Assert.Contains("data-checkout-search", content);
        Assert.Contains("data-checkout-results", content);
        Assert.Contains("data-checkout-sticky-summary", content);
        Assert.Contains("data-checkout-sticky-count", content);
        Assert.Contains("data-checkout-sticky-total", content);
        Assert.Contains("data-checkout-cart", content);
        Assert.Contains("data-checkout-subtotal", content);
        Assert.Contains("data-checkout-finalize", content);
        Assert.Contains("Finaliser", content);
        Assert.Contains("data-checkout-modal", content);
        Assert.Contains("data-checkout-modal-count", content);
        Assert.Contains("data-checkout-save-sale", content);
        Assert.Contains("Valider sans imprimer", content);
        Assert.Contains("data-checkout-buyer-address", content);
        Assert.Contains("Adresse", content);
        Assert.DoesNotContain("Détails du reçu", content);
        Assert.Contains("data-checkout-save-print", content);
        Assert.Contains("Valider et imprimer la facture", content);
        Assert.Contains("Mode de paiement", content);
        Assert.Contains("CB", content);
        Assert.Contains("Espèces", content);
        Assert.Contains("Chèque", content);
        Assert.Contains("data-checkout-payment-method", content);
        Assert.Contains("data-checkout-cheque-number", content);
        Assert.Contains("data-checkout-cheque-bank-name", content);
        Assert.Contains("list=\"checkout-cheque-bank-options\"", content);
        Assert.Contains("id=\"checkout-cheque-bank-options\"", content);
        var bankSuggestions = Regex.Matches(content, "<option value=\"(?<bank>[^\"]+)\"></option>")
            .Select(match => match.Groups["bank"].Value)
            .ToList();
        Assert.Equal(
            [
                "Crédit Agricole",
                "La Banque Postale",
                "Caisse d'Épargne",
                "Société Générale",
                "Banque Populaire",
                "Crédit Mutuel",
                "BNP Paribas",
                "LCL",
                "CIC",
                "CCF",
                "Axa Banque",
                "Crédit Coopératif",
                "Crédit Maritime",
                "Banque Palatine",
                "Banque BCP"
            ],
            bankSuggestions);
        Assert.Contains("data-checkout-invoice", content);
        Assert.DoesNotContain("data-checkout-invoice-header", content);
        Assert.Contains("Facture <span data-checkout-invoice-number>", content);
        Assert.Contains("/images/logo-afse.jpg", content);
        Assert.Contains("TVA non applicable en vertu de l'article 293 B du CGI.", content);
        Assert.Contains("Association Familiale", content);
        Assert.Contains("SIRET : 306 041 997 00058", content);
        Assert.Contains("Association loi 1901", content);
        Assert.DoesNotContain("Statut :", content);
        Assert.DoesNotContain("<h2>Acheteur</h2>", content);
        Assert.DoesNotContain("<h2>Adresse client</h2>", content);
        Assert.Contains("data-checkout-invoice-customer-text", content);
        Assert.Contains("P.U. HT", content);
        Assert.DoesNotContain("Prix unitaire HT", content);
        Assert.Contains("checkout-invoice-designation-column", content);
        Assert.Contains("data-checkout-invoice-lines", content);
        Assert.Contains("data-checkout-invoice-total-including-vat", content);
        Assert.DoesNotContain("Reçu de vente", content);
        Assert.Contains("checkout-results-frame", content);
        Assert.Contains("<th>REF</th>", content);
        Assert.Contains("<th>Ajouter</th>", content);
        Assert.Contains("CK-001-001", content);
        Assert.Contains("Jeu de cubes", content);
    }

    [Fact]
    public async Task CheckoutPageRendersItemSearchInsideCartPanel()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/caisse");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var cartPanelIndex = content.IndexOf("checkout-cart-panel", StringComparison.Ordinal);
        var cartHeadingIndex = content.IndexOf("checkout-step-items-title", StringComparison.Ordinal);
        var searchIndex = content.IndexOf("data-checkout-search", StringComparison.Ordinal);
        var cartBodyIndex = content.IndexOf("data-checkout-cart", StringComparison.Ordinal);

        Assert.True(cartPanelIndex >= 0, "The checkout page should render the cart panel.");
        Assert.True(cartHeadingIndex > cartPanelIndex, "The Articles heading should be inside the cart panel.");
        Assert.True(searchIndex > cartHeadingIndex, "The item search should be rendered inside the Articles section.");
        Assert.True(searchIndex < cartBodyIndex, "The item search should appear before the cart rows.");
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
                Phone = "0601020304",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CK-002-001",
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
                Email = "claire.valide@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "CV-001-001",
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
        long cashRegisterId;

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
                Title = "Jeu indisponible",
                SellerPriceCents = 800,
                Status = "sold"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-001");
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
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-001",
                FirstName = "Sonia",
                LastName = "Vente",
                Email = "sonia.vente@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-001-001",
                Title = "Jeu vendu",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-003");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "Client vente",
                buyerPostalCode = "38100",
                buyerCity = "Grenoble",
                paymentMethod = "cash"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sale = await dbContext.Sales.Include(x => x.SaleLines).SingleAsync(x => x.SaleLines.Any(line => line.ItemId == itemId));
            Assert.Equal(1500, sale.SubtotalSellerPriceCents);
            Assert.Equal(150, sale.CheckoutFeeCents);
            Assert.Equal(1650, sale.TotalPaidCents);
            Assert.Equal(cashRegisterId, sale.CashRegisterId);
            Assert.Equal("cash", sale.PaymentMethod);
            Assert.Null(sale.ChequeNumber);
            Assert.Null(sale.ChequeBankName);
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

    // The administration page shows one tab at a time; onglet picks which.
    private static async Task<string> UnlockSaleStatusPageAsync(HttpClient client, string? onglet = null)
    {
        var gateContent = await GetSaleStatusGateAsync(client);

        var response = await client.PostAsync(
            $"{AdminPath}?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "test-password")));

        response.EnsureSuccessStatusCode();

        if (onglet is not null)
        {
            response = await client.GetAsync($"{AdminPath}?onglet={onglet}");
            response.EnsureSuccessStatusCode();
        }

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> GetSaleStatusGateAsync(HttpClient client)
    {
        var gate = await client.GetAsync(AdminPath);
        var gateContent = await gate.Content.ReadAsStringAsync();

        gate.EnsureSuccessStatusCode();

        return gateContent;
    }

    private static void AssertMainNavigationContainsExpectedLinks(string content)
    {
        Assert.Contains("main-navigation", content);
        Assert.Contains("href=\"/vendeurs\"", content);
        Assert.Contains("href=\"/caisse\"", content);
        Assert.Contains("href=\"/donnees\"", content);
        Assert.Contains("href=\"/rapprochement\"", content);
        Assert.Contains($"href=\"{AdminPath}\"", content);
        Assert.Contains("VENDEURS", content);
        Assert.Contains("CAISSE", content);
        Assert.Contains("DONN\u00c9ES", content);
        Assert.Contains("ADMIN", content);
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

    private static int CountSortableDataListHeaders(string content)
    {
        return Regex.Matches(content, "<button[^>]*data-data-list-sort").Count;
    }

    private static void AssertPhaseButtonDisabled(string content, string label)
    {
        var button = MatchPhaseButton(content, label);

        Assert.Contains("disabled", button.Groups["attributes"].Value);
    }

    private static void AssertPhaseButtonEnabled(string content, string label)
    {
        var button = MatchPhaseButton(content, label);

        Assert.DoesNotContain("disabled", button.Groups["attributes"].Value);
    }

    private static Match MatchPhaseButton(string content, string label)
    {
        var button = Regex.Match(
            content,
            $"""<button\b(?<attributes>[^>]*)>\s*{Regex.Escape(label)}\s*</button>""",
            RegexOptions.Singleline);

        if (!button.Success)
        {
            throw new InvalidOperationException($"Could not find the phase button '{label}'.");
        }

        return button;
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
            Phone = phone ?? "0123",
            PostalAddress = "1 rue des Tests"
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
                Title = item.Title,
                SellerPriceCents = item.PriceCents,
                Status = item.Status
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<(long SaleId, string FirstReference, string SecondReference)> SeedSalesViewSaleAsync(
        string? buyerAddress,
        DateTime? firstLineCorrectedAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var sellerCode = $"RV-{suffix}";
        var seller = new SellerEntity
        {
            SellerCode = sellerCode,
            FirstName = "Renee",
            LastName = "Ventes"
        };

        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync();

        var firstItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}-001",
            Title = "Jeu corrigé",
            SellerPriceCents = 900,
            Status = firstLineCorrectedAt is null ? "sold" : "in_stock"
        };
        var secondItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}-002",
            Title = "Jeu conserve",
            SellerPriceCents = 600,
            Status = "sold"
        };

        dbContext.Items.AddRange(firstItem, secondItem);
        await dbContext.SaveChangesAsync();
        var cashRegisterId = await SeedCashRegisterAsync(dbContext, $"CAISSE-{sellerCode}");

        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
            CashRegisterId = cashRegisterId,
            SubtotalSellerPriceCents = 1500,
            CheckoutFeeCents = 150,
            TotalPaidCents = 1650,
            BuyerAddress = buyerAddress,
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = firstItem.Id,
                    SellerPriceAtSaleCents = 900,
                    CorrectedAt = firstLineCorrectedAt
                },
                new SaleLineEntity
                {
                    ItemId = secondItem.Id,
                    SellerPriceAtSaleCents = 600
                }
            ]
        };

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return (sale.Id, firstItem.Reference, secondItem.Reference);
    }

    private async Task<(long SaleId, string FirstReference)> SeedSalesViewInvoiceCandidateAsync(
        string? buyerAddress = "12 rue des Lilas")
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var seller = new SellerEntity
        {
            SellerCode = $"IV-{suffix}",
            FirstName = "Irene",
            LastName = "Facture"
        };

        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync();

        var item = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{seller.SellerCode}-001",
            Title = "Jeu facture apres vente",
            SellerPriceCents = 1200,
            Status = "sold"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();
        var cashRegisterId = await SeedCashRegisterAsync(dbContext, $"CAISSE-{seller.SellerCode}");

        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
            CashRegisterId = cashRegisterId,
            SubtotalSellerPriceCents = 1200,
            CheckoutFeeCents = 120,
            TotalPaidCents = 1320,
            BuyerName = "MJC Centre",
            BuyerPostalCode = "38100",
            BuyerCity = "Grenoble",
            BuyerAddress = buyerAddress,
            PaymentMethod = "card",
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = item.Id,
                    SellerPriceAtSaleCents = item.SellerPriceCents.GetValueOrDefault()
                }
            ]
        };

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return (sale.Id, item.Reference);
    }

    private async Task<(
        long SellerId,
        string SellerCode,
        long SaleId,
        string RegisteredReference,
        string InStockReference,
        string SoldReference,
        string MissingReference,
        string CashRegisterName)> SeedDataListScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var sellerCode = $"DL{suffix}";
        var cashRegisterName = $"CAISSE-DL-{suffix}";
        var seller = new SellerEntity
        {
            SellerCode = sellerCode,
            FirstName = "Diane",
            LastName = "Liste",
            Email = "diane.liste@example.test",
            Phone = "0606060606",
            PostalAddress = "7 rue Lecture",
            FeePaidCents = 0
        };

        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync();

        var registeredItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}001",
            Title = "Jeu enregistr\u00e9",
            SellerPriceCents = 500,
            Status = "registered"
        };
        var inStockItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}002",
            Title = "Jeu disponible",
            SellerPriceCents = 700,
            Status = "in_stock"
        };
        var soldItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}003",
            Title = "Jeu vendu",
            SellerPriceCents = 900,
            Status = "sold"
        };
        var missingItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}004",
            Title = "Jeu manquant",
            SellerPriceCents = 600,
            Status = "missing"
        };

        dbContext.Items.AddRange(registeredItem, inStockItem, soldItem, missingItem);
        await dbContext.SaveChangesAsync();
        var cashRegisterId = await SeedCashRegisterAsync(dbContext, cashRegisterName);

        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 5, 9, 30, 0, DateTimeKind.Utc),
            SubtotalSellerPriceCents = 900,
            CheckoutFeeCents = 90,
            TotalPaidCents = 990,
            CashRegisterId = cashRegisterId,
            BuyerName = "MJC Centre",
            BuyerPostalCode = "38100",
            BuyerCity = "Grenoble",
            BuyerAddress = "texte re\u00e7u confidentiel",
            PaymentMethod = "cheque",
            ChequeNumber = "CH-123",
            ChequeBankName = "banque cach\u00e9e",
            SaleLines =
            [
                new SaleLineEntity
                {
                    ItemId = soldItem.Id,
                    SellerPriceAtSaleCents = soldItem.SellerPriceCents.GetValueOrDefault()
                }
            ]
        };

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return (seller.Id, sellerCode, sale.Id, registeredItem.Reference, inStockItem.Reference, soldItem.Reference, missingItem.Reference, cashRegisterName);
    }

    private async Task SetSalePhaseAsync(SalePhase phase)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await new SalePhaseService(dbContext).SetPhaseAsync(phase);
    }

    private async Task<long> GetSellerIdByCodeAsync(string sellerCode)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Sellers
            .Where(x => x.SellerCode == sellerCode)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task<long> SeedCashRegisterAsync(
        AppDbContext dbContext,
        string name,
        int initialCashCents = 5000)
    {
        var existingCashRegister = await dbContext.CashRegisters.SingleOrDefaultAsync(x => x.Name == name);
        if (existingCashRegister is not null)
        {
            return existingCashRegister.Id;
        }

        var cashRegister = new CashRegisterEntity
        {
            Name = name,
            InitialCashCents = initialCashCents,
            IsActive = true
        };
        dbContext.CashRegisters.Add(cashRegister);
        await dbContext.SaveChangesAsync();
        return cashRegister.Id;
    }

    private async Task EnsureSoldItemsHaveSaleLinesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var soldItemsWithoutSaleLines = await dbContext.Items
            .Include(x => x.SaleLines)
            .Where(x => x.Status == "sold" && !x.SaleLines.Any(line => line.CorrectedAt == null))
            .ToListAsync();
        var cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SEED");

        foreach (var item in soldItemsWithoutSaleLines)
        {
            dbContext.Sales.Add(new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                CashRegisterId = cashRegisterId,
                SubtotalSellerPriceCents = item.SellerPriceCents.GetValueOrDefault(),
                CheckoutFeeCents = 0,
                TotalPaidCents = item.SellerPriceCents.GetValueOrDefault(),
                SaleLines =
                [
                    new SaleLineEntity
                    {
                        ItemId = item.Id,
                        SellerPriceAtSaleCents = item.SellerPriceCents.GetValueOrDefault()
                    }
                ]
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<long> SellerIdByCodeAsync(string sellerCode)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Sellers.Where(x => x.SellerCode == sellerCode).Select(x => x.Id).SingleAsync();
    }

    // Deposit actions need a desk open on the computer; releasing it at the end
    // keeps letters free for the other tests sharing the database.
    private static async Task<ClientDesk> OpenClientDeskAsync(HttpClient client, long sellerId)
    {
        var page = await client.GetStringAsync($"/vendeurs/{sellerId}");
        (await client.PostAsync($"/vendeurs/{sellerId}?handler=OpenDesk", BuildFormContent(page))).EnsureSuccessStatusCode();
        return new ClientDesk(client, sellerId);
    }

    private sealed class ClientDesk(HttpClient client, long sellerId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            var page = await client.GetStringAsync($"/vendeurs/{sellerId}");
            await client.PostAsync($"/vendeurs/{sellerId}?handler=ReleaseDesk", BuildFormContent(page));
        }
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

    private static FormUrlEncodedContent BuildCashRegisterCreateFormContent(string html, string cashRegisterName)
    {
        var formMatch = Regex.Matches(
                html,
                "<form(?<attributes>[^>]*)>(?<body>.*?)</form>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .FirstOrDefault(match => match.Groups["body"].Value.Contains("Ajouter la caisse", StringComparison.Ordinal));
        Assert.NotNull(formMatch);

        var values = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", ExtractRequestVerificationToken(html))
        };

        foreach (var inputMatch in Regex.Matches(
                     formMatch.Groups["body"].Value,
                     "<input(?<attributes>[^>]*)>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline).Cast<Match>())
        {
            var attributes = inputMatch.Groups["attributes"].Value;
            var name = ExtractAttributeValue(attributes, "name");
            if (name is null || name == "__RequestVerificationToken")
            {
                continue;
            }

            var type = ExtractAttributeValue(attributes, "type") ?? "text";
            if (string.Equals(type, "checkbox", StringComparison.OrdinalIgnoreCase)
                && !attributes.Contains("checked", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = name switch
            {
                "CashRegisterInput.Name" => cashRegisterName,
                "CashRegisterInput.InitialCash" => "50",
                _ => ExtractAttributeValue(attributes, "value") ?? string.Empty
            };

            values.Add(new KeyValuePair<string, string>(name, value));
        }

        return new FormUrlEncodedContent(values);
    }

    private static string? ExtractAttributeValue(string attributes, string attributeName)
    {
        var match = Regex.Match(
            attributes,
            $@"\b{Regex.Escape(attributeName)}=""(?<value>[^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : null;
    }

    private static void AssertCreateSellerFormPostsToCreatePage(string html)
    {
        var formMatch = Regex.Matches(
            html,
            "<form(?<attributes>[^>]*)>(?<body>.*?)</form>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .FirstOrDefault(match => match.Groups["body"].Value.Contains("Enregistrer le vendeur et les articles", StringComparison.Ordinal));
        Assert.NotNull(formMatch);

        var attributes = formMatch.Groups["attributes"].Value;
        Assert.Contains("action=\"/vendeurs/creer\"", attributes);
        Assert.DoesNotContain("handler=ImportExcel", attributes);
    }

    private static byte[] CreateSellerWorkbookBytes()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddWorkbookEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            AddWorkbookEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddWorkbookEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Feuil1" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            AddWorkbookEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            AddWorkbookEntry(archive, "xl/worksheets/sheet1.xml", $$"""
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="2">{{InlineCell("C2", "NOM PRENOM")}}{{InlineCell("F2", "CP VILLE")}}{{InlineCell("H2", "TELEPHONE")}}</row>
                    <row r="3">{{InlineCell("C3", "DURAND Marie")}}{{InlineCell("F3", "38120 Saint-Égrève")}}{{InlineCell("H3", "0600000000")}}</row>
                    <row r="5">{{InlineCell("B5", "libellé")}}{{InlineCell("C5", "prix")}}</row>
                    <row r="7">{{InlineCell("B7", "Puzzle Excel")}}<c r="C7"><v>5</v></c></row>
                  </sheetData>
                </worksheet>
                """);
        }

        return stream.ToArray();
    }

    private static string InlineCell(string cellReference, string value)
    {
        return $"<c r=\"{cellReference}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value)}</t></is></c>";
    }

    private static void AddWorkbookEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
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

    private static void AssertInputIncludesAttributes(
        string html,
        string inputName,
        params (string Name, string Value)[] attributes)
    {
        var inputMatch = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(inputName)}\"[^>]*>",
            RegexOptions.IgnoreCase);

        Assert.True(inputMatch.Success, $"Input {inputName} was not rendered.");

        foreach (var (name, value) in attributes)
        {
            Assert.Contains($"{name}=\"{value}\"", inputMatch.Value);
        }
    }

    private static void AssertRequiredFieldMarker(string html, string label)
    {
        Assert.Contains(
            $"<span class=\"field-label-text\">{label} <span class=\"required-field-badge\">Obligatoire</span></span>",
            html);
    }

    [Fact]
    public async Task SalesApiPersistsBuyerAddress()
    {
        var client = _factory.CreateClient();
        long itemId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-003",
                FirstName = "Sonia",
                LastName = "Facture",
                Email = "sonia.facture@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-003-001",
                Title = "Jeu avec facture",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-003");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "  Association sportive  ",
                buyerPostalCode = "  38100  ",
                buyerCity = " Grenoble ",
                buyerAddress = "  Association sportive  "
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sale = await dbContext.Sales.Include(x => x.SaleLines).SingleAsync(x => x.SaleLines.Any(line => line.ItemId == itemId));
            Assert.Equal("Association sportive", sale.BuyerAddress);
        }
    }

    [Fact]
    public async Task SalesApiPersistsChequeTraceability()
    {
        var client = _factory.CreateClient();
        long itemId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-004",
                FirstName = "Sonia",
                LastName = "Cheque",
                Email = "sonia.cheque@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-004-001",
                Title = "Jeu cheque",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-004");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "Sonia Cheque",
                buyerPostalCode = "38100",
                buyerCity = "Grenoble",
                paymentMethod = "cheque",
                chequeNumber = "  CH-2026  ",
                chequeBankName = "  Crédit Agricole  "
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sale = await dbContext.Sales.Include(x => x.SaleLines).SingleAsync(x => x.SaleLines.Any(line => line.ItemId == itemId));
            Assert.Equal("cheque", sale.PaymentMethod);
            Assert.Equal("CH-2026", sale.ChequeNumber);
            Assert.Equal("Crédit Agricole", sale.ChequeBankName);
        }
    }

    [Fact]
    public async Task SalesApiRejectsChequeWithoutTraceability()
    {
        var client = _factory.CreateClient();
        long itemId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-005",
                FirstName = "Sonia",
                LastName = "ChequeIncomplet",
                Email = "sonia.cheque.incomplet@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-005-001",
                Title = "Jeu cheque incomplet",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-005");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "Sonia Cheque",
                buyerPostalCode = "38100",
                buyerCity = "Grenoble",
                paymentMethod = "cheque",
                chequeNumber = "CH-2026",
                chequeBankName = " "
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("saved").GetBoolean());
        Assert.True(document.RootElement.GetProperty("isValidationError").GetBoolean());
        Assert.Contains("numéro de chèque", document.RootElement.GetProperty("message").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await dbContext.Sales.Where(x => x.SaleLines.Any(line => line.ItemId == itemId)).ToListAsync());
            Assert.Equal("in_stock", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
        }
    }

    [Fact]
    public async Task SalesApiRejectsMissingBuyerDetails()
    {
        var client = _factory.CreateClient();
        long itemId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-007",
                FirstName = "Sonia",
                LastName = "AcheteurObligatoire",
                Email = "sonia.acheteur.obligatoire@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-007-001",
                Title = "Jeu sans acheteur",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-007");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "Sonia",
                buyerPostalCode = "38100",
                buyerCity = " "
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("saved").GetBoolean());
        Assert.True(document.RootElement.GetProperty("isValidationError").GetBoolean());
        Assert.Contains("nom, le code postal et la ville", document.RootElement.GetProperty("message").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Empty(await dbContext.Sales.Where(x => x.SaleLines.Any(line => line.ItemId == itemId)).ToListAsync());
            Assert.Equal("in_stock", (await dbContext.Items.SingleAsync(x => x.Id == itemId)).Status);
        }
    }

    [Fact]
    public async Task SalesApiPersistsBuyerDetails()
    {
        var client = _factory.CreateClient();
        long itemId;
        long cashRegisterId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-006",
                FirstName = "Sonia",
                LastName = "Acheteur",
                Email = "sonia.acheteur@example.test",
                DepositStatus = "deposit_validated"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-006-001",
                Title = "Jeu avec acheteur",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-006");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "  Ecole du centre  ",
                buyerPostalCode = "  38100  ",
                buyerCity = " Grenoble "
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sale = await dbContext.Sales.Include(x => x.SaleLines).SingleAsync(x => x.SaleLines.Any(line => line.ItemId == itemId));
            Assert.Equal("Ecole du centre", sale.BuyerName);
            Assert.Equal("38100", sale.BuyerPostalCode);
            Assert.Equal("Grenoble", sale.BuyerCity);
        }
    }

    [Fact]
    public async Task SalesApiReturnsConflictWithoutPersistingUnavailableItems()
    {
        var client = _factory.CreateClient();
        long itemId;
        long cashRegisterId;

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
                Title = "Jeu deja vendu",
                SellerPriceCents = 1500,
                Status = "sold"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            cashRegisterId = await SeedCashRegisterAsync(dbContext, "CAISSE-SV-002");
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
                cashRegisterId,
                buyerName = "Client conflit",
                buyerPostalCode = "38100",
                buyerCity = "Grenoble"
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("saved").GetBoolean());
        Assert.Equal(itemId, document.RootElement.GetProperty("conflictItemIds")[0].GetInt64());
    }

    private static string FindRepositoryDirectory(params string[] relativePathParts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativePathParts).ToArray());
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"Could not find repository directory {Path.Combine(relativePathParts)}.");
    }

    [Fact]
    public async Task SellerDetails_AddItemsAfterOpeningTheDepositDialog_CreatesTheItems()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });
        await SetSalePhaseAsync(SalePhase.Presale);

        long sellerId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "UB",
                FirstName = "Ursule",
                LastName = "Bonnet",
                Phone = "0123",
                PostalAddress = "1 rue des Tests",
                PostalCode = "38120",
                City = "SAINT-EGREVE",
                IdentityDocumentType = "CNI",
                IdentityDocumentNumber = "123456",
                IdentityDocumentIssuedOn = new DateOnly(2020, 1, 1),
                DepositStatus = "deposit_pending"
            };
            seller.Items.Add(new ItemEntity { Title = "Premier jouet", SellerPriceCents = 500, Status = "registered" });
            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();
            sellerId = seller.Id;
        }

        // The operator opens the deposit dialog. This handler returns Page(), so the
        // browser's address bar keeps "?handler=BeginDepositFinalization".
        var dialogUrl = $"/vendeurs/{sellerId}?handler=BeginDepositFinalization";
        var pageResponse = await client.GetAsync($"/vendeurs/{sellerId}");
        pageResponse.EnsureSuccessStatusCode();
        var dialogResponse = await client.PostAsync(
            dialogUrl,
            BuildFormContent(await pageResponse.Content.ReadAsStringAsync()));
        dialogResponse.EnsureSuccessStatusCode();
        var dialogHtml = await dialogResponse.Content.ReadAsStringAsync();

        // The add-items form must carry an explicit action. Without one a browser posts
        // it to the current URL, query string included, so "Enregistrer le lot" would
        // re-enter the deposit handler instead of adding items.
        // Locate the add-items form specifically: several forms on this page share the
        // stack-form class, so find the one that actually contains the item rows.
        var rowFieldIndex = dialogHtml.IndexOf("Input.Rows[0].Title", StringComparison.Ordinal);
        Assert.True(rowFieldIndex > 0, "The add-items form was not rendered.");
        var formStart = dialogHtml.LastIndexOf("<form", rowFieldIndex, StringComparison.OrdinalIgnoreCase);
        Assert.True(formStart >= 0, "No form encloses the item rows.");
        var formTag = dialogHtml[formStart..(dialogHtml.IndexOf('>', formStart) + 1)];
        var actionMatch = Regex.Match(formTag, "action=\"(?<action>[^\"]*)\"", RegexOptions.IgnoreCase);
        var action = actionMatch.Success ? WebUtility.HtmlDecode(actionMatch.Groups["action"].Value) : string.Empty;
        Assert.False(string.IsNullOrEmpty(action), "The add-items form has no action, so it inherits the deposit handler query string.");
        Assert.DoesNotContain("handler=", action, StringComparison.OrdinalIgnoreCase);

        // Post exactly where a browser would.
        var addResponse = await client.PostAsync(
            action,
            BuildFormContent(
                dialogHtml,
                ("Input.Rows[0].Title", "Deuxieme jouet"),
                ("Input.Rows[0].SellerPrice", "7")));
        addResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var titles = await dbContext.Items
                .Where(x => x.SellerId == sellerId)
                .Select(x => x.Title)
                .ToListAsync();
            Assert.Contains("Deuxieme jouet", titles);
            Assert.Equal(2, titles.Count);
        }
    }

}
