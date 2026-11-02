using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

using NSubstitute;

using Taxi.Application.Common.Caching;
using Taxi.Application.Features.Cars.Commands.RemoveCar;
using Taxi.Application.Features.Drivers.Commands.AddDriverCertification;
using Taxi.Application.Features.Drivers.Commands.AssignDriverCar;
using Taxi.Application.Features.Drivers.Commands.CreateDriver;
using Taxi.Application.UnitTests.TestDoubles;
using Taxi.Contracts.Common;
using Taxi.Domain.Drivers;

using Xunit;

namespace Taxi.Application.UnitTests.Features.Drivers;

public class DriverHandlerTests
{
    private readonly TestAppDbContext context = TestAppDbContext.Create();
    private readonly HybridCache cache = Substitute.For<HybridCache>();

    [Fact]
    public async Task CreateDriver_RejectsADuplicateLicence_AfterNormalisation()
    {
        await this.SeedAsync(Driver.Create(Guid.NewGuid(), "A", "AB-1").Value);
        var handler = new CreateDriverCommandHandler(this.context, this.cache, new FakeLanguageContext());

        var result = await handler.Handle(new CreateDriverCommand("B", " ab-1 "), default);

        Assert.Equal(LocalizationKeys.Driver.LicenseNumberTaken, result.TopError.Code);
    }

    [Fact]
    public async Task AssignCar_ReturnsCarNotFound_ForAnUnknownCar()
    {
        var driver = await this.SeedDriverAsync();

        var result = await this.Assign(driver.Id, Guid.NewGuid());

        Assert.Equal(LocalizationKeys.Car.NotFound, result.TopError.Code);
    }

    [Fact]
    public async Task AssignCar_RefusesACarAnotherDriverHolds()
    {
        var car = CarFactory.Create();
        var holder = Driver.Create(Guid.NewGuid(), "Holder", "H1").Value;
        holder.AssignCar(car.Id);
        this.context.Cars.Add(car);
        await this.SeedAsync(holder);
        var driver = await this.SeedDriverAsync();

        var result = await this.Assign(driver.Id, car.Id);

        Assert.Equal(LocalizationKeys.Driver.CarTaken, result.TopError.Code);
    }

    [Fact]
    public async Task AssignCar_Assigns_AndEvictsTheDriversCache()
    {
        var car = CarFactory.Create();
        this.context.Cars.Add(car);
        var driver = await this.SeedDriverAsync();

        var result = await this.Assign(driver.Id, car.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(car.Id, (await this.context.Drivers.AsNoTracking().SingleAsync()).AssignedCarId);
        await this.cache.Received(1).RemoveByTagAsync(CacheTags.Drivers, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCar_IsRefused_WhileADriverIsAssigned()
    {
        var car = CarFactory.Create();
        var driver = Driver.Create(Guid.NewGuid(), "D", "D1").Value;
        driver.AssignCar(car.Id);
        this.context.Cars.Add(car);
        await this.SeedAsync(driver);

        var result = await new RemoveCarCommandHandler(this.context, this.cache).Handle(new RemoveCarCommand(car.Id), default);

        Assert.Equal(LocalizationKeys.Car.AssignedToDriver, result.TopError.Code);
        Assert.Single(this.context.Cars);
    }

    [Fact]
    public void AddCertificationValidator_UsesTheInjectedClock()
    {
        var validator = new AddDriverCertificationCommandValidator(new FixedTimeProvider(new DateTimeOffset(2030, 6, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.False(validator.Validate(new AddDriverCertificationCommand(Guid.NewGuid(), "A", "B", new DateOnly(2030, 6, 1))).IsValid);
        Assert.True(validator.Validate(new AddDriverCertificationCommand(Guid.NewGuid(), "A", "B", new DateOnly(2030, 6, 2))).IsValid);
    }

    private Task<Domain.Common.Results.Result<Domain.Common.Results.Updated>> Assign(Guid driverId, Guid carId) =>
        new AssignDriverCarCommandHandler(this.context, this.cache).Handle(new AssignDriverCarCommand(driverId, carId), default);

    private async Task<Driver> SeedDriverAsync()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Sami", $"L{Guid.NewGuid():N}"[..10]).Value;
        await this.SeedAsync(driver);
        return driver;
    }

    private async Task SeedAsync(Driver driver)
    {
        this.context.Drivers.Add(driver);
        await this.context.SaveChangesAsync(default);
        this.context.ChangeTracker.Clear();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
