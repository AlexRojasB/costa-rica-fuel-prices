using System.Net;
using System.Net.Http.Headers;
using CRFuelScraper.Core.Interfaces;
using CRFuelScraper.Core.Options;
using CRFuelScraper.Infrastructure.BackgroundServices;
using CRFuelScraper.Infrastructure.Data;
using CRFuelScraper.Infrastructure.Repositories;
using CRFuelScraper.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CRFuelScraper.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static bool IsSqlite(string connectionString) =>
        connectionString.TrimStart().StartsWith("Data Source", StringComparison.OrdinalIgnoreCase);

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Database — SQLite for local debugging, Npgsql everywhere else
        services.AddDbContext<AppDbContext>(opts =>
        {
            var conn = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required");

            if (IsSqlite(conn))
            {
                opts.UseSqlite(conn);
            }
            else
            {
                opts.UseNpgsql(conn, npg =>
                {
                    npg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), null);
                    npg.CommandTimeout(60);
                });
            }
        });

        services.Configure<ScrapingOptions>(configuration.GetSection("Scraping"));

        // Repositories
        services.AddScoped<IFuelPriceRepository, FuelPriceRepository>();
        services.AddScoped<IScraperLogRepository, ScraperLogRepository>();
        services.AddScoped<ISourceHealthRepository, SourceHealthRepository>();

        // Scrapers — concrete types registered individually so SourceArbiter can inject each one
        services.AddScoped<RecopeFuelScraperService>();
        services.AddScoped<RecopeApiScraperService>();
        services.AddScoped<AresepFuelScraperService>();
        services.AddScoped<ISourceArbiter, SourceArbiter>();
        services.AddScoped<IFuelPriceService, FuelPriceService>();

        services.AddMemoryCache();

        // HttpClient — HTML scraper
        services.AddHttpClient("FuelScraper", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "costa-rica-fuel-prices/1.0 (+https://github.com/AlexRojasB/costa-rica-fuel-prices)");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        // HttpClient — JSON REST API scraper (RECOPE API)
        services.AddHttpClient("FuelScraperJson", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept",
                "application/json, text/plain, */*");
            client.DefaultRequestHeaders.Add("Accept-Language",
                "es-CR,es;q=0.9,en-US;q=0.8,en;q=0.7");
            client.DefaultRequestHeaders.Add("Referer",
                "https://recope.go.cr/");
            client.DefaultRequestHeaders.Add("Origin",
                "https://recope.go.cr");
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHostedService<FuelPriceUpdateBackgroundService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (_, _, _, _) => { });

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1));

    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var isSqlite = db.Database.ProviderName?
            .Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        if (isSqlite)
            await db.Database.EnsureCreatedAsync();
        else if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
    }
}
