using AssociationSaleTool.Web.Configuration;
using Microsoft.Extensions.Configuration;

namespace AssociationSaleTool.Web.Tests.Configuration;

public class OptionsBindingTests
{
    [Fact]
    public void BindsCanonicalSections()
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = "data/event.db",
            ["Event:Name"] = "2026 Toy Sale",
            ["Admin:SharedPassword"] = "secret",
            ["Printing:ReceiptHeaderText"] = "Association Toy Sale"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        Assert.Equal("data/event.db", configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!.SqlitePath);
        Assert.Equal("2026 Toy Sale", configuration.GetSection(EventOptions.SectionName).Get<EventOptions>()!.Name);
        Assert.Equal("secret", configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>()!.SharedPassword);
        Assert.Equal("Association Toy Sale", configuration.GetSection(PrintingOptions.SectionName).Get<PrintingOptions>()!.ReceiptHeaderText);
    }
}
