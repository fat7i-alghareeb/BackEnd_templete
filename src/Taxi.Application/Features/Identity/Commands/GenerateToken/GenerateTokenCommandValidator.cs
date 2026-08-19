using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Identity.Commands.GenerateToken;

public sealed class GenerateTokenCommandValidator : AbstractValidator<GenerateTokenCommand>
{
    public GenerateTokenCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.EmailRequired)
            .EmailAddress().WithMessage(LocalizationKeys.Validation.EmailInvalid);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.PasswordRequired);
    }
}
