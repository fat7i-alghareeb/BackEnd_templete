using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

namespace Taxi.Domain.Identity;

public static class RefreshTokenErrors
{
    public static readonly Error IdRequired = Error.Validation(
        code: LocalizationKeys.RefreshToken.IdRequired,
        description: "Refresh token id is required");

    public static readonly Error TokenRequired = Error.Validation(
        code: LocalizationKeys.RefreshToken.TokenRequired,
        description: "Refresh token value is required");

    public static readonly Error UserIdRequired = Error.Validation(
        code: LocalizationKeys.RefreshToken.UserIdRequired,
        description: "User id is required for refresh token");

    public static readonly Error ExpiryInvalid = Error.Validation(
        code: LocalizationKeys.RefreshToken.ExpiryInvalid,
        description: "Refresh token expiry date must be in the future");
}
