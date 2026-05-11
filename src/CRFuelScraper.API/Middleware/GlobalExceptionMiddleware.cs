using System.Net;
using CRFuelScraper.Core.DTOs;

namespace CRFuelScraper.API.Middleware;

public class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private async Task WriteErrorResponseAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;

        var response = env.IsDevelopment()
            ? ApiResponse<object>.Fail(
                "An unexpected error occurred.",
                [ex.Message, ex.StackTrace ?? string.Empty])
            : ApiResponse<object>.Fail("An unexpected error occurred. Please try again later.");

        await context.Response.WriteAsJsonAsync(response);
    }
}
