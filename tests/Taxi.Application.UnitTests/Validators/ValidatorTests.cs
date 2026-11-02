using FluentValidation.TestHelper;

using Taxi.Application.Common.Files;
using Taxi.Application.Features.Account.Commands.Register;
using Taxi.Application.Features.Cars.Commands.CreateCar;
using Taxi.Application.Features.Cars.Commands.UpdateCar;
using Taxi.Application.Features.Cars.Commands.UploadCarImage;
using Taxi.Application.Features.Cars.Queries.GetCars;
using Taxi.Application.Features.Users.Commands.SetUserRoles;
using Taxi.Contracts.Common;

using Xunit;

namespace Taxi.Application.UnitTests.Validators;

/// <summary>
/// Tier-2 validation. Every message must be a <see cref="LocalizationKeys"/> constant — that is what
/// ValidationBehavior forwards as the error code and ProblemExtensions translates.
/// </summary>
public class ValidatorTests
{
    [Fact]
    public void CreateCar_AcceptsAValidCommand()
    {
        new CreateCarCommandValidator().TestValidate(ValidCreate()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", LocalizationKeys.Validation.MakeRequired)]
    [InlineData("x", null)]
    public void CreateCar_RequiresAMake(string make, string? expectedKey)
    {
        var result = new CreateCarCommandValidator().TestValidate(ValidCreate() with { Make = make });

        if (expectedKey is null)
        {
            result.ShouldNotHaveValidationErrorFor(c => c.Make);
        }
        else
        {
            result.ShouldHaveValidationErrorFor(c => c.Make).WithErrorMessage(expectedKey);
        }
    }

    [Fact]
    public void CreateCar_LimitsLengths_WithLocalizedKeys()
    {
        var result = new CreateCarCommandValidator().TestValidate(ValidCreate() with
        {
            Make = new string('m', 101),
            Model = new string('m', 101),
            DescriptionEn = new string('d', 1001),
        });

        result.ShouldHaveValidationErrorFor(c => c.Make).WithErrorMessage(LocalizationKeys.Validation.MakeTooLong);
        result.ShouldHaveValidationErrorFor(c => c.Model).WithErrorMessage(LocalizationKeys.Validation.ModelTooLong);
        result.ShouldHaveValidationErrorFor(c => c.DescriptionEn).WithErrorMessage(LocalizationKeys.Validation.DescriptionTooLong);
    }

    [Theory]
    [InlineData(1885, true)]
    [InlineData(1886, false)]
    [InlineData(3000, true)]
    public void CreateCar_ChecksTheYearRange(int year, bool invalid)
    {
        var result = new CreateCarCommandValidator().TestValidate(ValidCreate() with { Year = year });

        if (invalid)
        {
            result.ShouldHaveValidationErrorFor(c => c.Year).WithErrorMessage(LocalizationKeys.Validation.YearInvalid);
        }
        else
        {
            result.ShouldNotHaveValidationErrorFor(c => c.Year);
        }
    }

    [Fact]
    public void UpdateCar_RequiresAnId()
    {
        new UpdateCarCommandValidator()
            .TestValidate(new UpdateCarCommand(Guid.Empty, "A", "B", 2020, "en", "ar"))
            .ShouldHaveValidationErrorFor(c => c.Id)
            .WithErrorMessage(LocalizationKeys.Car.IdRequired);
    }

    [Fact]
    public void GetCars_RejectsAPageOrPageSizeBelowOne()
    {
        var result = new GetCarsQueryValidator().TestValidate(new GetCarsQuery(Page: 0, PageSize: 0));

        result.ShouldHaveValidationErrorFor(q => q.Page).WithErrorMessage(LocalizationKeys.Validation.PageInvalid);
        result.ShouldHaveValidationErrorFor(q => q.PageSize).WithErrorMessage(LocalizationKeys.Validation.PageSizeInvalid);
    }

    [Fact]
    public void GetCars_DoesNotRejectALargePageSize_ItIsClamped()
    {
        new GetCarsQueryValidator().TestValidate(new GetCarsQuery(PageSize: 10_000)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("year", "ASC", true)]
    [InlineData("CreatedAt", "desc", true)]
    [InlineData("price", "asc", false)]
    [InlineData("make", "up", false)]
    public void GetCars_SortIsAWhitelist_CaseInsensitive(string sortBy, string direction, bool valid)
    {
        var result = new GetCarsQueryValidator().TestValidate(new GetCarsQuery(SortBy: sortBy, SortDirection: direction));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("not-an-email", LocalizationKeys.Validation.EmailInvalid)]
    [InlineData("", LocalizationKeys.Validation.EmailRequired)]
    public void Register_ValidatesTheEmail(string email, string expectedKey)
    {
        new RegisterCommandValidator()
            .TestValidate(new RegisterCommand(email, "Secret123"))
            .ShouldHaveValidationErrorFor(c => c.Email)
            .WithErrorMessage(expectedKey);
    }

    [Fact]
    public void SetUserRoles_RequiresAtLeastOneKnownRole()
    {
        var validator = new SetUserRolesCommandValidator();

        validator.TestValidate(new SetUserRolesCommand("u", []))
            .ShouldHaveValidationErrorFor(c => c.Roles).WithErrorMessage(LocalizationKeys.Validation.RolesRequired);

        var unknown = validator.TestValidate(new SetUserRolesCommand("u", ["God"]));
        Assert.Contains(unknown.Errors, e => e.ErrorMessage == LocalizationKeys.Validation.RoleInvalid);

        validator.TestValidate(new SetUserRolesCommand("u", ["manager", "User"])).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, LocalizationKeys.Validation.FileRequired)]
    [InlineData(ImageFormat.MaxBytes + 1, LocalizationKeys.Validation.FileTooLarge)]
    public void UploadCarImage_ChecksTheLength(long length, string expectedKey)
    {
        new UploadCarImageCommandValidator()
            .TestValidate(new UploadCarImageCommand(Guid.NewGuid(), Stream.Null, length))
            .ShouldHaveValidationErrorFor(c => c.Length)
            .WithErrorMessage(expectedKey);
    }

    private static CreateCarCommand ValidCreate() => new("Toyota", "Camry", 2024, "en", "ar");
}
