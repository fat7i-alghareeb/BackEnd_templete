using System.Text;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

using Taxi.Application.Common;
using Taxi.Application.Common.Errors;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

namespace Taxi.Infrastructure.Identity;

public class IdentityService(
    UserManager<AppUser> userManager,
    IUserClaimsPrincipalFactory<AppUser> userClaimsPrincipalFactory,
    IAuthorizationService authorizationService) : IIdentityService
{
    private readonly UserManager<AppUser> userManager = userManager;
    private readonly IUserClaimsPrincipalFactory<AppUser> userClaimsPrincipalFactory = userClaimsPrincipalFactory;
    private readonly IAuthorizationService authorizationService = authorizationService;

    private PasswordOptions PasswordOptions => this.userManager.Options.Password;

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await this.userManager.FindByIdAsync(userId);

        return user != null && await this.userManager.IsInRoleAsync(user, role);
    }

    public async Task<bool> AuthorizeAsync(string userId, string? policyName)
    {
        var user = await this.userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return false;
        }

        var principal = await this.userClaimsPrincipalFactory.CreateAsync(user);

        var result = await this.authorizationService.AuthorizeAsync(principal, policyName!);

        return result.Succeeded;
    }

    public async Task<Result<AppUserDto>> AuthenticateAsync(string email, string password)
    {
        // Unknown email, wrong password and locked-out account all produce the same error, so the
        // endpoint cannot be used to discover which addresses have accounts.
        var user = await this.userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return ApplicationErrors.InvalidCredentials;
        }

        if (await this.userManager.IsLockedOutAsync(user))
        {
            return ApplicationErrors.InvalidCredentials;
        }

        if (!await this.userManager.CheckPasswordAsync(user, password))
        {
            // Counts towards Identity lockout (Lockout options in AddInfrastructure).
            await this.userManager.AccessFailedAsync(user);
            return ApplicationErrors.InvalidCredentials;
        }

        await this.userManager.ResetAccessFailedCountAsync(user);

        // Only revealed to someone who already proved they know the password.
        if (!user.EmailConfirmed)
        {
            return Error.Conflict(
                LocalizationKeys.Auth.EmailNotConfirmed,
                $"Email '{UtilityService.MaskEmail(email)}' is not confirmed");
        }

        return new AppUserDto(user.Id, user.Email!, await this.userManager.GetRolesAsync(user), await this.userManager.GetClaimsAsync(user));
    }

    public async Task<Result<AppUserDto>> GetUserByIdAsync(string userId)
    {
        var user = await this.userManager.FindByIdAsync(userId);

        if (user is null)
        {
            return ApplicationErrors.UserNotFound;
        }

        var roles = await this.userManager.GetRolesAsync(user);

        var claims = await this.userManager.GetClaimsAsync(user);

        return new AppUserDto(user.Id, user.Email!, roles, claims);
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await this.userManager.FindByIdAsync(userId);

        return user?.UserName;
    }

    public async Task<IdentityUserSummary?> FindByEmailAsync(string email)
    {
        var user = await this.userManager.FindByEmailAsync(email);

        return user is null ? null : new IdentityUserSummary(user.Id, user.Email!, user.EmailConfirmed);
    }

    public async Task<Result<Success>> ValidatePasswordAsync(string password, string propertyName)
    {
        var errors = new List<Error>();

        foreach (var validator in this.userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(this.userManager, new AppUser(), password);
            if (!result.Succeeded)
            {
                errors.AddRange(result.ToErrors(propertyName, this.PasswordOptions));
            }
        }

        return errors.Count == 0 ? Result.Success : errors;
    }

    public async Task<Result<string>> CreateUserAsync(string email, string password, string role)
    {
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = false };

        var created = await this.userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return created.ToErrors("Password", this.PasswordOptions);
        }

        var addedToRole = await this.userManager.AddToRoleAsync(user, role);
        if (!addedToRole.Succeeded)
        {
            return addedToRole.ToErrors("Password", this.PasswordOptions);
        }

        return user.Id;
    }

    public async Task<Result<string>> GenerateEmailConfirmationTokenAsync(string userId)
    {
        var user = await this.userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ApplicationErrors.UserNotFound;
        }

        return Encode(await this.userManager.GenerateEmailConfirmationTokenAsync(user));
    }

    public async Task<Result<Success>> ConfirmEmailAsync(string userId, string token)
    {
        var user = await this.userManager.FindByIdAsync(userId);

        // An unknown user and a bad token are indistinguishable to the caller.
        if (user is null || Decode(token) is not { } decoded)
        {
            return ApplicationErrors.AccountTokenInvalid;
        }

        var result = await this.userManager.ConfirmEmailAsync(user, decoded);

        return result.Succeeded ? Result.Success : result.ToErrors("Password", this.PasswordOptions);
    }

    public async Task<Result<string>> GeneratePasswordResetTokenAsync(string userId)
    {
        var user = await this.userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ApplicationErrors.UserNotFound;
        }

        return Encode(await this.userManager.GeneratePasswordResetTokenAsync(user));
    }

    public async Task<Result<string>> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await this.userManager.FindByEmailAsync(email);

        if (user is null || Decode(token) is not { } decoded)
        {
            return ApplicationErrors.AccountTokenInvalid;
        }

        var result = await this.userManager.ResetPasswordAsync(user, decoded, newPassword);
        if (!result.Succeeded)
        {
            return result.ToErrors("NewPassword", this.PasswordOptions);
        }

        // A successful reset proves control of the mailbox; clear any brute-force lockout too.
        await this.userManager.SetLockoutEndDateAsync(user, null);
        await this.userManager.ResetAccessFailedCountAsync(user);

        return user.Id;
    }

    public async Task<Result<Success>> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await this.userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ApplicationErrors.UserNotFound;
        }

        var result = await this.userManager.ChangePasswordAsync(user, currentPassword, newPassword);

        return result.Succeeded ? Result.Success : result.ToErrors("NewPassword", this.PasswordOptions);
    }

    public async Task<Result<Updated>> SetRolesAsync(string userId, IReadOnlyCollection<string> roles)
    {
        var user = await this.userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return ApplicationErrors.UserNotFound;
        }

        var current = await this.userManager.GetRolesAsync(user);

        var removed = await this.userManager.RemoveFromRolesAsync(user, current.Except(roles));
        if (!removed.Succeeded)
        {
            return removed.ToErrors("Roles", this.PasswordOptions);
        }

        var added = await this.userManager.AddToRolesAsync(user, roles.Except(current));
        if (!added.Succeeded)
        {
            return added.ToErrors("Roles", this.PasswordOptions);
        }

        return Result.Updated;
    }

    /// <summary>Identity tokens contain '+', '/' and '='; they travel in URLs, so Base64Url-encode them.</summary>
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string? Decode(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
