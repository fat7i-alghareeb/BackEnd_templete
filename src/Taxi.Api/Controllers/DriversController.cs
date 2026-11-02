using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taxi.Api.Infrastructure.Idempotency;
using Taxi.Application.Common.Models;
using Taxi.Application.Common.Security;
using Taxi.Application.Features.Drivers.Commands.AddDriverCertification;
using Taxi.Application.Features.Drivers.Commands.AssignDriverCar;
using Taxi.Application.Features.Drivers.Commands.CreateDriver;
using Taxi.Application.Features.Drivers.Commands.RemoveDriver;
using Taxi.Application.Features.Drivers.Commands.RemoveDriverCertification;
using Taxi.Application.Features.Drivers.Commands.UnassignDriverCar;
using Taxi.Application.Features.Drivers.Commands.UpdateDriver;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Application.Features.Drivers.Queries.GetDriverById;
using Taxi.Application.Features.Drivers.Queries.GetDrivers;
using Taxi.Contracts.Requests.Drivers;

namespace Taxi.Api.Controllers;

/// <summary>
/// The second reference controller: a child collection as a sub-resource
/// (<c>/drivers/{id}/certifications/{certificationId}</c>) and a relationship as a singleton
/// sub-resource (<c>/drivers/{id}/car</c>: PUT assigns, DELETE unassigns).
/// </summary>
[Route("api/v{version:apiVersion}/drivers")]
[ApiVersion("1.0")]
[Authorize(Policy = Policies.CanReadDrivers)]
public sealed class DriversController(ISender sender) : ApiController
{
    private readonly ISender sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<DriverDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("GetDrivers")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetDrivers([FromQuery] GetDriversRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new GetDriversQuery(request.Page, request.PageSize, request.Search), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("GetDriver")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetDriver(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new GetDriverByIdQuery(id), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpPost]
    [ProducesResponseType(typeof(DriverDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointName("CreateDriver")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [Idempotent]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateDriver([FromBody] CreateDriverRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new CreateDriverCommand(request.FullName, request.LicenseNumber), ct);

        return result.Match(
            driver => this.CreatedAtAction(nameof(this.GetDriver), new { version = this.RequestedApiVersion, id = driver.Id }, driver),
            this.Problem);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointName("UpdateDriver")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> UpdateDriver(Guid id, [FromBody] UpdateDriverRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new UpdateDriverCommand(id, request.FullName, request.LicenseNumber), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("RemoveDriver")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> RemoveDriver(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new RemoveDriverCommand(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpPost("{id:guid}/certifications")]
    [ProducesResponseType(typeof(DriverCertificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointName("AddDriverCertification")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> AddDriverCertification(Guid id, [FromBody] AddDriverCertificationRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(
            new AddDriverCertificationCommand(id, request.NameEn, request.NameAr, request.ExpiresOn),
            ct);

        // The certification has no GET of its own; it lives inside the driver resource.
        return result.Match(
            certification => this.CreatedAtAction(nameof(this.GetDriver), new { version = this.RequestedApiVersion, id }, certification),
            this.Problem);
    }

    [HttpDelete("{id:guid}/certifications/{certificationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("RemoveDriverCertification")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> RemoveDriverCertification(Guid id, Guid certificationId, CancellationToken ct)
    {
        var result = await this.sender.Send(new RemoveDriverCertificationCommand(id, certificationId), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpPut("{id:guid}/car")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointName("AssignDriverCar")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> AssignDriverCar(Guid id, [FromBody] AssignDriverCarRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(new AssignDriverCarCommand(id, request.CarId), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpDelete("{id:guid}/car")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointName("UnassignDriverCar")]
    [Authorize(Policy = Policies.CanManageDrivers)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> UnassignDriverCar(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new UnassignDriverCarCommand(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
