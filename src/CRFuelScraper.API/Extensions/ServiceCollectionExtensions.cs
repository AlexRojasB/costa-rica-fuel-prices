using System.Threading.RateLimiting;
using Asp.Versioning;
using CRFuelScraper.Infrastructure.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

namespace CRFuelScraper.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();

        services.AddApiVersioning(opts =>
        {
            opts.DefaultApiVersion = new ApiVersion(1, 0);
            opts.AssumeDefaultVersionWhenUnspecified = true;
            opts.ReportApiVersions = true;
        })
        .AddApiExplorer(opts =>
        {
            opts.GroupNameFormat = "'v'VVV";
            opts.SubstituteApiVersionInUrl = true;
        });

        // Rate limiting — 100 req/min per IP
        services.AddRateLimiter(opts =>
        {
            opts.AddSlidingWindowLimiter("ip-policy", limiter =>
            {
                limiter.PermitLimit = 100;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.SegmentsPerWindow = 6;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 5;
            });

            opts.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers["Retry-After"] = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests. Max 100 requests per minute per IP.",
                    retryAfterSeconds = 60
                }, ct);
            };
        });

        services.AddCors(opts =>
        {
            opts.AddPolicy("Default", policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opts =>
        {
            opts.SwaggerDoc("v1", new OpenApiInfo
            {
                Title   = "Costa Rica Fuel Prices API",
                Version = "v1",
                Description = """
                    ## Costa Rica Official Fuel Prices

                    API de datos públicos que expone los **precios oficiales de combustible en Costa Rica**,
                    publicados por [RECOPE](https://www.recope.go.cr/productos/precios-nacionales/tabla-precios/)
                    y regulados por [ARESEP](https://aresep.go.cr).

                    ### Tipos de combustible disponibles
                    | Clave | Nombre |
                    |-------|--------|
                    | `super` | Gasolina Súper (95 octanos) |
                    | `regular` | Gasolina Plus 91 |
                    | `diesel` | Diésel 50 ppm |

                    ### Atribución
                    > Los datos son provistos por **RECOPE** y regulados por **ARESEP**.
                    > Este proyecto no es oficial. Verifique siempre en las [fuentes primarias](https://www.recope.go.cr).
                    """,
                Contact = new OpenApiContact
                {
                    Name = "costa-rica-fuel-prices",
                    Url  = new Uri("https://github.com/AlexRojasB/costa-rica-fuel-prices")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url  = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) opts.IncludeXmlComments(xmlPath);
        });

        var connString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var healthChecks = services.AddHealthChecks();
        if (!InfrastructureServiceExtensions.IsSqlite(connString))
            healthChecks.AddNpgSql(connString, name: "postgres", tags: ["db", "ready"]);

        return services;
    }
}
