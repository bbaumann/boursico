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
    .Validate(options => !string.IsNullOrWhiteSpace(options.SqlitePath), "Database:SqlitePath is required")
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
    .Validate(options => !string.IsNullOrWhiteSpace(options.ReceiptHeaderText), "Printing:ReceiptHeaderText is required")
    .ValidateOnStart();
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    var databasePath = Path.GetFullPath(databaseOptions.SqlitePath, builder.Environment.ContentRootPath);
    var databaseDirectory = Path.GetDirectoryName(databasePath);

    if (!string.IsNullOrWhiteSpace(databaseDirectory))
    {
        Directory.CreateDirectory(databaseDirectory);
    }

    options.UseSqlite($"Data Source={databasePath}");
    options.AddInterceptors(serviceProvider.GetRequiredService<SqliteConnectionInterceptor>());
});
builder.Services.AddScoped<IntakeService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<SalePhaseService>();
builder.Services.AddScoped<SaleCorrectionService>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();

public partial class Program;
