using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

namespace Taxi.Domain.Cars;

public static class CarErrors
{
    public static readonly Error IdRequired = Error.Validation(
        code: LocalizationKeys.Car.IdRequired,
        description: "Car ID is required.");

    public static readonly Error MakeRequired = Error.Validation(
        code: LocalizationKeys.Car.MakeRequired,
        description: "Car make is required.");

    public static readonly Error ModelRequired = Error.Validation(
        code: LocalizationKeys.Car.ModelRequired,
        description: "Car model is required.");

    public static readonly Error DescriptionRequired = Error.Validation(
        code: LocalizationKeys.Car.DescriptionRequired,
        description: "Car description (both English and Arabic) is required.");

    public static readonly Error InvalidYear = Error.Validation(
        code: LocalizationKeys.Car.InvalidYear,
        description: "Car year is invalid.");

    public static readonly Error NotFound = Error.NotFound(
        code: LocalizationKeys.Car.NotFound,
        description: "Car was not found.");
}
