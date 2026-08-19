using Taxi.Contracts.Common;
using Taxi.Domain.Cars;

using Xunit;

namespace Taxi.Domain.UnitTests.Cars;

public class CarTests
{
    private const string ValidMake = "Toyota";
    private const string ValidModel = "Camry";
    private const int ValidYear = 2024;
    private const string ValidDescriptionEn = "A reliable sedan.";
    private const string ValidDescriptionAr = "سيارة سيدان موثوقة.";

    [Fact]
    public void Create_ReturnsCar_WhenAllInputsAreValid()
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, ValidModel, ValidYear, ValidDescriptionEn, ValidDescriptionAr);

        Assert.True(result.IsSuccess);
        Assert.Equal(ValidMake, result.Value.Make);
        Assert.Equal(ValidModel, result.Value.Model);
        Assert.Equal(ValidYear, result.Value.Year);
        Assert.Equal(ValidDescriptionEn, result.Value.Description.En);
        Assert.Equal(ValidDescriptionAr, result.Value.Description.Ar);
    }

    [Fact]
    public void Create_TrimsDescriptions()
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, ValidModel, ValidYear, "  spaced  ", "  مسافات  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("spaced", result.Value.Description.En);
        Assert.Equal("مسافات", result.Value.Description.Ar);
    }

    [Fact]
    public void Create_Fails_WhenIdIsEmpty()
    {
        var result = Car.Create(Guid.Empty, ValidMake, ValidModel, ValidYear, ValidDescriptionEn, ValidDescriptionAr);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.IdRequired, result.TopError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Fails_WhenMakeIsMissing(string? make)
    {
        var result = Car.Create(Guid.NewGuid(), make!, ValidModel, ValidYear, ValidDescriptionEn, ValidDescriptionAr);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.MakeRequired, result.TopError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_Fails_WhenModelIsMissing(string? model)
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, model!, ValidYear, ValidDescriptionEn, ValidDescriptionAr);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.ModelRequired, result.TopError.Code);
    }

    [Theory]
    [InlineData(1885)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_Fails_WhenYearPredatesTheFirstAutomobile(int year)
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, ValidModel, year, ValidDescriptionEn, ValidDescriptionAr);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.InvalidYear, result.TopError.Code);
    }

    [Fact]
    public void Create_Succeeds_AtTheYearBoundary()
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, ValidModel, 1886, ValidDescriptionEn, ValidDescriptionAr);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("", ValidDescriptionAr)]
    [InlineData(ValidDescriptionEn, "")]
    [InlineData("   ", "   ")]
    public void Create_Fails_WhenEitherLanguageIsMissing(string descriptionEn, string descriptionAr)
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, ValidModel, ValidYear, descriptionEn, descriptionAr);

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.DescriptionRequired, result.TopError.Code);
    }

    [Fact]
    public void Update_MutatesEveryField_WhenInputsAreValid()
    {
        var car = CreateValidCar();

        var result = car.Update("Honda", "Civic", 2025, "Updated.", "محدث.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Honda", car.Make);
        Assert.Equal("Civic", car.Model);
        Assert.Equal(2025, car.Year);
        Assert.Equal("Updated.", car.Description.En);
        Assert.Equal("محدث.", car.Description.Ar);
    }

    [Fact]
    public void Update_LeavesStateUntouched_WhenValidationFails()
    {
        var car = CreateValidCar();

        var result = car.Update(string.Empty, "Civic", 2025, "Updated.", "محدث.");

        Assert.True(result.IsError);
        Assert.Equal(LocalizationKeys.Car.MakeRequired, result.TopError.Code);

        // Guards run before any assignment — a rejected update must not partially apply.
        Assert.Equal(ValidMake, car.Make);
        Assert.Equal(ValidModel, car.Model);
        Assert.Equal(ValidYear, car.Year);
    }

    [Fact]
    public void Update_DoesNotRequireAnId()
    {
        var car = CreateValidCar();
        var originalId = car.Id;

        var result = car.Update("Honda", "Civic", 2025, "Updated.", "محدث.");

        Assert.True(result.IsSuccess);
        Assert.Equal(originalId, car.Id);
    }

    private static Car CreateValidCar()
    {
        var result = Car.Create(Guid.NewGuid(), ValidMake, ValidModel, ValidYear, ValidDescriptionEn, ValidDescriptionAr);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
