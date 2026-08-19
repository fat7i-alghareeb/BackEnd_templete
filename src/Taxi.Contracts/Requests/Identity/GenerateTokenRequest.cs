using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Identity;

public class GenerateTokenRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.EmailRequired)]
    [EmailAddress(ErrorMessage = LocalizationKeys.Validation.EmailInvalid)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.PasswordRequired)]
    public string Password { get; set; } = string.Empty;
}
