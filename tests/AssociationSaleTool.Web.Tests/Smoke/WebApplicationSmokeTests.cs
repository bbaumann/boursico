using Microsoft.AspNetCore.Mvc.Testing;

namespace AssociationSaleTool.Web.Tests.Smoke;

public class WebApplicationSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WebApplicationSmokeTests(WebApplicationFactory<Program> factory)
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
}
