using CRFuelScraper.API.Extensions;
using CRFuelScraper.API.Middleware;
using CRFuelScraper.Infrastructure.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();

if (builder.Environment.IsEnvironment("Local"))
{
    builder.Logging.AddSimpleConsole(opts =>
    {
        opts.IncludeScopes   = true;
        opts.SingleLine      = true;
        opts.TimestampFormat = "HH:mm:ss ";
    });
}
else
{
    builder.Logging.AddConsole(opts => opts.FormatterName = "json");
    builder.Logging.AddJsonConsole(opts =>
    {
        opts.UseUtcTimestamp   = true;
        opts.IncludeScopes     = true;
        opts.JsonWriterOptions = new JsonWriterOptions { Indented = false };
    });
}

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opts =>
{
    opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

// ── Run DB migrations ─────────────────────────────────────────────────────────
await app.Services.ApplyMigrationsAsync();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
});

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(opts =>
{
    opts.SwaggerEndpoint("/swagger/v1/swagger.json", "Costa Rica Fuel Prices API v1");
    opts.RoutePrefix = app.Environment.IsEnvironment("Local") ? string.Empty : "docs";
});

app.UseCors("Default");
app.UseRateLimiter();

// ── Health checks ─────────────────────────────────────────────────────────────
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResultStatusCodes =
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapControllers();

app.MapGet("/", () => Results.Redirect("/docs")).ExcludeFromDescription();

app.Run();

public partial class Program { }
