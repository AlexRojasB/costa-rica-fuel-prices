namespace CRFuelScraper.API.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"]  = "nosniff";
        headers["X-Frame-Options"]         = "DENY";
        headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"]      = "geolocation=(), microphone=(), camera=()";
        headers["Cache-Control"]           = "no-store";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        if (context.Request.IsHttps ||
            string.Equals(context.Request.Headers["X-Forwarded-Proto"], "https",
                StringComparison.OrdinalIgnoreCase))
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await next(context);
    }
}
