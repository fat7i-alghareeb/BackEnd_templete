using FluentValidation;
using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;

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

        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage(LocalizationKeys.Validation.SearchTooLong);

        RuleFor(x => x.Make)
            .MaximumLength(100).WithMessage(LocalizationKeys.Validation.SearchTooLong);

        RuleFor(x => x.SortBy)
            .Must(BeOneOf(CarSortFields.All))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage(LocalizationKeys.Validation.SortByInvalid);

        RuleFor(x => x.SortDirection)
            .Must(BeOneOf(SortDirections.All))
            .When(x => !string.IsNullOrWhiteSpace(x.SortDirection))
            .WithMessage(LocalizationKeys.Validation.SortDirectionInvalid);
    }

    private static Func<string?, bool> BeOneOf(string[] allowed) =>
        value => allowed.Contains(value!.Trim(), StringComparer.OrdinalIgnoreCase);
}
