using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Account.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.EmailRequired)
            .EmailAddress().WithMessage(LocalizationKeys.Validation.EmailInvalid)
            .MaximumLength(256).WithMessage(LocalizationKeys.Validation.EmailTooLong);

        // Length and character rules live in the Identity password policy, checked by the handler.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.PasswordRequired);
    }
}
