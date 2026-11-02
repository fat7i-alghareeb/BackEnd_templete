using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Taxi.Api.IntegrationTests.Infrastructure;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Drivers.Dtos;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

public class DriversTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly string NextYear = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Certifications_RoundTrip_ThroughTheOwnedTable_InBothLanguages()
    {
        var client = await this.AsManagerAsync();
        var driver = await this.CreateDriverAsync(client);

        var added = await client.PostAsJsonAsync($"/api/v1/drivers/{driver.Id}/certifications", new { nameEn = "First aid", nameAr = "إسعافات أولية", expiresOn = NextYear });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var english = await client.GetFromJsonAsync<DriverDto>($"/api/v1/drivers/{driver.Id}");
        var arabic = await (await this.AsManagerAsync("ar")).GetFromJsonAsync<DriverDto>($"/api/v1/drivers/{driver.Id}");
        Assert.Equal("First aid", Assert.Single(english!.Certifications).Name);
        Assert.Equal("إسعافات أولية", Assert.Single(arabic!.Certifications).Name);

        var row = await this.WithDbAsync(db => db.Database
            .SqlQuery<string>($"SELECT \"NameAr\" AS \"Value\" FROM \"DriverCertifications\" WHERE \"DriverId\" = {driver.Id}")
            .SingleAsync());
        Assert.Equal("إسعافات أولية", row);
    }

    [Fact]
    public async Task Assignment_EnforcesOneDriverPerCar_AndBlocksDeletingTheCar()
    {
        var client = await this.AsManagerAsync();
        var car = await this.CreateCarAsync(client);
        var first = await this.CreateDriverAsync(client);
        var second = await this.CreateDriverAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/drivers/{first.Id}/car", new { carId = car.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/v1/drivers/{second.Id}/car", new { carId = car.Id })).StatusCode);

        var deleteCar = await client.DeleteAsync($"/api/v1/cars/{car.Id}");
        Assert.Equal(HttpStatusCode.Conflict, deleteCar.StatusCode);
        Assert.Equal("The car is assigned to a driver. Unassign it first.", await deleteCar.TitleAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/drivers/{first.Id}/car")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/cars/{car.Id}")).StatusCode);
    }

    [Fact]
    public async Task LicenceNumbers_AreUnique_CaseInsensitively()
    {
        var client = await this.AsManagerAsync();
        var licence = Unique("Lic");
        await this.CreateDriverAsync(client, licence);

        var duplicate = await client.PostAsJsonAsync("/api/v1/drivers", new { fullName = "Other", licenseNumber = licence.ToLowerInvariant() });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Users_CanRead_ButNotManage_Drivers()
    {
        var user = await this.AsUserAsync();

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/drivers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/v1/drivers", new { fullName = "X", licenseNumber = "Y" })).StatusCode);
    }

    private async Task<DriverDto> CreateDriverAsync(HttpClient client, string? licence = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/drivers", new { fullName = "Test Driver", licenseNumber = licence ?? Unique("Lic") });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DriverDto>(JsonSerializerOptions.Web))!;
    }

    private async Task<CarDto> CreateCarAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cars", new { make = Unique("Car"), model = "M", year = 2020, descriptionEn = "en", descriptionAr = "ar" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CarDto>())!;
    }
}
