using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Account.Commands.ResendConfirmation;

public sealed class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.EmailRequired)
            .EmailAddress().WithMessage(LocalizationKeys.Validation.EmailInvalid)
            .MaximumLength(256).WithMessage(LocalizationKeys.Validation.EmailTooLong);
    }
}
