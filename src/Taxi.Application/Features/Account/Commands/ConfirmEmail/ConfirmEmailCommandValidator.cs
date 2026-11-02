using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Account.Commands.ConfirmEmail;

public sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage(LocalizationKeys.Validation.UserIdRequired);
        RuleFor(x => x.Token).NotEmpty().WithMessage(LocalizationKeys.Validation.TokenRequired);
    }
}
