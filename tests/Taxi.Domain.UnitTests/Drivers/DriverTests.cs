using Taxi.Contracts.Common;
using Taxi.Domain.Drivers;
using Taxi.Domain.Drivers.Events;

using Xunit;

namespace Taxi.Domain.UnitTests.Drivers;

public class DriverTests
{
    private static readonly DateOnly NextYear = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1);

    [Fact]
    public void Create_TrimsTheName_AndNormalisesTheLicence()
    {
        var driver = Driver.Create(Guid.NewGuid(), "  Sami Haddad ", " ab-123 ").Value;

        Assert.Equal("Sami Haddad", driver.FullName);
        Assert.Equal("AB-123", driver.LicenseNumber);
        Assert.Null(driver.AssignedCarId);
        Assert.Empty(driver.Certifications);
    }

    [Theory]
    [InlineData("", "L1", LocalizationKeys.Driver.FullNameRequired)]
    [InlineData("Sami", " ", LocalizationKeys.Driver.LicenseNumberRequired)]
    public void Create_RejectsMissingFields(string name, string licence, string expected)
    {
        Assert.Equal(expected, Driver.Create(Guid.NewGuid(), name, licence).TopError.Code);
    }

    [Fact]
    public void Create_RequiresAnId()
    {
        Assert.Equal(LocalizationKeys.Driver.IdRequired, Driver.Create(Guid.Empty, "Sami", "L1").TopError.Code);
    }

    [Fact]
    public void Update_LeavesStateUntouched_WhenRejected()
    {
        var driver = NewDriver();

        var result = driver.Update(string.Empty, "NEW");

        Assert.True(result.IsError);
        Assert.Equal("L1", driver.LicenseNumber);
    }

    [Fact]
    public void AddCertification_AddsToTheCollection()
    {
        var driver = NewDriver();

        var certification = driver.AddCertification(Guid.NewGuid(), " First aid ", "إسعاف", NextYear).Value;

        Assert.Equal("First aid", certification.Name.En);
        Assert.Single(driver.Certifications);
    }

    [Fact]
    public void AddCertification_RejectsADuplicateName_CaseInsensitively()
    {
        var driver = NewDriver();
        driver.AddCertification(Guid.NewGuid(), "First aid", "إسعاف", NextYear);

        var result = driver.AddCertification(Guid.NewGuid(), "FIRST AID", "x", NextYear);

        Assert.Equal(LocalizationKeys.Driver.CertificationAlreadyHeld, result.TopError.Code);
        Assert.Single(driver.Certifications);
    }

    [Fact]
    public void AddCertification_RequiresBothLanguages()
    {
        Assert.Equal(
            LocalizationKeys.Driver.CertificationNameRequired,
            NewDriver().AddCertification(Guid.NewGuid(), "First aid", " ", NextYear).TopError.Code);
    }

    [Fact]
    public void AddCertification_CapsTheCollection_WithAParametrizedError()
    {
        var driver = NewDriver();
        for (var i = 0; i < Driver.MaxCertifications; i++)
        {
            driver.AddCertification(Guid.NewGuid(), $"C{i}", $"ش{i}", NextYear);
        }

        var result = driver.AddCertification(Guid.NewGuid(), "One more", "x", NextYear);

        Assert.Equal(LocalizationKeys.Driver.TooManyCertifications, result.TopError.Code);
        Assert.Equal([Driver.MaxCertifications], result.TopError.Args);
    }

    [Fact]
    public void RemoveCertification_RemovesIt_OrReportsNotFound()
    {
        var driver = NewDriver();
        var certification = driver.AddCertification(Guid.NewGuid(), "First aid", "إسعاف", NextYear).Value;

        Assert.True(driver.RemoveCertification(certification.Id).IsSuccess);
        Assert.Empty(driver.Certifications);
        Assert.Equal(LocalizationKeys.Driver.CertificationNotFound, driver.RemoveCertification(certification.Id).TopError.Code);
    }

    [Fact]
    public void AssignCar_SetsTheCar_AndRaisesAnEventWithThePreviousCar()
    {
        var driver = NewDriver();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        driver.AssignCar(first);
        driver.AssignCar(second);

        Assert.Equal(second, driver.AssignedCarId);
        var last = Assert.IsType<DriverAssignedToCarDomainEvent>(driver.DomainEvents.Last());
        Assert.Equal((driver.Id, second, (Guid?)first), (last.DriverId, last.CarId, last.PreviousCarId));
    }

    [Fact]
    public void AssignCar_ToTheSameCar_IsAConflict_AndRaisesNothing()
    {
        var driver = NewDriver();
        var car = Guid.NewGuid();
        driver.AssignCar(car);

        var result = driver.AssignCar(car);

        Assert.Equal(LocalizationKeys.Driver.AlreadyAssignedToCar, result.TopError.Code);
        Assert.Single(driver.DomainEvents);
    }

    [Fact]
    public void UnassignCar_RequiresAnAssignment()
    {
        var driver = NewDriver();

        Assert.Equal(LocalizationKeys.Driver.NotAssigned, driver.UnassignCar().TopError.Code);

        driver.AssignCar(Guid.NewGuid());
        Assert.True(driver.UnassignCar().IsSuccess);
        Assert.Null(driver.AssignedCarId);
    }

    private static Driver NewDriver() => Driver.Create(Guid.NewGuid(), "Sami", "L1").Value;
}
