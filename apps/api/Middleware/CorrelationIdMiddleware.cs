using Serilog.Context;

namespace Desk.Api.Middleware;

/// <summary>
/// Assigns a correlation id to every request (honouring an inbound X-Correlation-ID),
/// echoes it on the response, and pushes it into the Serilog context so all logs for the
/// request carry it. This is the id later threaded into sync events and audit entries.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming)
                            && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString();

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
