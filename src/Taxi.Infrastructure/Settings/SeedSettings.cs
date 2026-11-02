namespace Taxi.Infrastructure.Settings;

/// <summary>
/// Bound from the <c>Seed</c> configuration section and read by
/// <c>ApplicationDbContextInitialiser</c>. Nothing here has a default: a seed account is created
/// only when both its email and password are configured, and sample data only when
/// <see cref="SampleData"/> is explicitly <see langword="true"/>. Development values live in
/// <c>appsettings.Development.json</c>; every other environment supplies them through user secrets
/// or environment variables (<c>Seed__Admin__Email</c>, <c>Seed__Admin__Password</c>) or seeds nothing.
/// </summary>
public sealed class SeedSettings
{
    public const string SectionName = "Seed";

    /// <summary>Gets or sets the bootstrap account placed in the <c>Manager</c> role.</summary>
    public SeedUserSettings Admin { get; set; } = new();

    /// <summary>Gets or sets a demo account placed in the <c>User</c> role, for exercising read-only access.</summary>
    public SeedUserSettings User { get; set; } = new();

    /// <summary>Gets or sets a value indicating whether to insert demo rows (one car) into an empty database.</summary>
    public bool SampleData { get; set; }
}

public sealed class SeedUserSettings
{
    public string? Email { get; set; }

    public string? Password { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(this.Email) && !string.IsNullOrWhiteSpace(this.Password);
}
