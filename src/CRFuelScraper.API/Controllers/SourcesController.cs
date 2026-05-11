using Asp.Versioning;
using CRFuelScraper.Core.DTOs;
using CRFuelScraper.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CRFuelScraper.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/fuel")]
[EnableRateLimiting("ip-policy")]
[Produces("application/json")]
public class SourcesController(
    ISourceHealthRepository healthRepo,
    ILogger<SourcesController> logger) : ControllerBase
{
    private static readonly (string Name, int Priority)[] SourcePriorities =
    [
        ("RECOPE API",  1),
        ("RECOPE HTML", 2),
        ("ARESEP",      3)
    ];

    /// <summary>Retorna el estado de salud de cada fuente de datos.</summary>
    /// <remarks>
    /// Muestra el resultado del último intento de scraping por fuente:
    /// - `status`: `Healthy`, `Degraded` o `Down`
    /// - `responseMs`: tiempo de respuesta en ms
    /// - `avgResponseMs`: promedio móvil de los últimos 10 intentos
    /// - `consecutiveFailures`: fallos consecutivos desde el último Healthy
    /// </remarks>
    /// <response code="200">Estado de salud de las fuentes.</response>
    [HttpGet("sources/health")]
    [ProducesResponseType(typeof(ApiResponse<SourcesHealthResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSourcesHealth(CancellationToken ct)
    {
        var latest  = await healthRepo.GetLatestPerSourceAsync(ct);
        var success = await healthRepo.GetLastSuccessPerSourceAsync(ct);

        var successBySource = success.ToDictionary(h => h.SourceName, h => h.CheckedAt);

        var dtos = SourcePriorities.Select(meta =>
        {
            var record = latest.FirstOrDefault(h => h.SourceName == meta.Name);
            return new SourceHealthDto(
                Name:                meta.Name,
                Priority:            meta.Priority,
                Status:              record?.Status.ToString() ?? "Unknown",
                LastChecked:         record?.CheckedAt ?? DateTime.MinValue,
                LastSuccess:         successBySource.GetValueOrDefault(meta.Name),
                ResponseMs:          record?.ResponseMs,
                LastEffectiveDate:   record?.EffectiveDate,
                Error:               SanitizeError(record?.ErrorMessage),
                ConsecutiveFailures: record?.ConsecutiveFailures ?? 0,
                AvgResponseMs:       record?.AvgResponseMs
            );
        }).ToList();

        logger.LogDebug("Sources health requested — {Count} sources", dtos.Count);

        return Ok(ApiResponse<SourcesHealthResponse>.Ok(
            new SourcesHealthResponse(dtos, DateTime.UtcNow)));
    }

    private static string? SanitizeError(string? raw) =>
        raw is null ? null :
        raw.Length > 120 ? string.Concat(raw.AsSpan(0, 120), "…") : raw;

    /// <summary>Retorna metadatos sobre los tipos de combustible y proveedores de datos.</summary>
    /// <response code="200">Metadatos del servicio.</response>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ApiResponse<FuelMetadataResponse>), StatusCodes.Status200OK)]
    public IActionResult GetMetadata()
    {
        var metadata = new FuelMetadataResponse(
            Country:  "Costa Rica",
            Currency: "CRC (Colón Costarricense)",
            FuelTypes:
            [
                new FuelTypeInfo("Super",   "Gasolina Súper",   "Gasolina de 95 octanos"),
                new FuelTypeInfo("Regular", "Gasolina Regular", "Gasolina de 91 octanos (Plus 91)"),
                new FuelTypeInfo("Diesel",  "Diésel",           "Diésel bajo en azufre (50 ppm)")
            ],
            DataProvider: new DataProviderInfo(
                Name:      "RECOPE",
                FullName:  "Refinadora Costarricense de Petróleo",
                Url:       "https://www.recope.go.cr",
                SourceUrl: "https://www.recope.go.cr/productos/precios-nacionales/"
            ),
            RegulatoryBody: new RegulatoryBodyInfo(
                Name:     "ARESEP",
                FullName: "Autoridad Reguladora de los Servicios Públicos",
                Url:      "https://www.aresep.go.cr"
            ),
            UpdateFrequency: "Daily at 07:00 UTC (price changes are irregular — can be weeks or months apart)",
            Timezone:        "America/Costa_Rica (UTC-6, no DST)",
            Disclaimer:      "Precios oficiales regulados por ARESEP y publicados por RECOPE. " +
                             "Este servicio no es oficial. Verifique siempre en las fuentes primarias."
        );

        return Ok(ApiResponse<FuelMetadataResponse>.Ok(metadata));
    }
}
