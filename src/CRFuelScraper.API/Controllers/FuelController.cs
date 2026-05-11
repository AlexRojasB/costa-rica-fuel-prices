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
public class FuelController(IFuelPriceService fuelPriceService)
    : ControllerBase
{
    /// <summary>Retorna los precios oficiales vigentes de todos los tipos de combustible.</summary>
    /// <remarks>
    /// Los precios provienen de RECOPE y son regulados por ARESEP. Se actualizan una vez al día (07:00 UTC).
    ///
    /// **Campos de confiabilidad:**
    ///
    /// | `confidenceScore` | Fuente | Significado |
    /// |---|---|---|
    /// | `0.99` | RECOPE API | Fuente primaria — REST estructurado |
    /// | `0.92` | RECOPE HTML | Fallback — parsing de la página oficial |
    /// | `0.85` | ARESEP | Fallback regulatorio |
    /// | `0.30` | LastKnownGood | Todos los scrapers fallaron — dato de BD |
    ///
    /// `isStale: true` indica que el precio tiene más de 45 días o provino del fallback.
    ///
    /// Usa `?includeAll=true` para incluir Kerosene además de Super, Regular y Diésel.
    /// </remarks>
    /// <param name="includeAll">Si es <c>true</c>, incluye Kerosene en la respuesta.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Precios vigentes.</response>
    /// <response code="404">Aún no hay precios disponibles (API recién desplegada).</response>
    [HttpGet("latest")]
    [ProducesResponseType(typeof(ApiResponse<FuelLatestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatest(
        [FromQuery] bool includeAll = false,
        CancellationToken ct = default)
    {
        var result = await fuelPriceService.GetLatestPricesAsync(includeAll, ct);

        if (result.Prices.Count == 0)
            return NotFound(ApiResponse<object>.Fail("No fuel prices available. Check back shortly."));

        return Ok(ApiResponse<FuelLatestResponse>.Ok(result));
    }

    /// <summary>Retorna el precio vigente de un tipo de combustible específico.</summary>
    /// <remarks>Códigos canónicos soportados: `super`, `regular`, `diesel`, `kerosene`.</remarks>
    /// <param name="canonicalCode">Código canónico del combustible (super | regular | diesel | kerosene).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Precio vigente del combustible solicitado.</response>
    /// <response code="404">Código canónico desconocido o precio no disponible.</response>
    [HttpGet("prices/{canonicalCode}")]
    [ProducesResponseType(typeof(ApiResponse<FuelPriceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCanonicalCode(
        string canonicalCode, CancellationToken ct)
    {
        var result = await fuelPriceService.GetPriceByCanonicalCodeAsync(canonicalCode, ct);

        if (result is null)
            return NotFound(ApiResponse<object>.Fail(
                $"No price found for '{canonicalCode}'. Valid codes: super, regular, diesel, kerosene."));

        return Ok(ApiResponse<FuelPriceDto>.Ok(result));
    }

    /// <summary>Retorna el historial de precios con filtros opcionales.</summary>
    /// <remarks>
    /// Filtros: `from`, `to` (yyyy-MM-dd), `fuelType`, `canonicalCode`, `source`, `hasConflict`, `page`, `pageSize` (máx. 100).
    /// </remarks>
    /// <param name="request">Filtros opcionales de búsqueda.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Historial paginado agrupado por tipo de combustible.</response>
    /// <response code="400">Parámetros de filtro inválidos.</response>
    [HttpGet("history")]
    [ProducesResponseType(typeof(PaginatedResponse<FuelHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] FuelHistoryRequest request, CancellationToken ct)
    {
        if (request.From.HasValue && request.To.HasValue && request.From > request.To)
            return BadRequest(ApiResponse<object>.Fail("'from' date must be before 'to' date."));

        if (request.From.HasValue && request.From > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            return BadRequest(ApiResponse<object>.Fail("'from' date cannot be in the future."));

        var result = await fuelPriceService.GetHistoryAsync(request, ct);
        return Ok(result);
    }
}
