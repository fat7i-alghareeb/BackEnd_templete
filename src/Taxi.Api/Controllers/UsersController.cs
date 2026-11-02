using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taxi.Application.Common.Security;
using Taxi.Application.Features.Users.Commands.SetUserRoles;
using Taxi.Contracts.Requests.Users;

namespace Taxi.Api.Controllers;

/// <summary>User administration. Manager only (<see cref="Policies.CanManageUsers"/>).</summary>
[Route("api/v{version:apiVersion}/users")]
[ApiVersion("1.0")]
[Authorize(Policy = Policies.CanManageUsers)]
public sealed class UsersController(ISender sender) : ApiController
{
    private readonly ISender sender = sender;

    [HttpPut("{userId}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointName("SetUserRoles")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> SetUserRoles(string userId, [FromBody] SetUserRolesRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new SetUserRolesCommand(userId, request.Roles), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
