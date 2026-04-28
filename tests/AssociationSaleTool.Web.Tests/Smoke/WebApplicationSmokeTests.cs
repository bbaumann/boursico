using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        Assert.Contains("Frais de caisse", content);
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
        Assert.DoesNotContain("Imprimer", content);
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
