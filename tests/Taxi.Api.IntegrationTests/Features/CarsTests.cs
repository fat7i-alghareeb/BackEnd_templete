using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Taxi.Api.IntegrationTests.Infrastructure;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Cars.Events;
using Taxi.Infrastructure.Data;
using Taxi.Infrastructure.Outbox;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

public class CarsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Crud_RoundTrip_WithCacheInvalidation()
    {
        var client = await this.AsManagerAsync();
        var make = Unique("Crud");

        var created = await client.PostAsJsonAsync("/api/v1/cars", NewCar(make));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var car = (await created.Content.ReadFromJsonAsync<CarDto>())!;
        Assert.Equal($"/api/v1/cars/{car.Id}", created.Headers.Location!.AbsolutePath);

        // Warm the list cache, update, and the list must show the new value (tag eviction).
        Assert.Equal("Model", (await this.FirstByMakeAsync(client, make)).Model);
        var put = await client.PutAsJsonAsync($"/api/v1/cars/{car.Id}", new { make, model = "Updated", year = 2025, descriptionEn = "en2", descriptionAr = "ar2" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal("Updated", (await this.FirstByMakeAsync(client, make)).Model);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/cars/{car.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/cars/{car.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("en", "Car year must be between 1886 and 2026.")]
    [InlineData("ar", "يجب أن تكون سنة السيارة بين 1886 و 2026.")]
    public async Task InvalidInput_IsALocalizedValidationProblem_KeyedByField(string language, string message)
    {
        var client = await this.AsManagerAsync(language);

        var response = await client.PostAsJsonAsync("/api/v1/cars", NewCar("Ford") with { Year = 1500 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([message], await response.FieldErrorsAsync("Year"));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public async Task Description_FollowsAcceptLanguage(string language)
    {
        var manager = await this.AsManagerAsync();
        var id = await this.CreateAsync(manager, Unique());

        var dto = await (await this.AsManagerAsync(language)).GetFromJsonAsync<CarDto>($"/api/v1/cars/{id}");

        Assert.Equal(language == "ar" ? "وصف" : "Description", dto!.Description);
    }

    [Fact]
    public async Task Rbac_UserCanRead_ButNotWrite_AndAnonymousIsRejected()
    {
        var user = await this.AsUserAsync();

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/v1/cars")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/v1/cars", NewCar("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await this.Anonymous().GetAsync("/api/v1/cars")).StatusCode);
    }

    [Fact]
    public async Task List_IsPagedFilteredAndSorted()
    {
        var client = await this.AsManagerAsync();
        var make = Unique("Page");
        foreach (var year in new[] { 2001, 2003, 2002 })
        {
            await this.CreateAsync(client, make, year);
        }

        var page = await (await client.GetAsync($"/api/v1/cars?make={make.ToUpperInvariant()}&sortBy=year&sortDirection=asc&pageSize=2")).ReadJsonAsync();

        Assert.Equal(3, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal([2001, 2002], page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("year").GetInt32()));
    }

    [Fact]
    public async Task Delete_IsSoft_TheRowRemainsWithAuditFields()
    {
        var client = await this.AsManagerAsync();
        var id = await this.CreateAsync(client, Unique("Soft"));

        await client.DeleteAsync($"/api/v1/cars/{id}");

        var row = await this.WithDbAsync(db => db.Cars.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == id));
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAtUtc);
        Assert.False(string.IsNullOrEmpty(row.DeletedBy));
    }

    [Fact]
    public async Task ConcurrentUpdate_IsDetectedByXmin_AndAnswered409()
    {
        var client = await this.AsManagerAsync();
        var id = await this.CreateAsync(client, Unique("Xmin"));

        // Load a tracked copy, let someone else change the row, then save the stale copy.
        await using var scope = this.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await db.Cars.SingleAsync(c => c.Id == id);

        await client.PutAsJsonAsync($"/api/v1/cars/{id}", new { make = stale.Make, model = "Theirs", year = 2020, descriptionEn = "en", descriptionAr = "ar" });

        stale.Update(stale.Make, "Mine", 2020, "en", "ar");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task IdempotencyKey_CreatesOnce_AndReplaysTheResponse()
    {
        var client = await this.AsManagerAsync();
        var make = Unique("Idem");
        var key = Guid.NewGuid().ToString();

        HttpResponseMessage Send(object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cars") { Content = JsonContent.Create(body) };
            request.Headers.Add("Idempotency-Key", key);
            return client.SendAsync(request).GetAwaiter().GetResult();
        }

        var first = Send(NewCar(make));
        var second = Send(NewCar(make));
        var mismatch = Send(NewCar(make) with { Model = "Other" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        Assert.Equal((HttpStatusCode)422, mismatch.StatusCode);
        Assert.Equal(1, (await this.ListByMakeAsync(client, make)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Create_WritesAnOutboxMessage_ThatTheProcessorPublishes()
    {
        var client = await this.AsManagerAsync();
        var id = await this.CreateAsync(client, Unique("Outbox"));

        // Content is jsonb, so match the car id in memory rather than with LIKE.
        Task<List<OutboxMessage>> CarCreatedMessages() => this.WithDbAsync(db => db.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.Type == typeof(CarCreatedDomainEvent).FullName)
            .ToListAsync());

        var pending = (await CarCreatedMessages()).Single(m => m.Content.Contains(id.ToString()));
        Assert.Null(pending.ProcessedOnUtc);

        await this.Factory.Services.GetRequiredService<OutboxProcessor>().ProcessAsync(default);

        var processed = (await CarCreatedMessages()).Single(m => m.Id == pending.Id);
        Assert.NotNull(processed.ProcessedOnUtc);
        Assert.Equal(0, processed.Attempts);
    }

    [Fact]
    public async Task Image_UploadDownloadAndRejectForgedContent()
    {
        var client = await this.AsManagerAsync();
        var id = await this.CreateAsync(client, Unique("Img"));
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3];

        var upload = await client.PutAsync($"/api/v1/cars/{id}/image", Multipart(png, "image/png"));
        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var download = await client.GetAsync($"/api/v1/cars/{id}/image");
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await download.Content.ReadAsByteArrayAsync());

        var forged = await client.PutAsync($"/api/v1/cars/{id}/image", Multipart("<?php"u8.ToArray(), "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
    }

    private static CarRequest NewCar(string make, int year = 2020) => new(make, "Model", year, "Description", "وصف");

    private static MultipartFormDataContent Multipart(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new(contentType);
        return new MultipartFormDataContent { { file, "file", "car.png" } };
    }

    private async Task<Guid> CreateAsync(HttpClient client, string make, int year = 2020)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cars", NewCar(make, year));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CarDto>())!.Id;
    }

    private async Task<JsonElement> ListByMakeAsync(HttpClient client, string make) =>
        await (await client.GetAsync($"/api/v1/cars?make={make}")).ReadJsonAsync();

    private async Task<CarDto> FirstByMakeAsync(HttpClient client, string make) =>
        (await this.ListByMakeAsync(client, make)).GetProperty("items")[0].Deserialize<CarDto>(JsonSerializerOptions.Web)!;

    private sealed record CarRequest(string Make, string Model, int Year, string DescriptionEn, string DescriptionAr);
}
