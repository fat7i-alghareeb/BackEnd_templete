using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Identity.Queries.GenerateTokens;

public sealed class GenerateTokenQueryValidator : AbstractValidator<GenerateTokenQuery>
{
    public GenerateTokenQueryValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.EmailRequired)
            .EmailAddress().WithMessage(LocalizationKeys.Validation.EmailInvalid);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.PasswordRequired);
    }
}
