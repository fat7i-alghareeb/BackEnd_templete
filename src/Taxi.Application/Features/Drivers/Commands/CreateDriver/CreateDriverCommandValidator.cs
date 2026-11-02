using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Drivers.Commands.CreateDriver;

public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.FullNameRequired)
            .MaximumLength(200).WithMessage(LocalizationKeys.Validation.FullNameTooLong);

        RuleFor(x => x.LicenseNumber)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.LicenseNumberRequired)
            .MaximumLength(50).WithMessage(LocalizationKeys.Validation.LicenseNumberTooLong);
    }
}
