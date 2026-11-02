using Taxi.Application.Features.Identity.Dtos;
using Taxi.Domain.Common.Results;

namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// Everything the Application layer needs from ASP.NET Identity, without seeing an Identity type.
/// Account tokens (email confirmation, password reset) cross this boundary already URL-safe
/// encoded; the implementation decodes them again.
/// </summary>
public interface IIdentityService
{
    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string? policyName);

    Task<Result<AppUserDto>> AuthenticateAsync(string email, string password);

    Task<Result<AppUserDto>> GetUserByIdAsync(string userId);

    Task<string?> GetUserNameAsync(string userId);

    Task<IdentityUserSummary?> FindByEmailAsync(string email);

    /// <summary>Checks a password against the configured policy without creating anything.</summary>
    /// <returns>Success, or validation errors keyed by <paramref name="propertyName"/>.</returns>
    Task<Result<Success>> ValidatePasswordAsync(string password, string propertyName);

    /// <summary>Creates an unconfirmed account in <paramref name="role"/>.</summary>
    /// <returns>The new user id.</returns>
    Task<Result<string>> CreateUserAsync(string email, string password, string role);

    Task<Result<string>> GenerateEmailConfirmationTokenAsync(string userId);

    Task<Result<Success>> ConfirmEmailAsync(string userId, string token);

    Task<Result<string>> GeneratePasswordResetTokenAsync(string userId);

    /// <returns>The id of the user whose password was reset.</returns>
    Task<Result<string>> ResetPasswordAsync(string email, string token, string newPassword);

    Task<Result<Success>> ChangePasswordAsync(string userId, string currentPassword, string newPassword);

    /// <summary>Replaces the user's roles with exactly <paramref name="roles"/>.</summary>
    Task<Result<Updated>> SetRolesAsync(string userId, IReadOnlyCollection<string> roles);
}
