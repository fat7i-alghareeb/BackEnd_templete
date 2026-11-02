using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Taxi.Application.Features.Identity.Dtos;
using Taxi.Infrastructure.Data;

using Xunit;

namespace Taxi.Api.IntegrationTests.Infrastructure;

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    protected static string Unique(string prefix = "T") => $"{prefix}{Guid.NewGuid():N}"[..20];

    protected HttpClient Anonymous(string language = "en")
    {
        var client = this.Factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));
        return client;
    }

    protected async Task<HttpClient> AsAsync(string email, string password, string language = "en")
    {
        var tokens = await this.LoginAsync(email, password);
        var client = this.Anonymous(language);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    protected Task<HttpClient> AsManagerAsync(string language = "en") =>
        this.AsAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword, language);

    protected Task<HttpClient> AsUserAsync(string language = "en") =>
        this.AsAsync(ApiFactory.UserEmail, ApiFactory.UserPassword, language);

    protected async Task<TokenResponse> LoginAsync(string email, string password)
    {
        var response = await this.Anonymous().PostAsJsonAsync("/api/token/generate", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    protected async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = this.Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
