using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Account;

public class ChangePasswordRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.CurrentPasswordRequired)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.NewPasswordRequired)]
    public string NewPassword { get; set; } = string.Empty;
}
