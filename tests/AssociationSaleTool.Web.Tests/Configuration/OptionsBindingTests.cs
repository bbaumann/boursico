using AssociationSaleTool.Web.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
            ["Printing:InvoicePeriod"] = "2611",
            ["Printing:InvoiceCode"] = "AFBJ"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        Assert.Equal("data/event.db", configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!.SqlitePath);
        Assert.Equal("2026 Toy Sale", configuration.GetSection(EventOptions.SectionName).Get<EventOptions>()!.Name);
        Assert.Equal("secret", configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>()!.SharedPassword);
        var printingOptions = configuration.GetSection(PrintingOptions.SectionName).Get<PrintingOptions>()!;
        Assert.Equal("2611AFBJ", printingOptions.InvoiceNumberPrefix);
    }

    [Theory]
    [InlineData("change-me")]
    [InlineData("CHANGE-ME")]
    public void StartupValidationRejectsPlaceholderAdminPassword(string sharedPassword)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"association-sale-tool-placeholder-{Guid.NewGuid():N}.db");

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        var values = new Dictionary<string, string?>
                        {
                            ["Database:SqlitePath"] = databasePath,
                            ["Event:Name"] = "Association Toy Sale Tests",
                            ["Admin:SharedPassword"] = sharedPassword
                        };

                        configurationBuilder.AddInMemoryCollection(values);
                    });
                });

            var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("Admin:SharedPassword must not use the placeholder value.", exception.ToString());
        }
        finally
        {
            TryDelete(databasePath);
            TryDelete($"{databasePath}-wal");
            TryDelete($"{databasePath}-shm");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StartupValidationRejectsBlankAdminPassword(string sharedPassword)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"association-sale-tool-blank-admin-{Guid.NewGuid():N}.db");

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseEnvironment("Development");
                    builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                    {
                        var values = new Dictionary<string, string?>
                        {
                            ["Database:SqlitePath"] = databasePath,
                            ["Event:Name"] = "Association Toy Sale Tests",
                            ["Admin:SharedPassword"] = sharedPassword
                        };

                        configurationBuilder.AddInMemoryCollection(values);
                    });
                });

            var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("Admin:SharedPassword is required", exception.ToString());
        }
        finally
        {
            TryDelete(databasePath);
            TryDelete($"{databasePath}-wal");
            TryDelete($"{databasePath}-shm");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
