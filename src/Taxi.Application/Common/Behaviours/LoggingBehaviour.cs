namespace Taxi.Application.Common.Behaviours;

using MediatR.Pipeline;

using Microsoft.Extensions.Logging;

using Taxi.Application.Common.Interfaces;

public class LoggingBehaviour<TRequest>(ILogger<TRequest> logger, IUser user, IIdentityService identityService)
    : IRequestPreProcessor<TRequest>
    where TRequest : notnull
{
    private readonly ILogger logger = logger;
    private readonly IUser user = user;
    private readonly IIdentityService identityService = identityService;

    public async Task Process(TRequest request, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = this.user.Id ?? string.Empty;
        string? userName = string.Empty;

        if (!string.IsNullOrEmpty(userId))
        {
            userName = await this.identityService.GetUserNameAsync(userId);
        }

        this.logger.LogInformation(
            "Request: {Name} {@UserId} {@UserName} {@Request}", requestName, userId, userName, request);
    }
}