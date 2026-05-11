using System.Text.Json;
using System.Text.Json.Serialization;

namespace CRFuelScraper.Core.DTOs.RecopeApi;

/// <summary>
/// Typed DTO matching the RECOPE consumer-price API response schema.
/// Source: https://api.recope.go.cr/ventas/precio/consumidor
/// </summary>
public record RecopeApiPriceItemDto
{
    [JsonPropertyName("nomprod")]
    public string? NomProd { get; init; }

    [JsonPropertyName("nombreProducto")]
    public string? NombreProducto { get; init; }

    [JsonIgnore]
    public string? ProductName => NomProd ?? NombreProducto;

    /// <summary>Total consumer price including taxes. Stored as JsonElement to handle quoted-string and bare-number JSON values.</summary>
    [JsonPropertyName("preciototal")]
    public JsonElement? PrecioTotal { get; init; }

    [JsonPropertyName("precsinimp")]
    public string? PrecSinImp { get; init; }

    [JsonPropertyName("impuesto")]
    public string? Impuesto { get; init; }

    [JsonPropertyName("margenpromedio")]
    public string? MargenPromedio { get; init; }

    /// <summary>Effective date — YYYYMMDD without separators (e.g. "20260507").</summary>
    [JsonPropertyName("fecha")]
    public string? Fecha { get; init; }

    /// <summary>Date this record was last updated — "yyyy/MM/dd" format.</summary>
    [JsonPropertyName("fechaupd")]
    public string? FechaUpd { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("tipo")]
    public string? Tipo { get; init; }
}
