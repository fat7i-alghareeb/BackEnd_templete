using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Cars.Queries.GetCars;

public sealed class GetCarsQueryValidator : AbstractValidator<GetCarsQuery>
{
    public GetCarsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageInvalid);

        // No upper bound: anything above Paging.MaxPageSize is clamped, not rejected.
        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageSizeInvalid);
    }
}
