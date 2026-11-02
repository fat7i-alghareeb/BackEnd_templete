using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;

using Taxi.Api.IntegrationTests.Infrastructure;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

public class PlatformTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthProbes_AreAnonymous_AndHealthy(string path)
    {
        var response = await this.Anonymous().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await response.ReadJsonAsync()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders_AndNoServerHeader()
    {
        var response = await this.Anonymous().GetAsync("/health/live");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task CredentialEndpoints_AreRateLimited_WithALocalized429()
    {
        using var limited = this.Factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Auth:PermitLimit", "2"));
        var client = limited.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "en");

        HttpResponseMessage? last = null;
        for (var i = 0; i < 3; i++)
        {
            last = await client.PostAsJsonAsync("/api/token/generate", new { email = "nobody@test.local", password = "x" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
        Assert.Equal("Too many requests. Please try again later.", await last.TitleAsync());
    }

    // The 1 MB body limit (413) is a Kestrel limit; TestServer does not implement
    // IHttpMaxRequestBodySizeFeature, so it cannot be exercised in-memory. It was verified against
    // the real server (see the #18 / #15 commits).
}
