using Microsoft.Extensions.Options;

using NSubstitute;

using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Common.Security;
using Taxi.Application.Common.Settings;
using Taxi.Application.Features.Account.Commands.ForgotPassword;
using Taxi.Application.Features.Account.Commands.Register;
using Taxi.Application.Features.Account.Commands.ResendConfirmation;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Application.UnitTests.TestDoubles;
using Taxi.Contracts.Common;
using Taxi.Domain.Common.Results;

using Xunit;

namespace Taxi.Application.UnitTests.Features.Account;

public class AccountCommandHandlerTests
{
    private const string Email = "rider@taxi.com";
    private const string UserId = "user-1";

    private readonly IIdentityService identity = Substitute.For<IIdentityService>();
    private readonly IEmailSender email = Substitute.For<IEmailSender>();
    private readonly FakeTextLocalizer text = new();
    private readonly IOptions<ClientAppSettings> clientApp = Options.Create(new ClientAppSettings
    {
        ConfirmEmailUrl = "https://app.test/confirm",
        ResetPasswordUrl = "https://app.test/reset",
    });

    public AccountCommandHandlerTests()
    {
        this.identity.ValidatePasswordAsync(default!, default!).ReturnsForAnyArgs(Result.Success);
    }

    [Fact]
    public async Task Register_RejectsAWeakPassword_BeforeLookingUpTheEmail()
    {
        // Otherwise a weak password would answer 400 for a new address and 202 for an existing one.
        this.identity.ValidatePasswordAsync("abc", "Password")
            .Returns(Error.ValidationForProperty("Password", LocalizationKeys.Validation.PasswordTooShort, 6));

        var result = await this.Register("abc");

        Assert.Equal(LocalizationKeys.Validation.PasswordTooShort, result.TopError.Code);
        await this.identity.DidNotReceiveWithAnyArgs().FindByEmailAsync(default!);
        await this.email.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Fact]
    public async Task Register_NotifiesTheExistingOwner_InsteadOfRevealingTheAccount()
    {
        this.identity.FindByEmailAsync(Email).Returns(new IdentityUserSummary(UserId, Email, EmailConfirmed: true));

        var result = await this.Register("Secret123");

        Assert.True(result.IsSuccess);
        await this.identity.DidNotReceiveWithAnyArgs().CreateUserAsync(default!, default!, default!);
        await this.email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m => m.To == Email && m.Subject == LocalizationKeys.Account.AlreadyRegisteredSubject),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_CreatesAUserInTheUserRole_AndEmailsAConfirmationLink()
    {
        this.identity.CreateUserAsync(Email, "Secret123", Roles.User).Returns(UserId);
        this.identity.GenerateEmailConfirmationTokenAsync(UserId).Returns("tok+/=");

        var result = await this.Register("Secret123");

        Assert.True(result.IsSuccess);
        await this.email.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == Email
                && m.Subject == LocalizationKeys.Account.ConfirmEmailSubject
                && m.HtmlBody.Contains("https://app.test/confirm?userId=user-1&amp;token=tok%2B%2F%3D")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_ReturnsIdentityErrors_AndSendsNothing_WhenCreationFails()
    {
        this.identity.CreateUserAsync(Email, "Secret123", Roles.User)
            .Returns(Error.Validation(LocalizationKeys.Account.IdentityFailed, "boom"));

        var result = await this.Register("Secret123");

        Assert.Equal(LocalizationKeys.Account.IdentityFailed, result.TopError.Code);
        await this.email.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task ForgotPassword_EmailsOnlyConfirmedAccounts_AndAlwaysSucceeds(bool confirmed, bool expectEmail)
    {
        this.identity.FindByEmailAsync(Email).Returns(new IdentityUserSummary(UserId, Email, confirmed));
        this.identity.GeneratePasswordResetTokenAsync(UserId).Returns("reset-token");
        var handler = new ForgotPasswordCommandHandler(this.identity, this.email, this.text, this.clientApp);

        var result = await handler.Handle(new ForgotPasswordCommand(Email), default);

        Assert.True(result.IsSuccess);
        await this.email.Received(expectEmail ? 1 : 0).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForgotPassword_SucceedsSilently_ForAnUnknownEmail()
    {
        var handler = new ForgotPasswordCommandHandler(this.identity, this.email, this.text, this.clientApp);

        var result = await handler.Handle(new ForgotPasswordCommand("ghost@taxi.com"), default);

        Assert.True(result.IsSuccess);
        await this.email.DidNotReceiveWithAnyArgs().SendAsync(default!);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ResendConfirmation_EmailsOnlyUnconfirmedAccounts(bool confirmed, bool expectEmail)
    {
        this.identity.FindByEmailAsync(Email).Returns(new IdentityUserSummary(UserId, Email, confirmed));
        this.identity.GenerateEmailConfirmationTokenAsync(UserId).Returns("t");
        var handler = new ResendConfirmationCommandHandler(this.identity, this.email, this.text, this.clientApp);

        var result = await handler.Handle(new ResendConfirmationCommand(Email), default);

        Assert.True(result.IsSuccess);
        await this.email.Received(expectEmail ? 1 : 0).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    private Task<Result<Success>> Register(string password) =>
        new RegisterCommandHandler(this.identity, this.email, this.text, this.clientApp)
            .Handle(new RegisterCommand(Email, password), default);
}
