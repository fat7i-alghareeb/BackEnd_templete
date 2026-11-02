using Microsoft.AspNetCore.Identity;

using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

namespace Taxi.Infrastructure.Identity;

/// <summary>
/// Translates an <see cref="IdentityResult"/> into <see cref="Error"/>s with localization keys, so no
/// Identity type or English Identity message reaches a response. Password-policy failures become
/// validation errors keyed by the offending request property.
/// </summary>
internal static class IdentityErrorMapper
{
    public static List<Error> ToErrors(this IdentityResult result, string passwordProperty, PasswordOptions passwordOptions) =>
        result.Errors.Select(e => Map(e, passwordProperty, passwordOptions)).ToList();

    private static Error Map(IdentityError error, string passwordProperty, PasswordOptions passwordOptions) => error.Code switch
    {
        nameof(IdentityErrorDescriber.PasswordTooShort) =>
            Error.ValidationForProperty(passwordProperty, LocalizationKeys.Validation.PasswordTooShort, passwordOptions.RequiredLength),
        nameof(IdentityErrorDescriber.PasswordRequiresDigit) =>
            Error.ValidationForProperty(passwordProperty, LocalizationKeys.Validation.PasswordRequiresDigit),
        nameof(IdentityErrorDescriber.PasswordRequiresLower) =>
            Error.ValidationForProperty(passwordProperty, LocalizationKeys.Validation.PasswordRequiresLower),
        nameof(IdentityErrorDescriber.PasswordRequiresUpper) =>
            Error.ValidationForProperty(passwordProperty, LocalizationKeys.Validation.PasswordRequiresUpper),
        nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric) =>
            Error.ValidationForProperty(passwordProperty, LocalizationKeys.Validation.PasswordRequiresNonAlphanumeric),
        nameof(IdentityErrorDescriber.PasswordRequiresUniqueChars) =>
            Error.ValidationForProperty(passwordProperty, LocalizationKeys.Validation.PasswordRequiresUniqueChars, passwordOptions.RequiredUniqueChars),
        nameof(IdentityErrorDescriber.PasswordMismatch) =>
            Error.ValidationForProperty("CurrentPassword", LocalizationKeys.Account.CurrentPasswordInvalid),
        nameof(IdentityErrorDescriber.InvalidToken) =>
            Error.ValidationForProperty("Token", LocalizationKeys.Account.TokenInvalid),
        _ => Error.Validation(LocalizationKeys.Account.IdentityFailed, error.Description),
    };
}
