using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Drivers.Commands.AssignDriverCar;

public sealed class AssignDriverCarCommandValidator : AbstractValidator<AssignDriverCarCommand>
{
    public AssignDriverCarCommandValidator()
    {
        RuleFor(x => x.DriverId).NotEmpty().WithMessage(LocalizationKeys.Driver.IdRequired);
        RuleFor(x => x.CarId).NotEmpty().WithMessage(LocalizationKeys.Validation.CarIdRequired);
    }
}
