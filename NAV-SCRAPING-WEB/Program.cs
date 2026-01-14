using NAV_SCRAPING.WEB.Components;
using NAV_SCRAPING.WEB.Services;
using NAV_SCRAPING.WEB.Services.Scrapers;
using System.Net;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddRadzenComponents();

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Talis")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseCookies = true,
        AllowAutoRedirect = true,
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });

builder.Services.AddScoped<INavScraper, TalisScraper>();
builder.Services.AddScoped<INavScraper, KAssetScraper>();
builder.Services.AddScoped<INavScraper, AssetPlusScraper>();
builder.Services.AddScoped<INavScraper, LHFundScraper>();
builder.Services.AddScoped<INavScraper, MFCFundScraper>();
builder.Services.AddScoped<INavScraper, DaoFundScraper>();
builder.Services.AddScoped<NavService>();
builder.Services.AddScoped<ExcelExportService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
