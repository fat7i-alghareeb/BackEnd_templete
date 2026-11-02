using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

namespace Taxi.Domain.Drivers;

public static class DriverErrors
{
    public static readonly Error IdRequired = Error.Validation(
        code: LocalizationKeys.Driver.IdRequired,
        description: "Driver ID is required.");

    public static readonly Error FullNameRequired = Error.Validation(
        code: LocalizationKeys.Driver.FullNameRequired,
        description: "Driver full name is required.");

    public static readonly Error LicenseNumberRequired = Error.Validation(
        code: LocalizationKeys.Driver.LicenseNumberRequired,
        description: "Driver licence number is required.");

    public static readonly Error CertificationNameRequired = Error.Validation(
        code: LocalizationKeys.Driver.CertificationNameRequired,
        description: "Certification name is required in both languages.");

    public static readonly Error CertificationAlreadyHeld = Error.Conflict(
        code: LocalizationKeys.Driver.CertificationAlreadyHeld,
        description: "The driver already holds this certification.");

    public static readonly Error CertificationNotFound = Error.NotFound(
        code: LocalizationKeys.Driver.CertificationNotFound,
        description: "Certification was not found.");

    public static readonly Error AlreadyAssignedToCar = Error.Conflict(
        code: LocalizationKeys.Driver.AlreadyAssignedToCar,
        description: "The driver is already assigned to this car.");

    public static readonly Error NotAssigned = Error.Conflict(
        code: LocalizationKeys.Driver.NotAssigned,
        description: "The driver is not assigned to a car.");

    public static readonly Error CarTaken = Error.Conflict(
        code: LocalizationKeys.Driver.CarTaken,
        description: "Another driver is already assigned to this car.");

    public static readonly Error LicenseNumberTaken = Error.Conflict(
        code: LocalizationKeys.Driver.LicenseNumberTaken,
        description: "Another driver already has this licence number.");

    public static readonly Error NotFound = Error.NotFound(
        code: LocalizationKeys.Driver.NotFound,
        description: "Driver was not found.");

    public static Error TooManyCertifications(int max) => Error.Validation(
        code: LocalizationKeys.Driver.TooManyCertifications,
        description: $"A driver can hold at most {max} certifications.",
        args: max);
}
