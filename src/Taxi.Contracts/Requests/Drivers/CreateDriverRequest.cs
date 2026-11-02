using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Drivers;

public class CreateDriverRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.FullNameRequired)]
    [StringLength(200, ErrorMessage = LocalizationKeys.Validation.FullNameTooLong)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.LicenseNumberRequired)]
    [StringLength(50, ErrorMessage = LocalizationKeys.Validation.LicenseNumberTooLong)]
    public string LicenseNumber { get; set; } = string.Empty;
}
