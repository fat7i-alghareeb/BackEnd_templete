using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taxi.Api.Infrastructure.Idempotency;
using Taxi.Application.Common.Models;
using Taxi.Application.Common.Security;
using Taxi.Application.Features.Cars.Commands.CreateCar;
using Taxi.Application.Features.Cars.Commands.RemoveCar;
using Taxi.Application.Features.Cars.Commands.UpdateCar;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Queries.GetCarById;
using Taxi.Application.Features.Cars.Queries.GetCars;
using Taxi.Contracts.Requests.Cars;

namespace Taxi.Api.Controllers;

[Route("api/v{version:apiVersion}/cars")]
[ApiVersion("1.0")]
[Authorize(Policy = Policies.CanReadCars)]
public sealed class CarsController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<CarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("GetCars")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetCars([FromQuery] GetCarsRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new GetCarsQuery(
                request.Page,
                request.PageSize,
                request.Search,
                request.Make,
                request.Year,
                request.SortBy,
                request.SortDirection),
            ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("GetCar")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetCar(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetCarByIdQuery(id), ct);
        return result.Match(this.Ok, this.Problem);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CarDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [EndpointName("CreateCar")]
    [Idempotent]
    [Authorize(Policy = Policies.CanManageCars)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> CreateCar([FromBody] CreateCarRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new CreateCarCommand(
                request.Make,
                request.Model,
                request.Year,
                request.DescriptionEn,
                request.DescriptionAr),
            ct);

        return result.Match(
            car => this.CreatedAtAction(nameof(this.GetCar), new { version = this.RequestedApiVersion, id = car.Id }, car),
            this.Problem);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EndpointName("UpdateCar")]
    [Authorize(Policy = Policies.CanManageCars)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> UpdateCar(Guid id, [FromBody] UpdateCarRequest request, CancellationToken ct)
    {
        var result = await sender.Send(
            new UpdateCarCommand(
                id,
                request.Make,
                request.Model,
                request.Year,
                request.DescriptionEn,
                request.DescriptionAr),
            ct);

        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [EndpointName("RemoveCar")]
    [Authorize(Policy = Policies.CanManageCars)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> RemoveCar(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveCarCommand(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
