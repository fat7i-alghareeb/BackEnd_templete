namespace Taxi.Application.Common.Behaviours;

using MediatR.Pipeline;

using Microsoft.Extensions.Logging;

using Taxi.Application.Common.Interfaces;

/// <summary>
/// Logs one line per request before the pipeline runs.
/// <para>
/// Registered explicitly via <c>AddOpenRequestPreProcessor</c> in <c>AddApplication()</c>:
/// MediatR's assembly scan does NOT discover open-generic <see cref="IRequestPreProcessor{TRequest}"/>
/// implementations, so without that call this class never executes.
/// </para>
/// <para>
/// Only the user id is logged. Resolving the user *name* would cost an
/// <c>IIdentityService.GetUserNameAsync</c> database round-trip on every authenticated request;
/// <c>PerformanceBehaviour</c> already pays that cost, but only for requests slower than 500 ms.
/// </para>
/// </summary>
public class LoggingBehaviour<TRequest>(ILogger<TRequest> logger, IUser user)
    : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger logger = logger;
    private readonly IUser user = user;

    public Task Process(TRequest request, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Request: {Name} {@UserId} {@Request}",
            typeof(TRequest).Name,
            this.user.Id ?? string.Empty,
            request);

        return Task.CompletedTask;
    }
}
