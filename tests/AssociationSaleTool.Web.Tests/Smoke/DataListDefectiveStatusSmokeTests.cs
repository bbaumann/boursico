using System.Net;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AssociationSaleTool.Web.Tests.Smoke;

public sealed class DataListDefectiveStatusSmokeTests : IClassFixture<IntakeWebApplicationFactory>
{
    private readonly IntakeWebApplicationFactory _factory;

    public DataListDefectiveStatusSmokeTests(IntakeWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DataListsArticlesTabOffersDefectiveStatusFilter()
    {
        var client = _factory.CreateClient();
        var seller = await SeedSellerWithDefectiveItemAsync();

        var response = await client.GetAsync("/donnees");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("<option value=\"defective\">Défectueux</option>", content);
        Assert.Contains($"data-data-list-status=\"defective\"", content);
        Assert.Contains(seller.DefectiveReference, content);
    }

    [Fact]
    public async Task DataListsSellersTabShowsDefectiveItemCount()
    {
        var client = _factory.CreateClient();
        var seller = await SeedSellerWithDefectiveItemAsync();

        var response = await client.GetAsync("/donnees?onglet=vendeurs");

        response.EnsureSuccessStatusCode();
        var content = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Défectueux", content);
        Assert.Contains($"href=\"/vendeurs/{seller.SellerId}\">{seller.SellerCode}</a>", content);
        Assert.Contains("data-list-defective-count-column\">1", content);
    }

    private async Task<(long SellerId, string SellerCode, string DefectiveReference)> SeedSellerWithDefectiveItemAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var sellerCode = $"DF{suffix}";
        var seller = new SellerEntity
        {
            SellerCode = sellerCode,
            FirstName = "Denise",
            LastName = "Defaut",
            Email = "denise.defaut@example.test",
            Phone = "0611223344",
            PostalAddress = "4 rue Defaut",
            PostalCode = "38100",
            City = "Grenoble"
        };

        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync();

        var defectiveItem = new ItemEntity
        {
            SellerId = seller.Id,
            Reference = $"{sellerCode}001",
            ItemNumber = 1,
            Title = "Jeu defectueux",
            SellerPriceCents = 500,
            Status = "defective"
        };

        dbContext.Items.Add(defectiveItem);
        await dbContext.SaveChangesAsync();

        return (seller.Id, sellerCode, defectiveItem.Reference);
    }
}
