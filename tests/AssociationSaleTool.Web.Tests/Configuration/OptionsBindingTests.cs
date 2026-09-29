using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Data.Entities;
using AssociationSaleTool.Web.Services;
using AssociationSaleTool.Web.Services.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssociationSaleTool.Web.Tests.Configuration;

public class OptionsBindingTests
{
    [Fact]
    public void BindsCanonicalSections()
    {
        var values = new Dictionary<string, string?>
        {
            ["Database:SqlitePath"] = "data/event.db",
            ["Database:ActiveProfile"] = "current",
            ["Database:Profiles:current"] = "data/event.db",
            ["Database:Profiles:user-test"] = "data/user-test.db",
            ["Event:Name"] = "2026 Toy Sale",
            ["Admin:SharedPassword"] = "secret",
            ["Printing:InvoicePeriod"] = "2611",
            ["Printing:InvoiceCode"] = "AFBJ",
            ["Labels:SoftCapacityPerLetter"] = "310"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()!;
        Assert.Equal("data/event.db", databaseOptions.SqlitePath);
        Assert.Equal("current", databaseOptions.ActiveProfile);
        Assert.Equal("data/event.db", databaseOptions.ResolveSqlitePath());
        Assert.Equal("data/user-test.db", databaseOptions.Profiles["user-test"]);
        Assert.Equal("2026 Toy Sale", configuration.GetSection(EventOptions.SectionName).Get<EventOptions>()!.Name);
        Assert.Equal("secret", configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>()!.SharedPassword);
        var printingOptions = configuration.GetSection(PrintingOptions.SectionName).Get<PrintingOptions>()!;
        Assert.Equal("2611AFBJ", printingOptions.InvoiceNumberPrefix);
        Assert.Equal(310, configuration.GetSection(LabelsOptions.SectionName).Get<LabelsOptions>()!.SoftCapacityPerLetter);
    }

    [Fact]
    public void DatabaseOptionsResolveActiveProfilePath()
    {
        var options = new DatabaseOptions
        {
            SqlitePath = "data/current.db",
            ActiveProfile = "user-test",
            Profiles = new Dictionary<string, string?>
            {
                ["current"] = "data/current.db",
                ["user-test"] = "data/user-test.db"
            }
        };

        Assert.Equal("data/user-test.db", options.ResolveSqlitePath());
    }

    [Fact]
    public void DatabaseOptionsFallbackToLegacySqlitePathWhenNoActiveProfileIsSet()
    {
        var options = new DatabaseOptions
        {
            SqlitePath = "data/current.db"
        };

        Assert.Equal("data/current.db", options.ResolveSqlitePath());
    }

    [Fact]
    public void StartupUsesActiveDatabaseProfilePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"association-sale-tool-profile-{Guid.NewGuid():N}");
        var legacyPath = Path.Combine(directory, "legacy.db");
        var userTestPath = Path.Combine(directory, "user-test.db");

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
                            ["Database:SqlitePath"] = legacyPath,
                            ["Database:ActiveProfile"] = "user-test",
                            ["Database:Profiles:current"] = legacyPath,
                            ["Database:Profiles:user-test"] = userTestPath,
                            ["Event:Name"] = "Association Toy Sale Tests",
                            ["Admin:SharedPassword"] = "secret"
                        };

                        configurationBuilder.AddInMemoryCollection(values);
                    });
                });

            using var client = factory.CreateClient();

            Assert.True(File.Exists(userTestPath));
            Assert.False(File.Exists(legacyPath));
        }
        finally
        {
            TryDelete(legacyPath);
            TryDelete($"{legacyPath}-wal");
            TryDelete($"{legacyPath}-shm");
            TryDelete(userTestPath);
            TryDelete($"{userTestPath}-wal");
            TryDelete($"{userTestPath}-shm");
            TryDeleteDirectory(directory);
        }
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

    [Fact]
    public async Task LabelsOptions_SoftCapacityPerLetterReachesIntakeServiceThroughDependencyInjection()
    {
        // IntakeService takes IOptions<LabelsOptions> with a default-value
        // fallback so `new IntakeService(context)` keeps compiling in unit
        // tests. That optional parameter is exactly the shape DI silently
        // resolves to null for an unregistered concrete LabelsOptions - this
        // test exercises the real container (Program.cs's registrations),
        // not a hand-built one, so it would have caught IntakeService quietly
        // falling back to the 455 default instead of the configured value.
        var databasePath = Path.Combine(Path.GetTempPath(), $"association-sale-tool-labels-{Guid.NewGuid():N}.db");

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
                            ["Admin:SharedPassword"] = "secret",
                            ["Labels:SoftCapacityPerLetter"] = "1"
                        };

                        configurationBuilder.AddInMemoryCollection(values);
                    });
                });

            using var client = factory.CreateClient();

            using var scope = factory.Services.CreateScope();
            var intakeService = scope.ServiceProvider.GetRequiredService<IntakeService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var seller = await intakeService.CreateSellerAsync(new CreateSellerRequest(
                "Ada",
                "Lovelace",
                null,
                "0123",
                "1 rue des Tests",
                null,
                PostalCode: "38000",
                City: "Grenoble",
                IdentityDocumentType: "CNI",
                IdentityDocumentNumber: "123456789",
                IdentityDocumentIssuedOn: new DateOnly(2020, 1, 15)));

            // Pre-fill this seller's own letter ('B', from the first
            // generated seller code "BB") right up to the configured
            // capacity of 1.
            dbContext.Items.Add(new ItemEntity
            {
                SellerId = seller.Id,
                Reference = "B001",
                Title = "Déjà en stock",
                SellerPriceCents = 100,
                Status = "in_stock"
            });
            await dbContext.SaveChangesAsync();

            await intakeService.CreateItemBatchAsync(new CreateItemBatchRequest(
                seller.Id, IntakeMode.EmailPreregistration, [new CreateItemRowRequest("Puzzle", 500)]));

            var result = await intakeService.AllocateDepositReferencesAsync(seller.Id);

            Assert.Equal(SellerDepositStatusChangeResult.Changed, result);
            var newReference = await dbContext.Items
                .Where(x => x.SellerId == seller.Id && x.Title == "Puzzle")
                .Select(x => x.Reference)
                .SingleAsync();

            // Letter B is already at the configured cap of 1, so the new
            // item can only land in B (the 455 default) if the configured
            // value never reached IntakeService. It must spill to C instead.
            Assert.StartsWith("C", newReference);
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

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path);
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
