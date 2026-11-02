namespace Taxi.Application.Features.Drivers.Mappers;

using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Contracts.Common;
using Taxi.Domain.Drivers;

public static class DriverMapper
{
    public static DriverDto ToDto(this Driver driver, string language = Languages.Default) =>
        new(
            driver.Id,
            driver.FullName,
            driver.LicenseNumber,
            driver.AssignedCarId,
            driver.Certifications
                .OrderBy(c => c.ExpiresOn)
                .Select(c => c.ToDto(language))
                .ToList());

    public static DriverCertificationDto ToDto(this DriverCertification certification, string language = Languages.Default) =>
        new(
            certification.Id,
            language == Languages.Ar ? certification.Name.Ar : certification.Name.En,
            certification.ExpiresOn);
}
