using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Identity.Commands.RefreshToken;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.ExpiredAccessToken)
            .NotEmpty().WithMessage(LocalizationKeys.Auth.ExpiredAccessTokenInvalid);

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(LocalizationKeys.RefreshToken.TokenRequired);
    }
}
