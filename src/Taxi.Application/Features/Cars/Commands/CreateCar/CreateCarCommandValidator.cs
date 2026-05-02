using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Cars.Commands.CreateCar;

public sealed class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
{
    public CreateCarCommandValidator()
    {
        RuleFor(x => x.Make)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.MakeRequired)
            .MaximumLength(100);

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.ModelRequired)
            .MaximumLength(100);

        RuleFor(x => x.Year)
            .InclusiveBetween(1886, DateTime.UtcNow.Year + 2).WithMessage(LocalizationKeys.Validation.YearInvalid);

        RuleFor(x => x.DescriptionEn)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.DescriptionRequired)
            .MaximumLength(1000);

        RuleFor(x => x.DescriptionAr)
            .NotEmpty().WithMessage(LocalizationKeys.Validation.DescriptionRequired)
            .MaximumLength(1000);
    }
}
