using AssociationSaleTool.Web.Configuration;
using AssociationSaleTool.Web.Data;
using AssociationSaleTool.Web.Infrastructure;
using AssociationSaleTool.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSingleton<SqliteConnectionInterceptor>();
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(
        options => options.HasResolvedSqlitePath(),
        "Database:SqlitePath is required, or Database:ActiveProfile must reference a configured Database:Profiles entry.")
    .ValidateOnStart();
builder.Services
    .AddOptions<EventOptions>()
    .Bind(builder.Configuration.GetSection(EventOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.Name), "Event:Name is required")
    .ValidateOnStart();
builder.Services
    .AddOptions<AdminOptions>()
    .Bind(builder.Configuration.GetSection(AdminOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.SharedPassword), "Admin:SharedPassword is required")
    .Validate(
        options => !string.Equals(options.SharedPassword?.Trim(), "change-me", StringComparison.OrdinalIgnoreCase),
        "Admin:SharedPassword must not use the placeholder value.")
    .ValidateOnStart();
builder.Services
    .AddOptions<PrintingOptions>()
    .Bind(builder.Configuration.GetSection(PrintingOptions.SectionName))
    .Validate(options => options.InvoicePeriod.Trim().Length == 4 && options.InvoicePeriod.Trim().All(char.IsDigit), "Printing:InvoicePeriod must use YYMM format")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InvoiceCode), "Printing:InvoiceCode is required")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SellerName), "Printing:SellerName is required")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SellerAddress), "Printing:SellerAddress is required")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SellerSiret), "Printing:SellerSiret is required")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SellerLegalStatus), "Printing:SellerLegalStatus is required")
    .Validate(options => !string.IsNullOrWhiteSpace(options.VatExemptionText), "Printing:VatExemptionText is required")
    .Validate(options => !string.IsNullOrWhiteSpace(options.LogoPath), "Printing:LogoPath is required")
    .ValidateOnStart();
builder.Services
    .AddOptions<LabelsOptions>()
    .Bind(builder.Configuration.GetSection(LabelsOptions.SectionName));
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var databasePath = Path.GetFullPath(databaseOptions.ResolveSqlitePath(), builder.Environment.ContentRootPath);
    var databaseDirectory = Path.GetDirectoryName(databasePath);

    if (!string.IsNullOrWhiteSpace(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }

    options.UseSqlite($"Data Source={databasePath}");
    options.AddInterceptors(serviceProvider.GetRequiredService<SqliteConnectionInterceptor>());
});
builder.Services.AddScoped<IntakeService>();
builder.Services.AddScoped<DepositDeskService>();
builder.Services.AddScoped<ReferenceAllocationService>();
builder.Services.AddScoped<SellerDuplicateService>();
builder.Services.AddSingleton(_ => IserePostalCityLookup.LoadFromJsonFile(
    Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "data", "isere-postal-cities.json")));
builder.Services.AddScoped<SellerFormImportParser>();
builder.Services.AddScoped<SellerFormImportService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<InvoiceNumberService>();
builder.Services.AddScoped<SalesViewService>();
builder.Services.AddScoped<DataListService>();
builder.Services.AddScoped<CashRegisterService>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<SaleStatisticsService>();
builder.Services.AddScoped<SalePhaseService>();
builder.Services.AddScoped<SaleCorrectionService>();
builder.Services.AddScoped<DefectiveItemService>();
builder.Services.AddScoped<ItemCorrectionService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5050";
app.Run($"http://0.0.0.0:{port}");

app.Run();

public partial class Program;
