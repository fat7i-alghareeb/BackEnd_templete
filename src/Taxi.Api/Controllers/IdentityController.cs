using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Commands.GenerateToken;
using Taxi.Application.Features.Identity.Commands.RefreshToken;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Application.Features.Identity.Queries.GetUserInfo;
using Taxi.Contracts.Requests.Identity;

namespace Taxi.Api.Controllers;

[Route("api/token")]
[ApiVersionNeutral]
public sealed class IdentityController(ISender sender, IUser currentUser) : ApiController
{
    [HttpPost("generate")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [EndpointName("GenerateToken")]
    public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenRequest request, CancellationToken ct)
    {
        var result = await sender.Send(new GenerateTokenCommand(request.Email, request.Password), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("RefreshToken")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new RefreshTokenCommand(request.ExpiredAccessToken, request.RefreshToken),
            ct);

        return result.Match(this.Ok, this.Problem);
    }

    [Authorize]
    [HttpGet("user-info")]
    [ProducesResponseType(typeof(AppUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [EndpointName("GetUserInfo")]
    public async Task<IActionResult> GetUserInfo(CancellationToken ct)
    {
        var result = await sender.Send(new GetUserByIdQuery(currentUser.Id!), ct);
        return result.Match(this.Ok, this.Problem);
    }
}
