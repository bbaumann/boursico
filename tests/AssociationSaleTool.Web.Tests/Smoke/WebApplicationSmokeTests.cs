using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
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
    public async Task PagesDeclareFrenchDocumentLanguage()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vendeurs");

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("<html lang=\"fr\">", content);
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
                ("Input.City", "Grenoble")));

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Un vendeur existe déjà avec les mêmes nom, prénom et téléphone", content);
        Assert.Contains("DS-777 - Ariane Doublon", content);
        Assert.Contains("ariane.doublon@example.test", content);
        Assert.Contains("0 article", content);
        Assert.Contains("Ouvrir ce vendeur", content);
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
    public async Task SaleStatusPageShowsMainNavigationWithAdminActive()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vente/statut");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        AssertMainNavigationContainsExpectedLinks(content);
        AssertMainNavigationActive("/vente/statut", content);
    }

    [Fact]
    public async Task SaleStatusPageShowsAdminGateWhenLocked()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/vente/statut");

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Statut de la vente", content);
        Assert.Contains("Mot de passe administrateur", content);
        Assert.Contains("Déverrouiller", content);
        Assert.DoesNotContain("Changer le statut", content);
        Assert.DoesNotContain("Correction des ventes", content);
    }

    [Fact]
    public async Task SaleStatusUnlockRejectsInvalidAdminPasswordInFrench()
    {
        var client = _factory.CreateClient();
        var gateContent = await GetSaleStatusGateAsync(client);

        var response = await client.PostAsync(
            "/vente/statut?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "wrong-password")));

        response.EnsureSuccessStatusCode();

        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Mot de passe administrateur", content);
        Assert.Contains("Mot de passe administrateur incorrect.", content);
        Assert.DoesNotContain("Correction des ventes", content);
    }

    [Fact]
    public async Task SaleStatusUnlockShowsPhaseAndCorrectionControls()
    {
        var client = _factory.CreateClient();

        var content = await UnlockSaleStatusPageAsync(client);

        Assert.Contains("Changer le statut", content);
        Assert.Contains("Correction des ventes", content);
        Assert.Contains("Passer en prévente", content);
        Assert.Contains("Passer en vente", content);
        Assert.Contains("Passer en après-vente", content);
        Assert.DoesNotContain("name=\"Input.Password\"", content);
        Assert.DoesNotContain("name=\"CorrectionPassword\"", content);
        Assert.Contains("href=\"/vente/statut\"", content);
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
            "/vente/statut?handler=ChangePhase",
            BuildFormContent(
                dashboardContent,
                ("Input.Phase", "sale")));

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Statut de la vente", content);
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
            $"/vente/statut?handler=CorrectSaleLine&saleLineId={saleLineId}",
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
    public async Task SaleStatusUnlockShowsDefectiveItemControls()
    {
        var client = _factory.CreateClient();

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
                Reference = "SD-001-001",
                ItemNumber = 1,
                Title = "Article remboursable",
                SellerPriceCents = 1000,
                Status = "sold"
            };
            var inStockItem = new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "SD-001-002",
                ItemNumber = 2,
                Title = "Article \u00e0 contr\u00f4ler",
                SellerPriceCents = 700,
                Status = "in_stock"
            };

            dbContext.Items.AddRange(soldItem, inStockItem);
            await dbContext.SaveChangesAsync();
            dbContext.Sales.Add(new SaleEntity
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
            });
            await dbContext.SaveChangesAsync();
        }

        var content = await UnlockSaleStatusPageAsync(client);

        Assert.Contains("Rembourser", content);
        Assert.Contains("Montant \u00e0 rembourser", content);
        Assert.Contains("10.00 EUR", content);
        Assert.Contains("1.00 EUR", content);
        Assert.Contains("11.00 EUR", content);
        Assert.Contains("Articles d\u00e9fectueux", content);
        Assert.Contains("Marquer d\u00e9fectueux", content);
        Assert.Contains("data-defective-items-root", content);
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
            $"/vente/statut?handler=RefundDefectiveSaleLine&saleLineId={saleLineId}",
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
            $"/vente/statut?handler=MarkDefectiveItem&itemId={itemId}",
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
            "/vente/statut?handler=ChangePhase",
            BuildFormContent(gateContent, ("Input.Phase", "sale")));
        var correctionResponse = await client.PostAsync(
            $"/vente/statut?handler=CorrectSaleLine&saleLineId={saleLineId}",
            BuildFormContent(gateContent));

        foreach (var response in new[] { phaseResponse, correctionResponse })
        {
            response.EnsureSuccessStatusCode();
            var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Mot de passe administrateur", content);
            Assert.Contains("Déverrouiller", content);
            Assert.DoesNotContain("Correction des ventes", content);
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
        var sale = await SeedSalesViewSaleAsync("Client reçu", new DateTime(2026, 5, 4, 11, 15, 0, DateTimeKind.Utc));

        var response = await client.GetAsync($"/caisse/ventes/{sale.SaleId}");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains($"Vente #{sale.SaleId}", content);
        Assert.Contains(sale.FirstReference, content);
        Assert.Contains("Jeu corrigé", content);
        Assert.Contains("Corrigée", content);
        Assert.Contains("Client reçu", content);
        Assert.Contains("Retour à la liste", content);
        Assert.DoesNotContain("Remettre en stock", content);
        Assert.DoesNotContain("Enregistrer la vente", content);
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
        Assert.Contains("Rembours\u00e9e - d\u00e9fectueux", content);
        Assert.Contains("Prix rembours\u00e9", content);
        Assert.Contains("Frais rembours\u00e9s", content);
        Assert.Contains("Total rembours\u00e9", content);
        Assert.Contains("10.00 EUR", content);
        Assert.Contains("1.00 EUR", content);
        Assert.Contains("11.00 EUR", content);
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
        Assert.Contains("8.00 EUR", content);
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
        Assert.Contains("4.00 EUR", content);
        Assert.Contains("payés 2.00 EUR", content);
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
        Assert.Contains("paymentMethod", content);
        Assert.Contains("chequeNumber", content);
        Assert.Contains("chequeBankName", content);
        Assert.Contains("data-checkout-payment-method", content);
        Assert.Contains("buyerName", content);
        Assert.Contains("buyerPostalCode", content);
        Assert.Contains("buyerCity", content);
        Assert.Contains("checkoutCitiesByPostalCode", content);
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
        Assert.Contains(".checkout-buyer-fields", content);
        Assert.Contains(".checkout-totals", content);
        Assert.Contains(".checkout-modal", content);
        Assert.Contains(".checkout-conflict-row", content);
        Assert.Contains(".checkout-print-receipt", content);
        Assert.Contains("@media print", content);
        Assert.Contains("body.checkout-printing", content);
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
            "/vente/statut?handler=Unlock",
            BuildFormContent(gateContent, ("PasswordInput.Password", "test-password")));

        response.EnsureSuccessStatusCode();

        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> GetSaleStatusGateAsync(HttpClient client)
    {
        var gate = await client.GetAsync("/vente/statut");
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
        Assert.Contains("href=\"/vente/statut\"", content);
        Assert.Contains("VENDEURS", content);
        Assert.Contains("CAISSE", content);
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
}
