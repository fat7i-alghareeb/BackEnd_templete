using System.Net;
using System.Net.Http.Json;

using Taxi.Api.IntegrationTests.Infrastructure;
using Taxi.Application.Features.Identity.Dtos;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

public class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Login_ReturnsTokens_ForTheSeededManager()
    {
        var tokens = await this.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);

        Assert.False(string.IsNullOrEmpty(tokens.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Theory]
    [InlineData("en", "Email or password is incorrect")]
    [InlineData("ar", "البريد الإلكتروني أو كلمة المرور غير صحيحة")]
    public async Task Login_Fails_Identically_ForAWrongPasswordAndAnUnknownEmail(string language, string title)
    {
        var client = this.Anonymous(language);

        var wrongPassword = await client.PostAsJsonAsync("/api/token/generate", new { email = ApiFactory.UserEmail, password = "nope" });
        var unknownEmail = await client.PostAsJsonAsync("/api/token/generate", new { email = "ghost@test.local", password = "nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(title, await wrongPassword.TitleAsync());
        Assert.Equal(title, await unknownEmail.TitleAsync());
    }

    [Fact]
    public async Task Refresh_RotatesTheRefreshToken_SoTheOldOneStopsWorking()
    {
        var first = await this.LoginAsync(ApiFactory.AdminEmail, ApiFactory.AdminPassword);
        var client = this.Anonymous();

        var refreshed = await client.PostAsJsonAsync("/api/token/refresh-token", new { expiredAccessToken = first.AccessToken, refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var second = (await refreshed.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var replay = await client.PostAsJsonAsync("/api/token/refresh-token", new { expiredAccessToken = first.AccessToken, refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAGarbageAccessToken_IsAClientError_NotA500()
    {
        var response = await this.Anonymous().PostAsJsonAsync("/api/token/refresh-token", new { expiredAccessToken = "garbage", refreshToken = "x" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken()
    {
        var tokens = await this.LoginAsync(ApiFactory.UserEmail, ApiFactory.UserPassword);
        var client = this.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/account/logout", null)).StatusCode);

        var refresh = await this.Anonymous().PostAsJsonAsync("/api/token/refresh-token", new { expiredAccessToken = tokens.AccessToken, refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Conflict, refresh.StatusCode);
    }

    [Fact]
    public async Task UserInfo_RequiresAToken_AndReturnsTheRoles()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await this.Anonymous().GetAsync("/api/token/user-info")).StatusCode);

        var client = await this.AsManagerAsync();
        var info = await (await client.GetAsync("/api/token/user-info")).ReadJsonAsync();

        Assert.Contains("Manager", info.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }
}
