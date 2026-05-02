using Serilog.Context;

namespace Taxi.Api.Infrastructure;

public class RequestLogContextMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate next = next;

    public Task InvokeAsync(HttpContext httpContext)
    {
        using (LogContext.PushProperty("CorrelationId", httpContext.TraceIdentifier))
        {
            // the purpose is pushing the request correlation id into the log context
            // to be included in the structured log of a life time of http request
            return this.next(httpContext);
        }
    }
}