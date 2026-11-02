using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

using Taxi.Contracts.Common;

namespace Taxi.Api.Infrastructure;

public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    IStringLocalizer<SharedResource> localizer) : IExceptionHandler
{
    private readonly IProblemDetailsService problemDetailsService = problemDetailsService;
    private readonly IHostEnvironment environment = environment;
    private readonly IStringLocalizer<SharedResource> localizer = localizer;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Kestrel signals client protocol errors (body over MaxRequestBodySize → 413, malformed
        // request → 400) with BadHttpRequestException. Those are the caller's fault and must keep
        // their status instead of turning into a 500.
        if (exception is BadHttpRequestException badRequest)
        {
            httpContext.Response.StatusCode = badRequest.StatusCode;

            return await this.problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Title = ReasonPhrases.GetReasonPhrase(badRequest.StatusCode),
                    Detail = badRequest.Message,
                },
            });
        }

        // Optimistic concurrency (xmin token): the row changed between load and save. Not a server
        // fault — the client should reload and retry.
        if (exception is DbUpdateConcurrencyException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

            return await this.problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = this.localizer[LocalizationKeys.Concurrency.Conflict].Value,
                },
            });
        }

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
