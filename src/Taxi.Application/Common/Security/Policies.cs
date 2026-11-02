namespace Taxi.Application.Common.Security;

/// <summary>
/// Named authorization policies. Endpoints reference a policy, never a role, so the mapping from
/// role to capability lives in one place (<c>AddAppAuthorization</c> in the API project).
/// Use with <c>[Authorize(Policy = Policies.X)]</c> or <c>IIdentityService.AuthorizeAsync</c>.
/// </summary>
public static class Policies
{
    /// <summary>Read cars. Granted to <see cref="Roles.Manager"/> and <see cref="Roles.User"/>.</summary>
    public const string CanReadCars = nameof(CanReadCars);

    /// <summary>Create, update and delete cars. Granted to <see cref="Roles.Manager"/>.</summary>
    public const string CanManageCars = nameof(CanManageCars);

    /// <summary>Read drivers. Granted to <see cref="Roles.Manager"/> and <see cref="Roles.User"/>.</summary>
    public const string CanReadDrivers = nameof(CanReadDrivers);

    /// <summary>Create, update, delete and assign drivers. Granted to <see cref="Roles.Manager"/>.</summary>
    public const string CanManageDrivers = nameof(CanManageDrivers);

    /// <summary>Administer accounts (assign roles). Granted to <see cref="Roles.Manager"/>.</summary>
    public const string CanManageUsers = nameof(CanManageUsers);
}
