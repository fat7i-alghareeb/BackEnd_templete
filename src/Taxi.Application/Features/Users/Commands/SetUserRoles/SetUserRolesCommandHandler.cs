namespace Taxi.Application.Features.Users.Commands.SetUserRoles;

using MediatR;
using Taxi.Application.Common.Errors;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Security;
using Taxi.Domain.Common.Results;

public class SetUserRolesCommandHandler(IIdentityService identityService, IUser user)
    : IRequestHandler<SetUserRolesCommand, Result<Updated>>
{
    private readonly IIdentityService identityService = identityService;
    private readonly IUser user = user;

    public async Task<Result<Updated>> Handle(SetUserRolesCommand request, CancellationToken cancellationToken)
    {
        // Canonical casing and no duplicates: "manager", "MANAGER" → "Manager".
        var roles = Roles.All
            .Where(role => request.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // A Manager demoting themselves could leave the system with nobody able to manage users.
        if (request.UserId == this.user.Id && !roles.Contains(Roles.Manager))
        {
            return ApplicationErrors.CannotRemoveOwnManagerRole;
        }

        return await this.identityService.SetRolesAsync(request.UserId, roles);
    }
}
