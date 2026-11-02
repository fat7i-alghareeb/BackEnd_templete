namespace Taxi.Domain.Drivers;

using Taxi.Domain.Common;

/// <summary>
/// One certification a driver holds (e.g. "First aid"). Part of the <see cref="Driver"/> aggregate:
/// created and removed only through <see cref="Driver.AddCertification"/> /
/// <see cref="Driver.RemoveCertification"/>, never on its own.
/// <para>
/// <see cref="Name"/> is a <see cref="LocalizedText"/> inside an <i>owned collection</i>, which EF Core
/// cannot store as JSON (<c>OwnsMany</c> + nested <c>ToJson</c> is unsupported): it is persisted as two
/// flat columns, <c>NameEn</c> / <c>NameAr</c>, while the domain still sees one value.
/// </para>
/// </summary>
public sealed class DriverCertification
{
    private DriverCertification()
    {
    }

    private DriverCertification(Guid id, LocalizedText name, DateOnly expiresOn)
    {
        this.Id = id;
        this.Name = name;
        this.ExpiresOn = expiresOn;
    }

    public Guid Id { get; private set; }

    public LocalizedText Name { get; private set; } = null!;

    public DateOnly ExpiresOn { get; private set; }

    internal static DriverCertification Create(Guid id, LocalizedText name, DateOnly expiresOn) => new(id, name, expiresOn);
}
