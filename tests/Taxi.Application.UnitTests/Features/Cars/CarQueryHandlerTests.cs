using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Queries.GetCarById;
using Taxi.Application.Features.Cars.Queries.GetCars;
using Taxi.Application.UnitTests.TestDoubles;
using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Application.UnitTests.Features.Cars;

public class CarQueryHandlerTests
{
    private readonly TestAppDbContext context = TestAppDbContext.Create();

    [Fact]
    public async Task GetCarById_ReturnsNotFound_ForAnUnknownId()
    {
        var handler = new GetCarByIdQueryHandler(this.context, new FakeLanguageContext());

        var result = await handler.Handle(new GetCarByIdQuery(Guid.NewGuid()), default);

        Assert.Equal(ErrorKind.NotFound, result.TopError.Type);
    }

    [Theory]
    [InlineData(Languages.En, "Toyota Camry en")]
    [InlineData(Languages.Ar, "Toyota Camry ar")]
    public async Task GetCarById_ResolvesTheDescription_InTheRequestLanguage(string language, string expected)
    {
        var car = CarFactory.Create();
        await this.SeedAsync(car);
        var handler = new GetCarByIdQueryHandler(this.context, new FakeLanguageContext(language));

        var result = await handler.Handle(new GetCarByIdQuery(car.Id), default);

        Assert.Equal(expected, result.Value.Description);
    }

    [Fact]
    public async Task GetCars_PagesTheResult_AndReportsTotals()
    {
        await this.SeedAsync(Enumerable.Range(0, 5).Select(i => CarFactory.Create(model: $"M{i}")).ToArray());

        var page = (await this.GetCarsAsync(new GetCarsQuery(Page: 2, PageSize: 2))).Value;

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task GetCars_ClampsThePageSize_To100()
    {
        await this.SeedAsync(CarFactory.Create());

        var page = (await this.GetCarsAsync(new GetCarsQuery(PageSize: 5000))).Value;

        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task GetCars_SearchMatchesMakeOrModel_CaseInsensitively()
    {
        await this.SeedAsync(CarFactory.Create("Honda", "Civic"), CarFactory.Create("Toyota", "Camry"), CarFactory.Create("Civicorp", "X"));

        var page = (await this.GetCarsAsync(new GetCarsQuery(Search: "CIVIC"))).Value;

        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task GetCars_FiltersByExactMake_AndYear()
    {
        await this.SeedAsync(CarFactory.Create("Honda", year: 2020), CarFactory.Create("Honda", year: 2021), CarFactory.Create("Hondaish", year: 2020));

        var page = (await this.GetCarsAsync(new GetCarsQuery(Make: "honda", Year: 2020))).Value;

        var only = Assert.Single(page.Items);
        Assert.Equal("Honda", only.Make);
        Assert.Equal(2020, only.Year);
    }

    [Theory]
    [InlineData(SortDirections.Asc, new[] { 2019, 2020, 2021 })]
    [InlineData(SortDirections.Desc, new[] { 2021, 2020, 2019 })]
    public async Task GetCars_SortsByTheRequestedField(string direction, int[] expectedYears)
    {
        await this.SeedAsync(CarFactory.Create(year: 2020), CarFactory.Create(year: 2019), CarFactory.Create(year: 2021));

        var page = (await this.GetCarsAsync(new GetCarsQuery(SortBy: "YEAR", SortDirection: direction))).Value;

        Assert.Equal(expectedYears, page.Items.Select(c => c.Year));
    }

    [Fact]
    public void GetCars_CacheKey_IsNormalised_SoEquivalentRequestsShareAnEntry()
    {
        var a = new GetCarsQuery(Make: "Tesla", SortBy: "Year", SortDirection: "ASC");
        var b = new GetCarsQuery(Make: " tesla ", SortBy: "year", SortDirection: "asc");
        var c = new GetCarsQuery(Make: "Tesla", SortBy: CarSortFields.Year, SortDirection: SortDirections.Desc);

        Assert.Equal(a.CacheKey, b.CacheKey);
        Assert.NotEqual(a.CacheKey, c.CacheKey);
    }

    private Task<Result<PaginatedList<CarDto>>> GetCarsAsync(GetCarsQuery query) =>
        new GetCarsQueryHandler(this.context, new FakeLanguageContext()).Handle(query, default);

    private async Task SeedAsync(params Car[] cars)
    {
        this.context.Cars.AddRange(cars);
        await this.context.SaveChangesAsync(default);
        this.context.ChangeTracker.Clear();
    }
}
