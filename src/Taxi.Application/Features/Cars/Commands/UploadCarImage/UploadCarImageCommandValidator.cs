using FluentValidation;
using Taxi.Application.Common.Files;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Cars.Commands.UploadCarImage;

public sealed class UploadCarImageCommandValidator : AbstractValidator<UploadCarImageCommand>
{
    public UploadCarImageCommandValidator()
    {
        RuleFor(x => x.CarId).NotEmpty().WithMessage(LocalizationKeys.Car.IdRequired);

        RuleFor(x => x.Length)
            .GreaterThan(0).WithMessage(LocalizationKeys.Validation.FileRequired)
            .LessThanOrEqualTo(ImageFormat.MaxBytes).WithMessage(LocalizationKeys.Validation.FileTooLarge);
    }
}
