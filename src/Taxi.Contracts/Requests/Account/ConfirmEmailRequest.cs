using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Account;

public class ConfirmEmailRequest
{
    [Required(ErrorMessage = LocalizationKeys.Validation.UserIdRequired)]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.Validation.TokenRequired)]
    public string Token { get; set; } = string.Empty;
}
