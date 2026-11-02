using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Account.Commands.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.EmailRequired)
            .EmailAddress().WithMessage(LocalizationKeys.Validation.EmailInvalid)
            .MaximumLength(256).WithMessage(LocalizationKeys.Validation.EmailTooLong);

        RuleFor(x => x.Token).NotEmpty().WithMessage(LocalizationKeys.Validation.TokenRequired);

        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(LocalizationKeys.Validation.NewPasswordRequired);
    }
}
