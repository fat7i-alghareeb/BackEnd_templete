using System.ComponentModel.DataAnnotations;
using Taxi.Contracts.Common;

namespace Taxi.Contracts.Requests.Identity;

public class RefreshTokenRequest
{
    [Required(ErrorMessage = LocalizationKeys.Auth.ExpiredAccessTokenInvalid)]
    public string ExpiredAccessToken { get; set; } = string.Empty;

    [Required(ErrorMessage = LocalizationKeys.RefreshToken.TokenRequired)]
    public string RefreshToken { get; set; } = string.Empty;
}
