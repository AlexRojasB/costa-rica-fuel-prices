namespace CRFuelScraper.API.Middleware;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName          = "X-Correlation-Id";
    private const int    MaxCorrelationIdLen = 64;
    public static readonly object CorrelationIdKey = new();

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var raw = context.Request.Headers[HeaderName].FirstOrDefault();

        var correlationId = IsValidCorrelationId(raw)
            ? raw!
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;
        context.Items[CorrelationIdKey] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    private static bool IsValidCorrelationId(string? v) =>
        !string.IsNullOrEmpty(v)
        && v.Length <= MaxCorrelationIdLen
        && v.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
}
