using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
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
        var user = await this.userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Error.NotFound("User_Not_Found", $"User with email {UtilityService.MaskEmail(email)} not found");
        }

        if (!user.EmailConfirmed)
        {
            return Error.Conflict("Email_Not_Confirmed", $"email '{UtilityService.MaskEmail(email)}' not confirmed");
        }

        if (!await this.userManager.CheckPasswordAsync(user, password))
        {
            return Error.Conflict("Invalid_Login_Attempt", "Email / Password are incorrect");
        }

        return new AppUserDto(user.Id, user.Email!, await this.userManager.GetRolesAsync(user), await this.userManager.GetClaimsAsync(user));
    }

    public async Task<Result<AppUserDto>> GetUserByIdAsync(string userId)
    {
        var user = await this.userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException(nameof(userId));

        var roles = await this.userManager.GetRolesAsync(user);

        var claims = await this.userManager.GetClaimsAsync(user);

        return new AppUserDto(user.Id, user.Email!, roles, claims);
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await this.userManager.FindByIdAsync(userId);

        return user?.UserName;
    }
}