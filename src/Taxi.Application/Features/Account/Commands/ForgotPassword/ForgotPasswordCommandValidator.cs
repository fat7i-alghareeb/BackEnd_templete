using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Account.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.EmailRequired)
            .EmailAddress().WithMessage(LocalizationKeys.Validation.EmailInvalid)
            .MaximumLength(256).WithMessage(LocalizationKeys.Validation.EmailTooLong);
    }
}
