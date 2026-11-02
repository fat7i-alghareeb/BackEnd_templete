using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Drivers;

public class AddDriverCertificationRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.CertificationNameRequired)]
    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.CertificationNameTooLong)]
    public string NameEn { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.CertificationNameRequired)]
    [StringLength(100, ErrorMessage = LocalizationKeys.Validation.CertificationNameTooLong)]
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Gets or sets the expiry date (<c>yyyy-MM-dd</c>); must be in the future.</summary>
    [Required(ErrorMessage = LocalizationKeys.Validation.ExpiresOnInvalid)]
    public DateOnly ExpiresOn { get; set; }
}
