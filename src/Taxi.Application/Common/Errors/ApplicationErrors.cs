namespace Taxi.Application.Common.Errors;

using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

public static class ApplicationErrors
{
    public static Error CarNotFound => Error.NotFound(
           LocalizationKeys.Car.NotFound,
           "Car does not exist.");

    public static Error InvalidRefreshToken => Error.Validation(
        LocalizationKeys.RefreshToken.ExpiryInvalid,
        "Expiry must be in the future.");

    public static Error ExpiredAccessTokenInvalid => Error.Conflict(
         code: LocalizationKeys.Auth.ExpiredAccessTokenInvalid,
         description: "Expired access token is not valid.");

    public static Error UserIdClaimInvalid => Error.Conflict(
        code: LocalizationKeys.Auth.UserIdClaimInvalid,
        description: "Invalid userId claim.");

    public static Error RefreshTokenExpired => Error.Conflict(
        code: LocalizationKeys.Auth.RefreshTokenExpired,
        description: "Refresh token is invalid or has expired.");

    public static Error UserNotFound => Error.NotFound(
        code: LocalizationKeys.Auth.UserNotFound,
        description: "User not found.");

    public static Error TokenGenerationFailed => Error.Failure(
        code: LocalizationKeys.Auth.TokenGenerationFailed,
        description: "Failed to generate new JWT token.");
}