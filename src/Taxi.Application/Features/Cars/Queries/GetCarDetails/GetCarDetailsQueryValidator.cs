using FluentValidation;
using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;

namespace Taxi.Application.Features.Cars.Queries.GetCarDetails;

public sealed class GetCarDetailsQueryValidator : AbstractValidator<GetCarDetailsQuery>
{
    public GetCarDetailsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageInvalid);
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(1).WithMessage(LocalizationKeys.Validation.PageSizeInvalid);
        RuleFor(x => x.Search).MaximumLength(100).WithMessage(LocalizationKeys.Validation.SearchTooLong);
        RuleFor(x => x.Make).MaximumLength(100).WithMessage(LocalizationKeys.Validation.SearchTooLong);

        RuleFor(x => x.SortBy)
            .Must(v => CarSortFields.All.Contains(v!.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.SortBy))
            .WithMessage(LocalizationKeys.Validation.SortByInvalid);

        RuleFor(x => x.SortDirection)
            .Must(v => SortDirections.All.Contains(v!.Trim(), StringComparer.OrdinalIgnoreCase))
            .When(x => !string.IsNullOrWhiteSpace(x.SortDirection))
            .WithMessage(LocalizationKeys.Validation.SortDirectionInvalid);
    }
}
