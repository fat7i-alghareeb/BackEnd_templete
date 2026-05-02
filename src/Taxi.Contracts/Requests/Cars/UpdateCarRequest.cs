using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Cars;

public class UpdateCarRequest
{
    [Required(ErrorMessage = LocalizationKeys.Car.IdRequired)]
    public Guid Id { get; set; }

    [Required(ErrorMessage = LocalizationKeys.Validation.MakeRequired)]
    [StringLength(100)]
    public string Make { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.ModelRequired)]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.YearInvalid)]
    public int Year { get; set; }

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000)]
    public string DescriptionEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.DescriptionRequired)]
    [StringLength(1000)]
    public string DescriptionAr { get; set; } = string.Empty;
}
