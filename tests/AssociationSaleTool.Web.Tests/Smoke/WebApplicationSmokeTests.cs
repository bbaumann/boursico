using AssociationSaleTool.Web.Tests.Infrastructure;

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
        var response = await client.GetAsync("/Sellers");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Sellers", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SellerCreatePageLoadsSuccessfully()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/Sellers/Create");

        response.EnsureSuccessStatusCode();
        Assert.Contains("Create Seller", await response.Content.ReadAsStringAsync());
    }
}
