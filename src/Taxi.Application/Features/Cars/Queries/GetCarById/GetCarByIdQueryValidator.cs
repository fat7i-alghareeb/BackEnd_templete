using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Cars.Queries.GetCarById;

public sealed class GetCarByIdQueryValidator : AbstractValidator<GetCarByIdQuery>
{
    public GetCarByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage(LocalizationKeys.Car.IdRequired);
    }
}
