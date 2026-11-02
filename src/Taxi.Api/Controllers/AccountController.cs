using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Taxi.Api.Infrastructure.RateLimiting;
using Taxi.Application.Features.Account.Commands.ChangePassword;
using Taxi.Application.Features.Account.Commands.ConfirmEmail;
using Taxi.Application.Features.Account.Commands.ForgotPassword;
using Taxi.Application.Features.Account.Commands.Logout;
using Taxi.Application.Features.Account.Commands.Register;
using Taxi.Application.Features.Account.Commands.ResendConfirmation;
using Taxi.Application.Features.Account.Commands.ResetPassword;
using Taxi.Contracts.Requests.Account;

namespace Taxi.Api.Controllers;

/// <summary>
/// Self-service account lifecycle. Version-neutral like <see cref="IdentityController"/>: a client
/// that cannot sign in cannot negotiate a version. Every endpoint that takes a password or sends an
/// email uses the strict <see cref="RateLimitPolicies.Auth"/> limiter. Endpoints that take an email
/// address answer 202 whether or not an account exists, so they cannot be used to enumerate users.
/// </summary>
[Route("api/account")]
[ApiVersionNeutral]
public sealed class AccountController(ISender sender) : ApiController
{
    private readonly ISender sender = sender;

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("Register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new RegisterCommand(request.Email, request.Password), ct);
        return result.Match(_ => this.Accepted(), this.Problem);
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("ConfirmEmail")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new ConfirmEmailCommand(request.UserId, request.Token), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("ResendConfirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new ResendConfirmationCommand(request.Email), ct);
        return result.Match(_ => this.Accepted(), this.Problem);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("ForgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new ForgotPasswordCommand(request.Email), ct);
        return result.Match(_ => this.Accepted(), this.Problem);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("ResetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(
            new ResetPasswordCommand(request.Email, request.Token, request.NewPassword),
            ct);

        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [EndpointName("ChangePassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword),
            ct);

        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [EndpointName("Logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var result = await this.sender.Send(new LogoutCommand(), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
