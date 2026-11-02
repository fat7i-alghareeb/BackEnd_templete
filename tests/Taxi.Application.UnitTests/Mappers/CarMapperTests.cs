using Taxi.Application.Common.Files;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Application.UnitTests.TestDoubles;
using Taxi.Contracts.Common;

using Xunit;

namespace Taxi.Application.UnitTests.Mappers;

public class CarMapperTests
{
    [Fact]
    public void ToDto_CopiesTheScalarFields()
    {
        var car = CarFactory.Create("Honda", "Civic", 2021);

        var dto = car.ToDto();

        Assert.Equal((car.Id, "Honda", "Civic", 2021), (dto.Id, dto.Make, dto.Model, dto.Year));
    }

    [Theory]
    [InlineData(Languages.En, "Honda Civic en")]
    [InlineData(Languages.Ar, "Honda Civic ar")]
    public void ToDto_ResolvesTheDescription_ForTheLanguage(string language, string expected)
    {
        Assert.Equal(expected, CarFactory.Create("Honda", "Civic").ToDto(language).Description);
    }

    [Fact]
    public void ToDto_DefaultsToTheDefaultLanguage()
    {
        Assert.Equal("Honda Civic en", CarFactory.Create("Honda", "Civic").ToDto().Description);
    }

    [Fact]
    public void ToDto_ReportsWhetherTheCarHasAnImage()
    {
        var car = CarFactory.Create();
        Assert.False(car.ToDto().HasImage);

        car.SetImage("cars/0123456789abcdef0123456789abcdef.png");
        Assert.True(car.ToDto().HasImage);
    }

    [Fact]
    public void ToDto_MapsACollection_InOrder()
    {
        var cars = new[] { CarFactory.Create(model: "A"), CarFactory.Create(model: "B") };

        Assert.Equal(["A", "B"], cars.ToDto(Languages.Ar).Select(d => d.Model));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' }, "image/webp")]
    [InlineData(new byte[] { (byte)'<', (byte)'?', (byte)'p', (byte)'h', (byte)'p' }, null)]
    [InlineData(new byte[0], null)]
    public void ImageFormat_DetectsByMagicBytes(byte[] header, string? expectedContentType)
    {
        Assert.Equal(expectedContentType, ImageFormat.Detect(header)?.ContentType);
    }
}
