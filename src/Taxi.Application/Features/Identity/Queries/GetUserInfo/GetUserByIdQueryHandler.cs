namespace Taxi.Application.Features.Identity.Queries.GetUserInfo;

using MediatR;
using Microsoft.Extensions.Logging;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

public class GetUserByIdQueryHandler(ILogger<GetUserByIdQueryHandler> logger, IIdentityService identityService)
    : IRequestHandler<GetUserByIdQuery, Result<AppUserDto>>
{
    private readonly ILogger<GetUserByIdQueryHandler> logger = logger;
    private readonly IIdentityService identityService = identityService;

    public async Task<Result<AppUserDto>> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var getUserByIdResult = await this.identityService.GetUserByIdAsync(request.UserId);

        if (getUserByIdResult.IsError)
        {
            this.logger.LogError("User with Id {UserId} {ErrorDetails}", request.UserId, getUserByIdResult.TopError.Description);
            return getUserByIdResult.Errors;
        }

        return getUserByIdResult.Value;
    }
}
