using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
[Authorize]
public sealed class CarsController(ISender sender) : ApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(List<CarDto>), StatusCodes.Status200OK)]
    [EndpointName("GetCars")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetCars(CancellationToken ct)
    {
        var result = await sender.Send(new GetCarsQuery(), ct);
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
    [EndpointName("CreateCar")]
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
            car => this.CreatedAtAction(nameof(this.GetCar), new { version = "1.0", id = car.Id }, car),
            this.Problem);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("UpdateCar")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> UpdateCar(Guid id, [FromBody] UpdateCarRequest request, CancellationToken ct)
    {
        if (id != request.Id)
        {
            return this.BadRequest();
        }

        var result = await sender.Send(
            new UpdateCarCommand(
                request.Id,
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
    [EndpointName("RemoveCar")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> RemoveCar(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RemoveCarCommand(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
