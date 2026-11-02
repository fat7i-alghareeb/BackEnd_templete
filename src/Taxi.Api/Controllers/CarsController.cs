using System.ComponentModel.DataAnnotations;

using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Taxi.Api.Infrastructure.Idempotency;
using Taxi.Application.Common.Files;
using Taxi.Application.Common.Models;
using Taxi.Application.Common.Security;
using Taxi.Application.Features.Cars.Commands.CreateCar;
using Taxi.Application.Features.Cars.Commands.RemoveCar;
using Taxi.Application.Features.Cars.Commands.RemoveCarImage;
using Taxi.Application.Features.Cars.Commands.UpdateCar;
using Taxi.Application.Features.Cars.Commands.UploadCarImage;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Queries.GetCarById;
using Taxi.Application.Features.Cars.Queries.GetCarImage;
using Taxi.Application.Features.Cars.Queries.GetCars;
using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;

namespace Taxi.Api.Controllers;

[Route("api/v{version:apiVersion}/cars")]
[ApiVersion("1.0")]
[Authorize(Policy = Policies.CanReadCars)]
public sealed class CarsController(ISender sender) : ApiController
{
    /// <summary>The 5 MB image limit plus room for the multipart envelope; the handler enforces 5 MB exactly.</summary>
    private const long ImageUploadRequestLimit = ImageFormat.MaxBytes + (64 * 1024);

    private readonly ISender sender = sender;

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<CarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointName("GetCars")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetCars([FromQuery] GetCarsRequest request, CancellationToken ct)
    {
        var result = await this.sender.Send(
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
        var result = await this.sender.Send(new GetCarByIdQuery(id), ct);
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
        var result = await this.sender.Send(
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
        var result = await this.sender.Send(
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
        var result = await this.sender.Send(new RemoveCarCommand(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpPut("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImageUploadRequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUploadRequestLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [EndpointName("UploadCarImage")]
    [Authorize(Policy = Policies.CanManageCars)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> UploadCarImage(
        Guid id,
        [Required(ErrorMessage = LocalizationKeys.Validation.FileRequired)] IFormFile file,
        CancellationToken ct)
    {
        await using var content = file.OpenReadStream();

        var result = await this.sender.Send(new UploadCarImageCommand(id, content, file.Length), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }

    [HttpGet("{id:guid}/image")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("GetCarImage")]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetCarImage(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new GetCarImageQuery(id), ct);
        return result.Match(image => this.File(image.Content, image.ContentType), this.Problem);
    }

    [HttpDelete("{id:guid}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointName("RemoveCarImage")]
    [Authorize(Policy = Policies.CanManageCars)]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> RemoveCarImage(Guid id, CancellationToken ct)
    {
        var result = await this.sender.Send(new RemoveCarImageCommand(id), ct);
        return result.Match(_ => this.NoContent(), this.Problem);
    }
}
