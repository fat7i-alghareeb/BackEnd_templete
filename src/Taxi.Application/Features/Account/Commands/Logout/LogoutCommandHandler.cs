namespace Taxi.Application.Features.Account.Commands.Logout;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Errors;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;

public class LogoutCommandHandler(IAppDbContext context, IUser user) : IRequestHandler<LogoutCommand, Result<Success>>
{
    private readonly IAppDbContext context = context;
    private readonly IUser user = user;

    public async Task<Result<Success>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (this.user.Id is not { } userId)
        {
            return ApplicationErrors.UserIdClaimInvalid;
        }

        await this.context.RefreshTokens.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        return Result.Success;
    }
}
