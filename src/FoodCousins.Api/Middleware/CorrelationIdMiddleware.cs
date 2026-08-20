using System.Diagnostics;
using Serilog.Context;

namespace FoodCousins.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "FoodCousins.CorrelationId";
    private const int MaxCorrelationIdLength = 128;

    public async Task Invoke(HttpContext context)
    {
        var supplied = context.Request.Headers.TryGetValue(HeaderName, out var header)
            ? header.ToString().Trim()
            : string.Empty;

        var correlationId = !string.IsNullOrWhiteSpace(supplied) && supplied.Length <= MaxCorrelationIdLength
            ? supplied
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
