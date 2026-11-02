using FluentValidation;
using Taxi.Contracts.Common;

namespace Taxi.Application.Features.Drivers.Queries.GetDrivers;

public sealed class GetDriversQueryValidator : AbstractValidator<GetDriversQuery>
{
    public GetDriversQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageInvalid);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageSizeInvalid);
        RuleFor(x => x.Search).MaximumLength(100).WithMessage(LocalizationKeys.Validation.SearchTooLong);
    }
}
