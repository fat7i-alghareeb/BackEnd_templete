using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Account.Commands.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(LocalizationKeys.Validation.CurrentPasswordRequired);

        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(LocalizationKeys.Validation.NewPasswordRequired);
    }
}
