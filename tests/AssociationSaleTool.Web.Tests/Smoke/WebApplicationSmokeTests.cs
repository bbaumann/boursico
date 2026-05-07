using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security;
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
    public async Task SellersIndexRendersLiveSearchHooksForCodeAndName()
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
        Assert.Contains("placeholder=\"Code ou nom\"", content);
        Assert.Contains("Aucun vendeur ne correspond à cette recherche.", content);
        Assert.Contains("data-seller-row", content);
        Assert.Contains("data-seller-filter-text=\"sl-101 ariane martin\"", content);
        Assert.Contains("data-seller-filter-text=\"sl-202 bruno durand\"", content);
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
                ItemNumber = 1,
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
        Assert.Contains("data-city-options", createContent);
        Assert.Contains("Code postal", createContent);
        Assert.Contains("Ville", createContent);

        var sellerId = await GetSellerIdByCodeAsync("SF-001");
        var editResponse = await client.GetAsync($"/vendeurs/{sellerId}/modifier");
        editResponse.EnsureSuccessStatusCode();
        var editContent = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Input.PostalCode\"", editContent);
        Assert.Contains("name=\"Input.City\"", editContent);
        Assert.Contains("data-postal-city-root", editContent);
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
                ItemNumber = 1,
                Title = "Article mal vendu",
                SellerPriceCents = 900,
                Status = "sold"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
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
                ItemNumber = 1,
                Title = "Article remboursable",
                SellerPriceCents = 1000,
                Status = "sold"
            };
            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-002",
                ItemNumber = 2,
                Title = "Article \u00e0 contr\u00f4ler",
                SellerPriceCents = 700,
                Status = "in_stock"
            };
            var defectiveItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-003",
                ItemNumber = 3,
                Title = "Article déjà défectueux",
                SellerPriceCents = 500,
                Status = "defective"
            };
            var missingItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-004",
                ItemNumber = 4,
                Title = "Article manquant",
                SellerPriceCents = 300,
                Status = "missing"
            };
            var registeredItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "00-SD-005",
                ItemNumber = 5,
                Title = "Article enregistré",
                SellerPriceCents = 200,
                Status = "registered"
            };

            dbContext.Items.AddRange(soldItem, inStockItem, defectiveItem, missingItem, registeredItem);
            await dbContext.SaveChangesAsync();
            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
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

        var content = await UnlockSaleStatusPageAsync(client);

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
    public async Task SaleStatusPageRefundsDefectiveSaleLineWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        long saleLineId;

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
                ItemNumber = 1,
                Title = "Article retourn\u00e9",
                SellerPriceCents = 1500,
                Status = "sold"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
                SubtotalSellerPriceCents = 1500,
                CheckoutFeeCents = 150,
                TotalPaidCents = 1650,
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
            BuildFormContent(dashboardContent));

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
        }
    }

    [Fact]
    public async Task AdminPageCancelsDefectiveRefundWhenUnlocked()
    {
        var client = _factory.CreateClient();
        long itemId;
        long saleLineId;

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
                ItemNumber = 1,
                Title = "Article remboursement erreur",
                SellerPriceCents = 1500,
                Status = "defective"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
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
                        RefundReason = "defective"
                    }
                ]
            };
            dbContext.Sales.Add(sale);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
            saleLineId = sale.SaleLines.Single().Id;
        }

        var dashboardContent = await UnlockSaleStatusPageAsync(client);
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
                ItemNumber = 1,
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
                ItemNumber = 1,
                Title = "Article garde",
                SellerPriceCents = 700,
                Status = "sold"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
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
        Assert.Contains("Client facture", content);
        Assert.Contains("Retour à la liste", content);
        Assert.DoesNotContain("Remettre en stock", content);
        Assert.DoesNotContain("Enregistrer la vente", content);
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
        Assert.Contains("data-list-filter-bar", content);
        Assert.Contains("data-list-filter-field-search", content);
        Assert.Contains("data-list-filter-field-status", content);
        Assert.Contains("data-data-list-search", content);
        Assert.Contains("data-data-list-status-filter", content);
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
    public async Task DataListsSellersTabSearchesByCodeAndNameOnly()
    {
        var client = _factory.CreateClient();
        var scenario = await SeedDataListScenarioAsync();

        var response = await client.GetAsync("/donnees?onglet=vendeurs");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Vendeurs", content);
        Assert.Contains(scenario.SellerCode, content);
        Assert.Contains("Diane Liste", content);
        Assert.Contains("diane.liste@example.test", content);
        Assert.Contains("0606060606", content);
        Assert.Contains("data-list-seller-table", content);
        Assert.Contains("class=\"data-list-size-xxs data-list-code-column\">Code", content);
        Assert.Contains("class=\"data-list-size-l data-list-name-column\">Nom", content);
        Assert.Contains("class=\"data-list-size-l data-list-email-column\">E-mail", content);
        Assert.Contains("class=\"data-list-size-m data-list-phone-column\">T\u00e9l\u00e9phone", content);
        Assert.Contains("title=\"Total des articles\">Tot.</abbr>", content);
        Assert.Contains("title=\"Enregistr\u00e9s\">Enr.</abbr>", content);
        Assert.Contains("title=\"En stock\">Stock</abbr>", content);
        Assert.Contains("title=\"Vendus\">Vend.</abbr>", content);
        Assert.Contains("title=\"Manquants\">Manq.</abbr>", content);
        Assert.Contains("title=\"D\u00e9fectueux\">D\u00e9f.</abbr>", content);
        Assert.Contains("class=\"data-list-size-xs data-list-fee-column\">Frais", content);
        Assert.Contains($"href=\"/vendeurs/{scenario.SellerId}\">{scenario.SellerCode}</a>", content);
        Assert.Contains("0/2\u20ac", content);
        Assert.DoesNotContain("0.00 € / 2.00 €", content);
        Assert.Contains($"data-data-list-filter-text=\"{scenario.SellerCode.ToLowerInvariant()} diane liste\"", content);
        Assert.DoesNotContain("diane.liste@example.test\"", content);
        Assert.DoesNotContain("0606060606\"", content);
        Assert.DoesNotContain("rue lecture\"", content);
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
        Assert.DoesNotContain("sales-view-table", content);
        Assert.Contains("class=\"data-list-size-xxs data-list-sale-id-column\">Vente", content);
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
        Assert.Contains("class=\"data-list-size-xxs data-list-cheque-sale-column\">Vente", content);
        Assert.Contains("<th>N\u00b0 ch\u00e8que</th>", content);
        Assert.Contains("<th>Banque</th>", content);
        Assert.Contains("<th>Montant</th>", content);
        Assert.Contains($"href=\"/caisse/ventes/{scenario.SaleId}\">#{scenario.SaleId}</a>", content);
        Assert.Contains("CH-123", content);
        Assert.Contains("banque cach\u00e9e", content);
        Assert.Contains("9.90 €", content);
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
                ItemNumber = 1,
                Title = "Jeu defectueux",
                SellerPriceCents = 1000,
                Status = "defective"
            };
            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();

            var sale = new SaleEntity
            {
                SoldAt = DateTime.UtcNow,
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
                        RefundReason = "defective"
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
            Assert.Contains("Code ou nom du vendeur", content);
            Assert.Contains("list=\"seller-sheet-options\"", content);
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
                    ItemNumber = 1,
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
                    ItemNumber = 1,
                    Title = "Article en stock",
                    SellerPriceCents = 500,
                    Status = "in_stock"
                };
                var missingItem = new ItemEntity
                {
                    SellerId = seller.Id,
                    Reference = "RO-002-002",
                    ItemNumber = 2,
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
                $"/rapprochement?handler=MarkMissing&itemId={inStockItemId}&sellerCode=RO-002",
                BuildFormContent(gateContent));
            var markInStockResponse = await client.PostAsync(
                $"/rapprochement?handler=MarkInStock&itemId={missingItemId}&sellerCode=RO-002",
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
                        ItemNumber = 1,
                        Title = "Camion filtrable",
                        SellerPriceCents = 700,
                        Status = "in_stock"
                    },
                    new ItemEntity
                    {
                        SellerId = seller.Id,
                        Reference = "RA-010-002",
                        ItemNumber = 2,
                        Title = "Puzzle deja manquant",
                        SellerPriceCents = 800,
                        Status = "missing"
                    });

                await dbContext.SaveChangesAsync();
            }

            var content = await UnlockReconciliationDashboardAsync(client);

            Assert.Contains("<datalist id=\"seller-sheet-options\">", content);
            Assert.Contains("value=\"RA-010 - Alice Autocomplete\"", content);
            Assert.Contains("data-reconciliation-filter-text=\"ra-010-001 camion filtrable ra-010 alice autocomplete\"", content);
            Assert.Contains("data-reconciliation-filter-text=\"ra-010-002 puzzle deja manquant ra-010 alice autocomplete\"", content);
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
                ItemNumber = 1,
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
                    ItemNumber = itemNumber,
                    Title = $"Article enregistre {itemNumber}",
                    SellerPriceCents = 100,
                    Status = "registered"
                });
            }

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/rapprochement/feuilles?codeVendeur=RS-FEE");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Sous-total articles", content);
        Assert.Contains("Frais attendus", content);
        Assert.Contains("Frais payés", content);
        Assert.Contains("Frais restant dus", content);
        Assert.Contains("Total à payer", content);
        Assert.Contains("8.00 €", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageFiltersBySellerCodeWhenUnlocked()
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

        var response = await client.GetAsync("/rapprochement/feuilles?codeVendeur=RS-201%20-%20Claire%20Filtre");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("RS-201", content);
        Assert.Contains("Article cible", content);
        Assert.DoesNotContain("RS-202", content);
        Assert.DoesNotContain("Article autre vendeur", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
    }

    [Fact]
    public async Task ReconciliationSheetsPageShowsFrenchMessageForUnknownSellerCode()
    {
        await SetSalePhaseAsync(SalePhase.PostSale);
        try
        {
        var (client, _) = await UnlockReconciliationDashboardAsync();

        var response = await client.GetAsync("/rapprochement/feuilles?codeVendeur=RS-404");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Aucune feuille vendeur pour le code RS-404.", content);
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
    public async Task SellerCreatePageRendersCombinedSellerItemFormAndExcelUpload()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs/creer");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("excel-import-panel", content);
        Assert.Contains("excel-import-form", content);
        Assert.Contains("aria-label=\"Préremplissage Excel\"", content);
        Assert.Contains("Fichier Excel vendeur", content);
        Assert.DoesNotContain("Pr\u00c3", content);
        Assert.DoesNotContain("<h2>Importer depuis Excel</h2>", content);
        Assert.Contains("Enregistrer le vendeur et les articles", content);
        Assert.Contains("Mode de saisie", content);
        Assert.Contains("name=\"Input.Rows[9].Title\"", content);
        Assert.DoesNotContain("name=\"Input.Rows[10].Title\"", content);
        Assert.Contains("data-item-registration-table", content);
        Assert.Contains("data-add-item-row", content);
    }

    [Fact]
    public async Task SellerCreateExcelUploadPrefillsFormWithoutPersistingData()
    {
        var client = _factory.CreateClient();
        await SetSalePhaseAsync(SalePhase.Presale);
        int sellerCountBefore;
        int itemCountBefore;

        using (var beforeScope = _factory.Services.CreateScope())
        {
            var beforeContext = beforeScope.ServiceProvider.GetRequiredService<AppDbContext>();
            sellerCountBefore = await beforeContext.Sellers.CountAsync();
            itemCountBefore = await beforeContext.Items.CountAsync();
        }

        var form = await client.GetAsync("/vendeurs/creer");
        form.EnsureSuccessStatusCode();
        var formContent = await form.Content.ReadAsStringAsync();

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(ExtractRequestVerificationToken(formContent)), "__RequestVerificationToken");
        multipart.Add(
            new ByteArrayContent(CreateSellerWorkbookBytes()),
            "ExcelFile",
            "liste.xlsx");

        var response = await client.PostAsync("/vendeurs/creer?handler=ImportExcel", multipart);

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("value=\"Marie\"", content);
        Assert.Contains("value=\"Durand\"", content);
        Assert.Contains("value=\"0600000000\"", content);
        Assert.Contains("UNKNOWN", content);
        Assert.Contains("value=\"38120\"", content);
        Assert.Contains("value=\"Saint-Égrève\"", content);
        Assert.Contains("Puzzle Excel", content);
        Assert.Contains("value=\"5\"", content);
        AssertCreateSellerFormPostsToCreatePage(content);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(sellerCountBefore, await dbContext.Sellers.CountAsync());
        Assert.Equal(itemCountBefore, await dbContext.Items.CountAsync());
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
                    ItemNumber = itemNumber,
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
            Assert.DoesNotContain("Modifier le vendeur", content);
            Assert.DoesNotContain("Ajouter des articles", content);
        }
        finally
        {
            await SetSalePhaseAsync(SalePhase.Presale);
        }
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
        Assert.Contains("Cet article est déjà dans le panier.", content);
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
                ItemNumber = 1,
                Title = "Puzzle",
                SellerPriceCents = 450,
                Status = "registered"
            };
            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "BK-009-002",
                ItemNumber = 2,
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
        Assert.Contains("data-checkout-buyer-name", content);
        Assert.Contains("data-checkout-buyer-postal-code", content);
        Assert.Contains("data-checkout-buyer-city", content);
        Assert.Contains("data-checkout-buyer-city-options", content);
        Assert.Contains("data-checkout-buyer-message", content);
        Assert.Contains("data-postal-city-root", content);
        Assert.Contains("data-postal-code-input", content);
        Assert.Contains("data-city-input", content);
        Assert.Contains("Nom ou structure", content);
        Assert.Contains("Code postal", content);
        Assert.Contains("Ville", content);
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
        Assert.Contains("data-checkout-buyer-address", content);
        Assert.Contains("Adresse", content);
        Assert.DoesNotContain("Détails du reçu", content);
        Assert.Contains("data-checkout-save-print", content);
        Assert.Contains("Enregistrer et imprimer", content);
        Assert.Contains("Mode de paiement", content);
        Assert.Contains("CB", content);
        Assert.Contains("Espèces", content);
        Assert.Contains("Chèque", content);
        Assert.Contains("data-checkout-payment-method", content);
        Assert.Contains("data-checkout-cheque-number", content);
        Assert.Contains("data-checkout-cheque-bank-name", content);
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
        var cartPanelIndex = content.IndexOf("class=\"subsection checkout-cart-panel\"", StringComparison.Ordinal);
        var cartHeadingIndex = content.IndexOf("<h2>Panier</h2>", StringComparison.Ordinal);
        var searchIndex = content.IndexOf("data-checkout-search", StringComparison.Ordinal);
        var cartBodyIndex = content.IndexOf("data-checkout-cart", StringComparison.Ordinal);

        Assert.True(cartPanelIndex >= 0, "The checkout page should render the cart panel.");
        Assert.True(cartHeadingIndex > cartPanelIndex, "The Panier heading should be inside the cart panel.");
        Assert.True(searchIndex > cartHeadingIndex, "The item search should be rendered inside the Panier section.");
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

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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

    private static async Task<string> UnlockSaleStatusPageAsync(HttpClient client)
    {
        var gateContent = await GetSaleStatusGateAsync(client);

        var response = await client.PostAsync(
            $"{AdminPath}?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "test-password")));

        response.EnsureSuccessStatusCode();

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
                ItemNumber = index + 1,
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
            ItemNumber = 1,
            Title = "Jeu corrigé",
            SellerPriceCents = 900,
            Status = firstLineCorrectedAt is null ? "sold" : "in_stock"
        };
        var secondItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}-002",
            ItemNumber = 2,
            Title = "Jeu conserve",
            SellerPriceCents = 600,
            Status = "sold"
        };

        dbContext.Items.AddRange(firstItem, secondItem);
        await dbContext.SaveChangesAsync();

        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc),
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

    private async Task<(
        long SellerId,
        string SellerCode,
        long SaleId,
        string RegisteredReference,
        string InStockReference,
        string SoldReference,
        string MissingReference)> SeedDataListScenarioAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var sellerCode = $"DL{suffix}";
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
            ItemNumber = 1,
            Title = "Jeu enregistr\u00e9",
            SellerPriceCents = 500,
            Status = "registered"
        };
        var inStockItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}002",
            ItemNumber = 2,
            Title = "Jeu disponible",
            SellerPriceCents = 700,
            Status = "in_stock"
        };
        var soldItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}003",
            ItemNumber = 3,
            Title = "Jeu vendu",
            SellerPriceCents = 900,
            Status = "sold"
        };
        var missingItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}004",
            ItemNumber = 4,
            Title = "Jeu manquant",
            SellerPriceCents = 600,
            Status = "missing"
        };

        dbContext.Items.AddRange(registeredItem, inStockItem, soldItem, missingItem);
        await dbContext.SaveChangesAsync();

        var sale = new SaleEntity
        {
            SoldAt = new DateTime(2026, 5, 5, 9, 30, 0, DateTimeKind.Utc),
            SubtotalSellerPriceCents = 900,
            CheckoutFeeCents = 90,
            TotalPaidCents = 990,
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
                    SellerPriceAtSaleCents = soldItem.SellerPriceCents
                }
            ]
        };

        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return (seller.Id, sellerCode, sale.Id, registeredItem.Reference, inStockItem.Reference, soldItem.Reference, missingItem.Reference);
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

    private async Task EnsureSoldItemsHaveSaleLinesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var soldItemsWithoutSaleLines = await dbContext.Items
            .Include(x => x.SaleLines)
            .Where(x => x.Status == "sold" && !x.SaleLines.Any(line => line.CorrectedAt == null))
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

    [Fact]
    public async Task SalesApiPersistsBuyerAddress()
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
                LastName = "Facture",
                Email = "sonia.facture@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-003-001",
                ItemNumber = 1,
                Title = "Jeu avec facture",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-004",
                FirstName = "Sonia",
                LastName = "Cheque",
                Email = "sonia.cheque@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-004-001",
                ItemNumber = 1,
                Title = "Jeu cheque",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-005",
                FirstName = "Sonia",
                LastName = "ChequeIncomplet",
                Email = "sonia.cheque.incomplet@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-005-001",
                ItemNumber = 1,
                Title = "Jeu cheque incomplet",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-007",
                FirstName = "Sonia",
                LastName = "AcheteurObligatoire",
                Email = "sonia.acheteur.obligatoire@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-007-001",
                ItemNumber = 1,
                Title = "Jeu sans acheteur",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var seller = new SellerEntity
            {
                SellerCode = "SV-006",
                FirstName = "Sonia",
                LastName = "Acheteur",
                Email = "sonia.acheteur@example.test"
            };

            dbContext.Sellers.Add(seller);
            await dbContext.SaveChangesAsync();

            var item = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SV-006-001",
                ItemNumber = 1,
                Title = "Jeu avec acheteur",
                SellerPriceCents = 1500,
                Status = "in_stock"
            };

            dbContext.Items.Add(item);
            await dbContext.SaveChangesAsync();
            itemId = item.Id;
        }

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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

        var response = await client.PostAsJsonAsync(
            "/api/ventes",
            new
            {
                itemIds = new[] { itemId },
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
}
