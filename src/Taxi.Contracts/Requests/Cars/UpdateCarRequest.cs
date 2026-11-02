using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

/// <summary>
/// Body of <c>PUT /api/v1/cars/{id}</c>. Deliberately has no <c>Id</c>: the route is the single
/// source of the resource identity, so there is no route/body mismatch to check.
/// </summary>
public class UpdateCarRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]
    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.MakeTooLong)]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.ModelRequired)]
    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.ModelTooLong)]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.YearInvalid)]
    public int Year { get; set; }

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000, ErrorMessage = LocalizationKeys.Validation.DescriptionTooLong)]
    public string DescriptionEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000, ErrorMessage = LocalizationKeys.Validation.DescriptionTooLong)]
    public string DescriptionAr { get; set; } = string.Empty;
}
