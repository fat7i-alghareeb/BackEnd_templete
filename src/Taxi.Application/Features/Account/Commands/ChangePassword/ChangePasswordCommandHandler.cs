namespace Taxi.Application.Features.Account.Commands.ChangePassword;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Errors;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

public class ChangePasswordCommandHandler(IIdentityService identityService, IAppDbContext context, IUser user)
    : IRequestHandler<ChangePasswordCommand, Result<Success>>
{
    private readonly IIdentityService identityService = identityService;
    private readonly IAppDbContext context = context;
    private readonly IUser user = user;

    public async Task<Result<Success>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (this.user.Id is not { } userId)
        {
            return ApplicationErrors.UserIdClaimInvalid;
        }

        var changeResult = await this.identityService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        if (changeResult.IsError)
        {
            return changeResult.Errors;
        }

        // Other sessions must sign in again with the new password. The caller's current access
        // token stays valid until it expires — JWTs are stateless.
        await this.context.RefreshTokens.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        return Result.Success;
    }
}
