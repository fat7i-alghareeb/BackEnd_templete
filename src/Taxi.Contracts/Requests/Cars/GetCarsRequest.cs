using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

/// <summary>
/// Query string of <c>GET /api/v1/cars</c>, bound with <c>[FromQuery]</c>.
/// <para>
/// Paging: a <see cref="PageSize"/> above 100 is clamped to 100.
/// Filtering: <see cref="Search"/> matches make or model (contains, case-insensitive);
/// <see cref="Make"/> is an exact, case-insensitive match; <see cref="Year"/> is exact.
/// Sorting: <see cref="SortBy"/> is one of <see cref="CarSortFields.All"/> (default
/// <c>createdAt</c>), <see cref="SortDirection"/> one of <see cref="SortDirections.All"/>
/// (default <c>desc</c>).
/// </para>
/// </summary>
public class GetCarsRequest
{
    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageInvalid)]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageSizeInvalid)]
    public int PageSize { get; set; } = 20;

    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.SearchTooLong)]
    public string? Search { get; set; }

    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.SearchTooLong)]
    public string? Make { get; set; }

    public int? Year { get; set; }

    public string? SortBy { get; set; }

    public string? SortDirection { get; set; }
}
