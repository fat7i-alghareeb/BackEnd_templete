using System.Net;
using System.Net.Http.Json;

using Taxi.Api.IntegrationTests.Infrastructure;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

public class AccountTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Register_Confirm_Login_ResetPassword()
    {
        var email = $"{Unique("rider")}@test.local".ToLowerInvariant();
        var client = this.Anonymous();

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/account/register", new { email, password = "Rider123!" })).StatusCode);

        // Unconfirmed: the password is right but sign-in is refused.
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/token/generate", new { email, password = "Rider123!" })).StatusCode);

        var confirm = this.Factory.Emails.LastLinkQuery(email);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/confirm-email", new { userId = confirm["userId"], token = confirm["token"] })).StatusCode);
        Assert.NotNull(await this.LoginAsync(email, "Rider123!"));

        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/account/forgot-password", new { email })).StatusCode);
        var reset = this.Factory.Emails.LastLinkQuery(email);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/reset-password", new { email, token = reset["token"], newPassword = "Rider456!" })).StatusCode);

        Assert.NotNull(await this.LoginAsync(email, "Rider456!"));
    }

    [Fact]
    public async Task Register_WithAnExistingEmail_Answers202_AndNotifiesTheOwner()
    {
        var before = this.Factory.Emails.Sent.Count(m => m.To == ApiFactory.UserEmail);

        var response = await this.Anonymous().PostAsJsonAsync("/api/account/register", new { email = ApiFactory.UserEmail, password = "Whatever1!" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(before + 1, this.Factory.Emails.Sent.Count(m => m.To == ApiFactory.UserEmail));
    }

    [Fact]
    public async Task Register_WithAWeakPassword_IsALocalizedFieldError()
    {
        var response = await this.Anonymous("ar").PostAsJsonAsync("/api/account/register", new { email = "weak@test.local", password = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["يجب ألا تقل كلمة المرور عن 6 أحرف."], await response.FieldErrorsAsync("Password"));
    }

    [Fact]
    public async Task ManagerCannotRemoveTheirOwnManagerRole()
    {
        var client = await this.AsManagerAsync();
        var me = (await (await client.GetAsync("/api/token/user-info")).ReadJsonAsync()).GetProperty("userId").GetString();

        var response = await client.PutAsJsonAsync($"/api/v1/users/{me}/roles", new { roles = new[] { "User" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
