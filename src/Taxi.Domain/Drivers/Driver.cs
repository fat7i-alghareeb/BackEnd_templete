namespace Taxi.Domain.Drivers;

using Taxi.Domain.Common;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers.Events;

/// <summary>
/// The second reference aggregate. Where <c>Car</c> is the minimal shape, <c>Driver</c> shows the
/// three patterns <c>Car</c> lacks:
/// <list type="bullet">
/// <item>a <b>child collection</b> (<see cref="Certifications"/>) owned by the aggregate and changed
/// only through its methods;</item>
/// <item>a <b>relationship</b> to another aggregate, held by id only (<see cref="AssignedCarId"/>) —
/// never a navigation to <c>Car</c>;</item>
/// <item>a <b>domain event</b> (<see cref="DriverAssignedToCarDomainEvent"/>).</item>
/// </list>
/// Rules that need another aggregate (the car exists, nobody else drives it) belong to the handler,
/// which can query; rules about the driver's own state live here.
/// </summary>
public sealed class Driver : AuditableEntity
{
    public const int MaxCertifications = 10;

    private readonly List<DriverCertification> certifications = [];

    private Driver()
    {
    }

    private Driver(Guid id, string fullName, string licenseNumber)
        : base(id)
    {
        this.FullName = fullName;
        this.LicenseNumber = licenseNumber;
    }

    public string FullName { get; private set; } = null!;

    /// <summary>Gets the driving-licence number, normalised to upper case; unique across drivers.</summary>
    public string LicenseNumber { get; private set; } = null!;

    /// <summary>Gets the car this driver is assigned to — an id, not a navigation: aggregates reference each other by id.</summary>
    public Guid? AssignedCarId { get; private set; }

    public IReadOnlyCollection<DriverCertification> Certifications => this.certifications.AsReadOnly();

    public static Result<Driver> Create(Guid id, string fullName, string licenseNumber)
    {
        if (id == Guid.Empty)
        {
            return DriverErrors.IdRequired;
        }

        if (Validate(fullName, licenseNumber) is { } error)
        {
            return error;
        }

        return new Driver(id, fullName.Trim(), Normalize(licenseNumber));
    }

    public Result<Updated> Update(string fullName, string licenseNumber)
    {
        if (Validate(fullName, licenseNumber) is { } error)
        {
            return error;
        }

        this.FullName = fullName.Trim();
        this.LicenseNumber = Normalize(licenseNumber);

        return Result.Updated;
    }

    public Result<DriverCertification> AddCertification(Guid id, string nameEn, string nameAr, DateOnly expiresOn)
    {
        if (string.IsNullOrWhiteSpace(nameEn) || string.IsNullOrWhiteSpace(nameAr))
        {
            return DriverErrors.CertificationNameRequired;
        }

        if (this.certifications.Count >= MaxCertifications)
        {
            return DriverErrors.TooManyCertifications(MaxCertifications);
        }

        var name = new LocalizedText(nameEn.Trim(), nameAr.Trim());

        if (this.certifications.Any(c => string.Equals(c.Name.En, name.En, StringComparison.OrdinalIgnoreCase)))
        {
            return DriverErrors.CertificationAlreadyHeld;
        }

        var certification = DriverCertification.Create(id == Guid.Empty ? Guid.NewGuid() : id, name, expiresOn);
        this.certifications.Add(certification);

        return certification;
    }

    public Result<Deleted> RemoveCertification(Guid certificationId)
    {
        var certification = this.certifications.Find(c => c.Id == certificationId);
        if (certification is null)
        {
            return DriverErrors.CertificationNotFound;
        }

        this.certifications.Remove(certification);

        return Result.Deleted;
    }

    /// <summary>
    /// Puts the driver behind the wheel of <paramref name="carId"/>. The caller must already have
    /// checked that the car exists and that no other driver holds it — those need other aggregates.
    /// </summary>
    public Result<Updated> AssignCar(Guid carId)
    {
        if (this.AssignedCarId == carId)
        {
            return DriverErrors.AlreadyAssignedToCar;
        }

        var previous = this.AssignedCarId;
        this.AssignedCarId = carId;
        this.AddDomainEvent(new DriverAssignedToCarDomainEvent(this.Id, carId, previous));

        return Result.Updated;
    }

    public Result<Updated> UnassignCar()
    {
        if (this.AssignedCarId is null)
        {
            return DriverErrors.NotAssigned;
        }

        this.AssignedCarId = null;

        return Result.Updated;
    }

    /// <summary>Shared by Create and Update; runs before any assignment so a rejected update changes nothing.</summary>
    private static Error? Validate(string fullName, string licenseNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return DriverErrors.FullNameRequired;
        }

        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            return DriverErrors.LicenseNumberRequired;
        }

        return null;
    }

    private static string Normalize(string licenseNumber) => licenseNumber.Trim().ToUpperInvariant();
}
