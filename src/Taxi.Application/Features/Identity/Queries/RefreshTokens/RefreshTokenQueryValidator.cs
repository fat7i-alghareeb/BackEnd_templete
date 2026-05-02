using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Identity.Queries.RefreshTokens;

public sealed class RefreshTokenQueryValidator : AbstractValidator<RefreshTokenQuery>
{
    public RefreshTokenQueryValidator()
    {
        RuleFor(x => x.ExpiredAccessToken)
            .NotEmpty().WithMessage(LocalizationKeys.Auth.ExpiredAccessTokenInvalid);

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage(LocalizationKeys.RefreshToken.TokenRequired);
    }
}
