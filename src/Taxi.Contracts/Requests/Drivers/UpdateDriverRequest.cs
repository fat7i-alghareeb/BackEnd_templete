using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Drivers;

/// <summary>Body of <c>PUT /api/v1/drivers/{id}</c>; the id comes from the route only.</summary>
public class UpdateDriverRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.FullNameRequired)]
    [StringLength(200, ErrorMessage = LocalizationKeys.Validation.FullNameTooLong)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.LicenseNumberRequired)]
    [StringLength(50, ErrorMessage = LocalizationKeys.Validation.LicenseNumberTooLong)]
    public string LicenseNumber { get; set; } = string.Empty;
}
