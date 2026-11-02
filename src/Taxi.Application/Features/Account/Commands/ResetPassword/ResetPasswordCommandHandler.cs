namespace Taxi.Application.Features.Account.Commands.ResetPassword;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

public class ResetPasswordCommandHandler(IIdentityService identityService, IAppDbContext context)
    : IRequestHandler<ResetPasswordCommand, Result<Success>>
{
    private readonly IIdentityService identityService = identityService;
    private readonly IAppDbContext context = context;

    public async Task<Result<Success>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var resetResult = await this.identityService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
        if (resetResult.IsError)
        {
            return resetResult.Errors;
        }

        // Whoever knew the old password must not keep refreshing a session with it.
        var userId = resetResult.Value;
        await this.context.RefreshTokens.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        return Result.Success;
    }
}
