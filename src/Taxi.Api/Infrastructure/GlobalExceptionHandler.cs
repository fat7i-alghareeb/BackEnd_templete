using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Taxi.Api.Infrastructure;

public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment) : IExceptionHandler
{
    private readonly IProblemDetailsService problemDetailsService = problemDetailsService;
    private readonly IHostEnvironment environment = environment;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        // Exception type and message are only exposed in Development. Outside it they can
        // leak connection strings, file paths and internal state to any caller; the
        // `requestId` extension added by AddCustomProblemDetails is enough to correlate
        // the response with the logged exception.
        var isDevelopment = this.environment.IsDevelopment();

        return await this.problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = isDevelopment ? exception.GetType().Name : null,
                Title = "Application error",
                Detail = isDevelopment
                    ? exception.Message
                    : "An unexpected error occurred. Quote the requestId when reporting this.",
            }
        });
    }
}
