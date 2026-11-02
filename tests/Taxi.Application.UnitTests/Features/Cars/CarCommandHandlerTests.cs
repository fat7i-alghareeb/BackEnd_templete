using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

using NSubstitute;

using Taxi.Application.Common.Caching;
using Taxi.Application.Features.Cars.Commands.CreateCar;
using Taxi.Application.Features.Cars.Commands.RemoveCar;
using Taxi.Application.Features.Cars.Commands.UpdateCar;
using Taxi.Application.UnitTests.TestDoubles;
using Taxi.Contracts.Common;
using Taxi.Domain.Cars.Events;
using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Application.UnitTests.Features.Cars;

public class CarCommandHandlerTests
{
    private readonly TestAppDbContext context = TestAppDbContext.Create();
    private readonly HybridCache cache = Substitute.For<HybridCache>();

    [Fact]
    public async Task CreateCar_PersistsTheCar_AndReturnsItInTheRequestLanguage()
    {
        var handler = new CreateCarCommandHandler(this.context, this.cache, new FakeLanguageContext(Languages.Ar));

        var result = await handler.Handle(new CreateCarCommand("Tesla", "Model 3", 2024, "Electric.", "كهربائية."), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("كهربائية.", result.Value.Description);
        var stored = await this.context.Cars.SingleAsync();
        Assert.Equal(result.Value.Id, stored.Id);
        Assert.Equal("Electric.", stored.Description.En);
    }

    [Fact]
    public async Task CreateCar_RaisesCarCreatedDomainEvent()
    {
        var handler = new CreateCarCommandHandler(this.context, this.cache, new FakeLanguageContext());

        await handler.Handle(new CreateCarCommand("Tesla", "Model 3", 2024, "en", "ar"), default);

        // The outbox interceptor (Infrastructure) would drain this list on save; the InMemory context keeps it.
        var car = await this.context.Cars.SingleAsync();
        var raised = Assert.IsType<CarCreatedDomainEvent>(Assert.Single(car.DomainEvents));
        Assert.Equal(car.Id, raised.CarId);
    }

    [Fact]
    public async Task CreateCar_EvictsTheCarsCacheTag()
    {
        var handler = new CreateCarCommandHandler(this.context, this.cache, new FakeLanguageContext());

        await handler.Handle(new CreateCarCommand("Tesla", "Model 3", 2024, "en", "ar"), default);

        await this.cache.Received(1).RemoveByTagAsync(CacheTags.Cars, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCar_ReturnsTheDomainError_AndStoresNothing_WhenTheDomainRejectsIt()
    {
        var handler = new CreateCarCommandHandler(this.context, this.cache, new FakeLanguageContext());

        var result = await handler.Handle(new CreateCarCommand("Ford", "T", 1500, "en", "ar"), default);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.InvalidYear, result.TopError.Code);
        Assert.Empty(this.context.Cars);
        await this.cache.DidNotReceiveWithAnyArgs().RemoveByTagAsync(default(string)!, default);
    }

    [Fact]
    public async Task UpdateCar_ReturnsNotFound_ForAnUnknownId()
    {
        var handler = new UpdateCarCommandHandler(this.context, this.cache);

        var result = await handler.Handle(new UpdateCarCommand(Guid.NewGuid(), "A", "B", 2020, "en", "ar"), default);

        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
        Assert.Equal(LocalizationKeys.Car.NotFound, result.TopError.Code);
    }

    [Fact]
    public async Task UpdateCar_SavesTheChanges_AndEvictsTheCache()
    {
        var car = CarFactory.Create();
        this.context.Cars.Add(car);
        await this.context.SaveChangesAsync(default);
        var handler = new UpdateCarCommandHandler(this.context, this.cache);

        var result = await handler.Handle(new UpdateCarCommand(car.Id, "Honda", "Civic", 2025, "new en", "new ar"), default);

        Assert.True(result.IsSuccess);
        var stored = await this.context.Cars.AsNoTracking().SingleAsync();
        Assert.Equal("Honda", stored.Make);
        Assert.Equal("new ar", stored.Description.Ar);
        await this.cache.Received(1).RemoveByTagAsync(CacheTags.Cars, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCar_LeavesTheCarUntouched_WhenTheDomainRejectsTheUpdate()
    {
        var car = CarFactory.Create(make: "Toyota");
        this.context.Cars.Add(car);
        await this.context.SaveChangesAsync(default);
        var handler = new UpdateCarCommandHandler(this.context, this.cache);

        var result = await handler.Handle(new UpdateCarCommand(car.Id, string.Empty, "Civic", 2025, "en", "ar"), default);

        Assert.Equal(LocalizationKeys.Car.MakeRequired, result.TopError.Code);
        Assert.Equal("Toyota", (await this.context.Cars.AsNoTracking().SingleAsync()).Make);
        await this.cache.DidNotReceiveWithAnyArgs().RemoveByTagAsync(default(string)!, default);
    }

    [Fact]
    public async Task RemoveCar_ReturnsNotFound_ForAnUnknownId()
    {
        var handler = new RemoveCarCommandHandler(this.context, this.cache);

        var result = await handler.Handle(new RemoveCarCommand(Guid.NewGuid()), default);

        Assert.Equal(LocalizationKeys.Car.NotFound, result.TopError.Code);
    }

    [Fact]
    public async Task RemoveCar_RemovesTheCar_AndEvictsTheCache()
    {
        var car = CarFactory.Create();
        this.context.Cars.Add(car);
        await this.context.SaveChangesAsync(default);
        var handler = new RemoveCarCommandHandler(this.context, this.cache);

        var result = await handler.Handle(new RemoveCarCommand(car.Id), default);

        Assert.True(result.IsSuccess);
        Assert.Empty(this.context.Cars);
        await this.cache.Received(1).RemoveByTagAsync(CacheTags.Cars, Arg.Any<CancellationToken>());
    }
}
