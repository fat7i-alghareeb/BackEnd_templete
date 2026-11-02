namespace Taxi.Application.Features.Cars.Mappers;

using Taxi.Application.Features.Cars.Dtos;
using Taxi.Contracts.Common;
using Taxi.Domain.Cars;

public static class CarMapper
{
    public static CarDto ToDto(this Car car, string language = Languages.Default)
    {
        return new CarDto(
            car.Id,
            car.Make,
            car.Model,
            car.Year,
            language == Languages.Ar ? car.Description.Ar : car.Description.En,
            car.ImageKey is not null);
    }

    public static List<CarDto> ToDto(this IEnumerable<Car> cars, string language = Languages.Default)
    {
        return cars.Select(c => c.ToDto(language)).ToList();
    }

    /// <summary>v2 shape: both languages, audit timestamps. No language parameter — nothing is resolved.</summary>
    /// <returns>The details DTO.</returns>
    public static CarDetailsDto ToDetailsDto(this Car car)
    {
        return new CarDetailsDto(
            car.Id,
            car.Make,
            car.Model,
            car.Year,
            new LocalizedTextDto(car.Description.En, car.Description.Ar),
            car.ImageKey is not null,
            car.CreatedAtUtc,
            car.LastModifiedUtc);
    }
}
