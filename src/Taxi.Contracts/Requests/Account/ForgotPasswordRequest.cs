using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Account;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.EmailRequired)]
    [EmailAddress(ErrorMessage = LocalizationKeys.Validation.EmailInvalid)]
    [StringLength(256, ErrorMessage = LocalizationKeys.Validation.EmailTooLong)]
    public string Email { get; set; } = string.Empty;
}
