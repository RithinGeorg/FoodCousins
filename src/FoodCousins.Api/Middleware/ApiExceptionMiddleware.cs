using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodCousins.Api.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller disconnected/cancelled the request. This is not an application failure.
            logger.LogInformation(
                "HTTP request was cancelled by the client. Method={Method}, Path={Path}, TraceIdentifier={TraceIdentifier}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);
        }
        catch (Exception ex)
        {
            var (status, title, exposeDetail) = ex switch
            {
                UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", true),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found", true),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", true),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict", false),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Request conflict", true),
                TimeoutException => (StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable", false),
                _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", false)
            };

            var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
                ? value?.ToString()
                : context.TraceIdentifier;

            if (status >= 500)
            {
                logger.LogError(
                    ex,
                    "Unhandled API error. Status={Status}, Method={Method}, Path={Path}, CorrelationId={CorrelationId}",
                    status,
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);
            }
            else
            {
                logger.LogWarning(
                    ex,
                    "API request failed. Status={Status}, Method={Method}, Path={Path}, CorrelationId={CorrelationId}",
                    status,
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);
            }

            // If headers/body already started, middleware can no longer safely translate the exception.
            // Rethrow so ASP.NET Core/Kestrel can terminate the response correctly and telemetry records the failure.
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = exposeDetail ? ex.Message : "The request could not be completed.",
                Instance = context.Request.Path
            };
            problem.Extensions["correlationId"] = correlationId;

            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
    }
}
