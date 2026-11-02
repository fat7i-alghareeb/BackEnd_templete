using FluentValidation;
using Taxi.Application.Common.Security;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Users.Commands.SetUserRoles;

public sealed class SetUserRolesCommandValidator : AbstractValidator<SetUserRolesCommand>
{
    public SetUserRolesCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage(LocalizationKeys.Validation.UserIdRequired);

        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.RolesRequired);

        RuleForEach(x => x.Roles)
            .Must(role => Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage(LocalizationKeys.Validation.RoleInvalid);
    }
}
