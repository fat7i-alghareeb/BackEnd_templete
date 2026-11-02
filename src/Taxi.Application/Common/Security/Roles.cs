namespace Taxi.Application.Common.Security;

/// <summary>
/// The Identity roles the system knows about. Seeded on startup and referenced by
/// <see cref="Policies"/>; never write a role name inline.
/// </summary>
public static class Roles
{
    /// <summary>Back-office staff: full read/write over cars and user administration.</summary>
    public const string Manager = "Manager";

    /// <summary>Default role for self-registered accounts: read-only access.</summary>
    public const string User = "User";

    public static readonly string[] All = [Manager, User];
}
