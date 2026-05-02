using System.ComponentModel.DataAnnotations;

using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Drivers;

public class UpdateDriverRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.EnglishNameRequired)]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.ArabicNameRequired)]
    public string NameAr { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.RequiredField)]
    public string LicenseNumber { get; set; } = string.Empty;

    public DriverState State { get; set; }
}