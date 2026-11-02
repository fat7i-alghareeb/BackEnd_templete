using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

public class CreateCarRequest
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
