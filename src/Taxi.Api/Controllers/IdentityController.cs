using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Identity.Dtos;
using Taxi.Application.Features.Identity.Queries.GenerateTokens;
using Taxi.Application.Features.Identity.Queries.GetUserInfo;
using Taxi.Application.Features.Identity.Queries.RefreshTokens;

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
    public async Task<IActionResult> GenerateToken([FromBody] GenerateTokenQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("RefreshToken")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
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
