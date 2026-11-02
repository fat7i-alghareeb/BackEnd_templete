using System.Net;
using System.Net.Http.Json;

using Taxi.Api.IntegrationTests.Infrastructure;
using Taxi.Application.Features.Cars.Dtos;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

/// <summary>v1 (deprecated) and v2 served by one interleaved controller.</summary>
public class VersioningTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task V2_Read_ReturnsBothLanguages_AndTimestamps()
    {
        var client = await this.AsManagerAsync();
        var created = await client.PostAsJsonAsync("/api/v2/cars", new { make = Unique("V2"), model = "M", year = 2024, descriptionEn = "English", descriptionAr = "عربي" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.StartsWith("/api/v2/cars/", created.Headers.Location!.AbsolutePath);

        var details = await client.GetFromJsonAsync<CarDetailsDto>(created.Headers.Location);
        Assert.Equal(new LocalizedTextDto("English", "عربي"), details!.Description);
        Assert.NotEqual(default, details.CreatedAtUtc);
    }

    [Fact]
    public async Task V1_IsAdvertisedAsDeprecated_WithASunsetDate()
    {
        var client = await this.AsManagerAsync();

        var response = await client.GetAsync("/api/v1/cars");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1.0", response.Headers.GetValues("api-deprecated-versions").Single());
        Assert.Equal("2.0", response.Headers.GetValues("api-supported-versions").Single());
        Assert.True(response.Headers.Contains("Sunset"));
    }

    [Fact]
    public async Task V1AndV2_FilterTheSameWay()
    {
        var client = await this.AsManagerAsync();
        var make = Unique("Same");
        foreach (var year in new[] { 2010, 2012 })
        {
            await client.PostAsJsonAsync("/api/v1/cars", new { make, model = "M", year, descriptionEn = "en", descriptionAr = "ar" });
        }

        var v1 = await (await client.GetAsync($"/api/v1/cars?make={make}&sortBy=year&sortDirection=desc")).ReadJsonAsync();
        var v2 = await (await client.GetAsync($"/api/v2/cars?make={make}&sortBy=year&sortDirection=desc")).ReadJsonAsync();

        int[] Years(System.Text.Json.JsonElement page) =>
            page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("year").GetInt32()).ToArray();

        Assert.Equal([2012, 2010], Years(v1));
        Assert.Equal(Years(v1), Years(v2));
    }

    [Fact]
    public async Task UnknownVersion_Is404()
    {
        var client = await this.AsManagerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v3/cars")).StatusCode);
    }
}
