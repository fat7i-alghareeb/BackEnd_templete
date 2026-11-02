using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Drivers;

/// <summary>Query string of <c>GET /api/v1/drivers</c>. <see cref="Search"/> matches name or licence number.</summary>
public class GetDriversRequest
{
    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageInvalid)]
    public int Page { get; set; } = 1;

    [Range(1, int.MaxValue, ErrorMessage = LocalizationKeys.Validation.PageSizeInvalid)]
    public int PageSize { get; set; } = 20;

    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.SearchTooLong)]
    public string? Search { get; set; }
}
