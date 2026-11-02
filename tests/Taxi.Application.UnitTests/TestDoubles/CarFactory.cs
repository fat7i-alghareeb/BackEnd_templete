using Taxi.Domain.Cars;

namespace Taxi.Application.UnitTests.TestDoubles;

public static class CarFactory
{
    public static Car Create(string make = "Toyota", string model = "Camry", int year = 2024) =>
        Car.Create(Guid.NewGuid(), make, model, year, $"{make} {model} en", $"{make} {model} ar").Value;
}
