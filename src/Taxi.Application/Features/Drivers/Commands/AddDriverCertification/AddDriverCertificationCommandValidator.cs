using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Drivers.Commands.AddDriverCertification;

public sealed class AddDriverCertificationCommandValidator : AbstractValidator<AddDriverCertificationCommand>
{
    public AddDriverCertificationCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.DriverId).NotEmpty().WithMessage(LocalizationKeys.Driver.IdRequired);

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.CertificationNameRequired)
            .MaximumLength(100).WithMessage(LocalizationKeys.Validation.CertificationNameTooLong);

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.CertificationNameRequired)
            .MaximumLength(100).WithMessage(LocalizationKeys.Validation.CertificationNameTooLong);

        // Uses the injected clock, so tests can pin "today".
        RuleFor(x => x.ExpiresOn)
            .Must(expiresOn => expiresOn > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime))
            .WithMessage(LocalizationKeys.Validation.ExpiresOnInvalid);
    }
}
