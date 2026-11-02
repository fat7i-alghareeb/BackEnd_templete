using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

/// <summary>
/// Query string of <c>GET /api/v1/cars</c>, bound with <c>[FromQuery]</c>.
/// A <see cref="PageSize"/> above 100 is clamped to 100.
/// </summary>
public class GetCarsRequest
{
    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageInvalid)]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageSizeInvalid)]
    public int PageSize { get; set; } = 20;
}
